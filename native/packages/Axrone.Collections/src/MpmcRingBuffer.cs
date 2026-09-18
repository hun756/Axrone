using Axrone.Utility.Alignment;
using Axrone.Utility.Result;

namespace Axrone.Collections;

[StructLayout(LayoutKind.Explicit, Size = 256)]
internal struct PaddedRingPosition
{
    [FieldOffset(0)]
    public long EnqueuePosition;

    [FieldOffset(Alignment.CacheLine128Bytes)]
    public long DequeuePosition;
}

public sealed class MpmcRingBuffer<T, TBackoff> : IRingBuffer<T>
    where TBackoff : struct, ISpinBackoff
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct Slot
    {
        public long Sequence;
        public T Value;
    }

    private sealed class ProducerEndpoint : IProducer<T>
    {
        private readonly MpmcRingBuffer<T, TBackoff> _buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProducerEndpoint(MpmcRingBuffer<T, TBackoff> buffer) => _buffer = buffer;

        public int Capacity => _buffer.Capacity;
        public int Count => _buffer.Count;
        public bool IsFull => _buffer.IsFull;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(in T item) => _buffer.TryEnqueue(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result Enqueue(in T item, TimeSpan timeout, CancellationToken cancellationToken = default)
            => _buffer.Enqueue(item, timeout, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(in T item, CancellationToken cancellationToken = default)
            => _buffer.Enqueue(item, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EnqueueRange(ReadOnlySpan<T> source) => _buffer.EnqueueRange(source);
    }

    private sealed class ConsumerEndpoint : IConsumer<T>
    {
        private readonly MpmcRingBuffer<T, TBackoff> _buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConsumerEndpoint(MpmcRingBuffer<T, TBackoff> buffer) => _buffer = buffer;

        public int Capacity => _buffer.Capacity;
        public int Count => _buffer.Count;
        public bool IsEmpty => _buffer.IsEmpty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue([MaybeNullWhen(false)] out T item) => _buffer.TryDequeue(out item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T> Dequeue(TimeSpan timeout, CancellationToken cancellationToken = default)
            => _buffer.Dequeue(timeout, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Dequeue(CancellationToken cancellationToken = default)
            => _buffer.Dequeue(cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DequeueRange(Span<T> destination) => _buffer.DequeueRange(destination);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DrainTo(Span<T> destination) => _buffer.DrainTo(destination);
    }

    private const int StateActive = 0;
    private const int StateDisposed = 1;

    private static readonly Error s_bufferFull = Error.Failure("RING_BUFFER_FULL", "The ring buffer is full.");
    private static readonly Error s_bufferEmpty = Error.Failure("RING_BUFFER_EMPTY", "The ring buffer is empty.");
    private static readonly Error s_bufferTimeout = Error.Timeout("RING_BUFFER_TIMEOUT", "The ring buffer operation timed out.");

    private readonly Slot[] _slots;
    private readonly int _capacity;
    private readonly long _mask;
    private readonly bool _autoClearOnDispose;

    private readonly ProducerEndpoint _producerEndpoint;
    private readonly ConsumerEndpoint _consumerEndpoint;

    private PaddedRingPosition _positions;
    private int _state;

    public MpmcRingBuffer(int capacity)
        : this(new RingBufferOptions { Capacity = capacity })
    {
    }

    public MpmcRingBuffer(RingBufferOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Capacity < 2)
        {
            ThrowCapacityOutOfRange(options.Capacity);
        }

        int actualCapacity = (int)BitOperations.RoundUpToPowerOf2((uint)options.Capacity);
        if (actualCapacity < 2)
        {
            ThrowCapacityTooLarge(options.Capacity);
        }

        _capacity = actualCapacity;
        _mask = actualCapacity - 1;
        _autoClearOnDispose = options.AutoClearOnDispose;

        _slots = new Slot[actualCapacity];
        for (int i = 0; i < actualCapacity; i++)
        {
            _slots[i].Sequence = i;
        }

        _positions.EnqueuePosition = 0;
        _positions.DequeuePosition = 0;
        _state = StateActive;

        _producerEndpoint = new ProducerEndpoint(this);
        _consumerEndpoint = new ConsumerEndpoint(this);
    }

    public int Capacity => _capacity;

    public int Count
    {
        get
        {
            TBackoff.Initialize(out int backoffState);
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                long headBefore = Volatile.Read(ref _positions.DequeuePosition);
                long tail = Volatile.Read(ref _positions.EnqueuePosition);
                long headAfter = Volatile.Read(ref _positions.DequeuePosition);

                if (headBefore == headAfter)
                {
                    long diff = tail - headBefore;
                    if (diff < 0) return 0;
                    if (diff > _capacity) return _capacity;
                    return (int)diff;
                }

                TBackoff.Advance(ref backoffState);
            }

            long finalHead = Volatile.Read(ref _positions.DequeuePosition);
            long finalTail = Volatile.Read(ref _positions.EnqueuePosition);
            long bestEffort = finalTail - finalHead;
            if (bestEffort < 0) return 0;
            if (bestEffort > _capacity) return _capacity;
            return (int)bestEffort;
        }
    }

    public bool IsEmpty => Count == 0;
    public bool IsFull => Count >= _capacity;
    public bool IsDisposed => Volatile.Read(ref _state) != StateActive;
    public IProducer<T> Producer => _producerEndpoint;
    public IConsumer<T> Consumer => _consumerEndpoint;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(in T item)
    {
        ThrowIfDisposed();

        Slot[] slots = _slots;
        long mask = _mask;
        long pos = Volatile.Read(ref _positions.EnqueuePosition);

        while (true)
        {
            ref Slot slot = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slots), (nint)(pos & mask));
            long seq = Volatile.Read(ref slot.Sequence);
            long diff = seq - pos;

            if (diff == 0)
            {
                long actualPos = Interlocked.CompareExchange(ref _positions.EnqueuePosition, pos + 1, pos);
                if (actualPos == pos)
                {
                    slot.Value = item;
                    Volatile.Write(ref slot.Sequence, pos + 1);
                    return true;
                }
                pos = actualPos;
            }
            else if (diff < 0)
            {
                return false;
            }
            else
            {
                pos = Volatile.Read(ref _positions.EnqueuePosition);
            }
        }
    }

    public Result Enqueue(in T item, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (TryEnqueue(item)) return Result.Success();
        if (timeout == TimeSpan.Zero) return Result.Failure(s_bufferFull);

        long startTimestamp = Stopwatch.GetTimestamp();
        TBackoff.Initialize(out int backoffState);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (timeout != Timeout.InfiniteTimeSpan && Stopwatch.GetElapsedTime(startTimestamp) >= timeout)
                return Result.Failure(s_bufferTimeout);

            TBackoff.Advance(ref backoffState);
            if (TryEnqueue(item)) return Result.Success();
        }
    }

    public void Enqueue(in T item, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (TryEnqueue(item)) return;

        TBackoff.Initialize(out int backoffState);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            TBackoff.Advance(ref backoffState);
            if (TryEnqueue(item)) return;
        }
    }

    public int EnqueueRange(ReadOnlySpan<T> source)
    {
        ThrowIfDisposed();
        int written = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (!TryEnqueue(source[i])) break;
            written++;
        }
        return written;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        ThrowIfDisposed();
        return TryDequeueInternal(out item);
    }

    public Result<T> Dequeue(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (TryDequeue(out T? immediateItem))
            return Result<T>.Success(immediateItem);

        if (timeout == TimeSpan.Zero)
            return Result<T>.Failure(s_bufferEmpty);

        long startTimestamp = Stopwatch.GetTimestamp();
        TBackoff.Initialize(out int backoffState);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (timeout != Timeout.InfiniteTimeSpan && Stopwatch.GetElapsedTime(startTimestamp) >= timeout)
                return Result<T>.Failure(s_bufferTimeout);

            TBackoff.Advance(ref backoffState);
            if (TryDequeue(out T? item))
                return Result<T>.Success(item);
        }
    }

    public T Dequeue(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (TryDequeue(out T? immediateItem)) return immediateItem;

        TBackoff.Initialize(out int backoffState);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            TBackoff.Advance(ref backoffState);
            if (TryDequeue(out T? item)) return item;
        }
    }

    public int DequeueRange(Span<T> destination)
    {
        ThrowIfDisposed();
        int read = 0;
        for (int i = 0; i < destination.Length; i++)
        {
            if (!TryDequeue(out destination[i])) break;
            read++;
        }
        return read;
    }

    public int DrainTo(Span<T> destination)
    {
        ThrowIfDisposed();
        int drained = 0;
        while (drained < destination.Length && TryDequeue(out destination[drained]))
            drained++;
        return drained;
    }

    public void Clear()
    {
        ThrowIfDisposed();
        while (TryDequeueInternal(out _)) { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _state, StateDisposed) != StateActive) return;

        if (_autoClearOnDispose)
        {
            while (TryDequeueInternal(out _)) { }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryDequeueInternal([MaybeNullWhen(false)] out T item)
    {
        Slot[] slots = _slots;
        long mask = _mask;
        long pos = Volatile.Read(ref _positions.DequeuePosition);

        while (true)
        {
            ref Slot slot = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slots), (nint)(pos & mask));
            long seq = Volatile.Read(ref slot.Sequence);
            long diff = seq - (pos + 1);

            if (diff == 0)
            {
                long actualPos = Interlocked.CompareExchange(ref _positions.DequeuePosition, pos + 1, pos);
                if (actualPos == pos)
                {
                    item = slot.Value;
                    if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                        slot.Value = default!;
                    Volatile.Write(ref slot.Sequence, pos + mask + 1);
                    return true;
                }
                pos = actualPos;
            }
            else if (diff < 0)
            {
                item = default;
                return false;
            }
            else
            {
                pos = Volatile.Read(ref _positions.DequeuePosition);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _state) != StateActive) ThrowObjectDisposed();
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposed() =>
        throw new ObjectDisposedException(typeof(MpmcRingBuffer<T, TBackoff>).FullName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCapacityOutOfRange(int capacity) =>
        throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than or equal to 2.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCapacityTooLarge(int capacity) =>
        throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Requested capacity exceeds maximum allocatable power-of-two size.");
}

