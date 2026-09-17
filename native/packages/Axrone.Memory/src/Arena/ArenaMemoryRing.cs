using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

[StructLayout(LayoutKind.Explicit, Size = 512)]
internal struct QuadSequence
{
    [FieldOffset(0)]
    public AlignedAtomicCounter128 WriteReserved;

    [FieldOffset(128)]
    public AlignedAtomicCounter128 WriteCommitted;

    [FieldOffset(256)]
    public AlignedAtomicCounter128 ReadReserved;

    [FieldOffset(384)]
    public AlignedAtomicCounter128 ReadCommitted;
}

public sealed unsafe class ArenaMemoryRing<T, TBackoff> : IBatchReservable<T>, IAsyncStreamable<T>, IAdministrativeEndpoint, IArenaCommitTarget, IDisposable
    where T : unmanaged
    where TBackoff : struct, IBackoffPolicy
{
    private readonly IArenaStorageBlock<T> _storage;
    private readonly int _capacity;
    private readonly int _mask;
    private readonly TBackoff _backoff;

    private QuadSequence _sequences;
    private DisposalTracker _tracker;

    private long _writeSpins;
    private long _readSpins;
    private long _writeOverflowCount;
    private long _readUnderflowCount;

    public ArenaMemoryRing(IArenaStorageBlock<T> storage)
    {
        if (storage is null) ThrowHelper.ThrowArgumentNullException(nameof(storage));

        int count = storage.ElementCount;
        if (!BitOperations.IsPow2(count))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(storage), "Storage element count must be a power of 2.");
        }

        _storage = storage;
        _capacity = count;
        _mask = count - 1;
        _backoff = default;
    }

    public int Capacity => _capacity;

    public bool IsDisposed => _tracker.IsDisposed;

    public long CommittedWriteSequence => _sequences.WriteCommitted.Value;

    public long CommittedReadSequence => _sequences.ReadCommitted.Value;

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)(_sequences.WriteCommitted.Value - _sequences.ReadCommitted.Value);
    }

    public bool IsEmpty => Count == 0;

    public bool IsFull => Count == _capacity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReserveWriteBatch(int requestedCount, out Span<T> reservedSpan)
    {
        ThrowIfDisposed();
        if ((uint)requestedCount > (uint)_capacity) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(requestedCount));

        TBackoff backoff = _backoff;
        backoff.Reset();
        int spinCount = 0;

        while (true)
        {
            long writeReserved = _sequences.WriteReserved.Value;
            long readCommitted = _sequences.ReadCommitted.Value;
            long available = _capacity - (int)(writeReserved - readCommitted);

            if (available >= requestedCount)
            {
                long newReserved = writeReserved + requestedCount;
                if (_sequences.WriteReserved.CompareExchange(newReserved, writeReserved))
                {
                    int startIndex = (int)(writeReserved & _mask);
                    Span<T> fullSpan = _storage.Span;

                    if (startIndex + requestedCount <= _capacity)
                    {
                        reservedSpan = fullSpan.Slice(startIndex, requestedCount);
                    }
                    else
                    {
                        reservedSpan = fullSpan.Slice(startIndex);
                    }

                    return requestedCount;
                }
            }
            else if (available <= 0)
            {
                Interlocked.Increment(ref _writeOverflowCount);
                reservedSpan = default;
                return 0;
            }

            backoff.OnSpin(spinCount++);
            Interlocked.Increment(ref _writeSpins);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReserveReadBatch(int requestedCount, out Span<T> reservedSpan)
    {
        ThrowIfDisposed();
        if ((uint)requestedCount > (uint)_capacity) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(requestedCount));

        TBackoff backoff = _backoff;
        backoff.Reset();
        int spinCount = 0;

        while (true)
        {
            long readReserved = _sequences.ReadReserved.Value;
            long writeCommitted = _sequences.WriteCommitted.Value;
            long available = writeCommitted - readReserved;

            if (available >= requestedCount)
            {
                long newReserved = readReserved + requestedCount;
                if (_sequences.ReadReserved.CompareExchange(newReserved, readReserved))
                {
                    int startIndex = (int)(readReserved & _mask);
                    Span<T> fullSpan = _storage.Span;

                    if (startIndex + requestedCount <= _capacity)
                    {
                        reservedSpan = fullSpan.Slice(startIndex, requestedCount);
                    }
                    else
                    {
                        reservedSpan = fullSpan.Slice(startIndex);
                    }

                    return requestedCount;
                }
            }
            else if (available <= 0)
            {
                Interlocked.Increment(ref _readUnderflowCount);
                reservedSpan = default;
                return 0;
            }

            backoff.OnSpin(spinCount++);
            Interlocked.Increment(ref _readSpins);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CommitWrite(int committedCount)
    {
        ThrowIfDisposed();
        if (committedCount < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(committedCount));
        _sequences.WriteCommitted.Add(committedCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CommitRead(int committedCount)
    {
        ThrowIfDisposed();
        if (committedCount < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(committedCount));
        _sequences.ReadCommitted.Add(committedCount);
    }

    public bool TryWrite(ReadOnlySpan<T> source)
    {
        int reserved = ReserveWriteBatch(source.Length, out Span<T> reservedSpan);
        if (reserved <= 0) return false;

        source.CopyTo(reservedSpan);
        CommitWrite(reserved);
        return true;
    }

    public bool TryRead(Span<T> destination)
    {
        int reserved = ReserveReadBatch(destination.Length, out Span<T> reservedSpan);
        if (reserved <= 0) return false;

        reservedSpan.CopyTo(destination);
        CommitRead(reserved);
        return true;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        ReadOnlySpan<T> sourceSpan = source.Span;
        while (sourceSpan.Length > 0)
        {
            int reserved = ReserveWriteBatch(Math.Min(sourceSpan.Length, _capacity), out Span<T> reservedSpan);
            if (reserved > 0)
            {
                sourceSpan.Slice(0, reserved).CopyTo(reservedSpan);
                CommitWrite(reserved);
                sourceSpan = sourceSpan.Slice(reserved);
            }
            else
            {
                Thread.SpinWait(1);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<int> ReadAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        int reserved = ReserveReadBatch(Math.Min(destination.Length, _capacity), out Span<T> reservedSpan);
        if (reserved > 0)
        {
            reservedSpan.CopyTo(destination.Span);
            CommitRead(reserved);
            return new ValueTask<int>(reserved);
        }

        return new ValueTask<int>(0);
    }

    public HealthReport GetHealthReport()
    {
        long writeReserved = _sequences.WriteReserved.Value;
        long writeCommitted = _sequences.WriteCommitted.Value;
        long readReserved = _sequences.ReadReserved.Value;
        long readCommitted = _sequences.ReadCommitted.Value;

        int activeWrites = (int)(writeReserved - writeCommitted);
        int activeReads = (int)(readReserved - readCommitted);
        int used = (int)(writeCommitted - readCommitted);
        double utilization = _capacity > 0 ? (double)used / _capacity * 100.0 : 0.0;

        return new HealthReport(
            IsHealthy: !_tracker.IsDisposed && activeWrites < _capacity,
            IsDisposed: _tracker.IsDisposed,
            IsDraining: false,
            Capacity: _capacity,
            ActiveWriteReservations: activeWrites,
            ActiveReadReservations: activeReads,
            UtilizationPercent: utilization);
    }

    public MetricsSnapshot GetMetricsSnapshot()
    {
        return new MetricsSnapshot(
            TotalWrites: _sequences.WriteCommitted.Value,
            TotalReads: _sequences.ReadCommitted.Value,
            TotalWriteSpins: Volatile.Read(ref _writeSpins),
            TotalReadSpins: Volatile.Read(ref _readSpins),
            WriteBatchReservations: _sequences.WriteReserved.Value,
            ReadBatchReservations: _sequences.ReadReserved.Value,
            WriteOverflowCount: Volatile.Read(ref _writeOverflowCount),
            ReadUnderflowCount: Volatile.Read(ref _readUnderflowCount));
    }

    public void RequestDrain(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => _tracker.ThrowIfDisposed(nameof(ArenaMemoryRing<T, TBackoff>));

    public void Dispose()
    {
        if (_tracker.TryDispose())
        {
            _storage.Dispose();
        }
    }
}