/// <summary>
/// Default production-ready ring buffer specializing with <see cref="AdaptiveSpinBackoff"/>.
/// Provides a simplified API surface without requiring a backoff type parameter.
/// </summary>
public sealed class MpmcRingBuffer<T> : IRingBuffer<T>
{
    private readonly MpmcRingBuffer<T, AdaptiveSpinBackoff> _inner;

    private sealed class ProducerEndpoint : IProducer<T>
    {
        private readonly MpmcRingBuffer<T, AdaptiveSpinBackoff> _inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProducerEndpoint(MpmcRingBuffer<T, AdaptiveSpinBackoff> inner) => _inner = inner;

        public int Capacity => _inner.Capacity;
        public int Count => _inner.Count;
        public bool IsFull => _inner.IsFull;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(in T item) => _inner.TryEnqueue(item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result Enqueue(in T item, TimeSpan timeout, CancellationToken cancellationToken = default)
            => _inner.Enqueue(item, timeout, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(in T item, CancellationToken cancellationToken = default)
            => _inner.Enqueue(item, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EnqueueRange(ReadOnlySpan<T> source) => _inner.EnqueueRange(source);
    }

    private sealed class ConsumerEndpoint : IConsumer<T>
    {
        private readonly MpmcRingBuffer<T, AdaptiveSpinBackoff> _inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConsumerEndpoint(MpmcRingBuffer<T, AdaptiveSpinBackoff> inner) => _inner = inner;

        public int Capacity => _inner.Capacity;
        public int Count => _inner.Count;
        public bool IsEmpty => _inner.IsEmpty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue([MaybeNullWhen(false)] out T item) => _inner.TryDequeue(out item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T> Dequeue(TimeSpan timeout, CancellationToken cancellationToken = default)
            => _inner.Dequeue(timeout, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Dequeue(CancellationToken cancellationToken = default)
            => _inner.Dequeue(cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DequeueRange(Span<T> destination) => _inner.DequeueRange(destination);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DrainTo(Span<T> destination) => _inner.DrainTo(destination);
    }

    private readonly ProducerEndpoint _producerEndpoint;
    private readonly ConsumerEndpoint _consumerEndpoint;

    public MpmcRingBuffer(int capacity)
    {
        _inner = new MpmcRingBuffer<T, AdaptiveSpinBackoff>(capacity);
        _producerEndpoint = new ProducerEndpoint(_inner);
        _consumerEndpoint = new ConsumerEndpoint(_inner);
    }

    public MpmcRingBuffer(RingBufferOptions options)
    {
        _inner = new MpmcRingBuffer<T, AdaptiveSpinBackoff>(options);
        _producerEndpoint = new ProducerEndpoint(_inner);
        _consumerEndpoint = new ConsumerEndpoint(_inner);
    }

    public int Capacity => _inner.Capacity;
    public int Count => _inner.Count;
    public bool IsEmpty => _inner.IsEmpty;
    public bool IsFull => _inner.IsFull;
    public bool IsDisposed => _inner.IsDisposed;
    public IProducer<T> Producer => _producerEndpoint;
    public IConsumer<T> Consumer => _consumerEndpoint;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(in T item) => _inner.TryEnqueue(item);

    public Result Enqueue(in T item, TimeSpan timeout, CancellationToken cancellationToken = default)
        => _inner.Enqueue(item, timeout, cancellationToken);

    public void Enqueue(in T item, CancellationToken cancellationToken = default)
        => _inner.Enqueue(item, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EnqueueRange(ReadOnlySpan<T> source) => _inner.EnqueueRange(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue([MaybeNullWhen(false)] out T item) => _inner.TryDequeue(out item);

    public Result<T> Dequeue(TimeSpan timeout, CancellationToken cancellationToken = default)
        => _inner.Dequeue(timeout, cancellationToken);

    public T Dequeue(CancellationToken cancellationToken = default)
        => _inner.Dequeue(cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DequeueRange(Span<T> destination) => _inner.DequeueRange(destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DrainTo(Span<T> destination) => _inner.DrainTo(destination);

    public void Clear() => _inner.Clear();
    public void Dispose() => _inner.Dispose();
}
