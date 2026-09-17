namespace Axrone.Memory.Arena;

public sealed class ArenaScope<T, TBackoff> : IDisposable
    where T : unmanaged
    where TBackoff : struct, IBackoffPolicy
{
    private readonly ArenaMemoryRing<T, TBackoff> _ring;
    private long _checkpointWriteReserved;
    private long _checkpointWriteCommitted;
    private long _checkpointReadReserved;
    private long _checkpointReadCommitted;
    private int _isDisposed;

    public ArenaScope(ArenaMemoryRing<T, TBackoff> ring)
    {
        if (ring is null) ThrowHelper.ThrowArgumentNullException(nameof(ring));
        _ring = ring;
        TakeCheckpoint();
    }

    public ArenaMemoryRing<T, TBackoff> Ring => _ring;

    public bool TryWrite(ReadOnlySpan<T> source) => _ring.TryWrite(source);

    public bool TryRead(Span<T> destination) => _ring.TryRead(destination);

    public int ReserveWriteBatch(int requestedCount, out Span<T> reservedSpan) =>
        _ring.ReserveWriteBatch(requestedCount, out reservedSpan);

    public int ReserveReadBatch(int requestedCount, out Span<T> reservedSpan) =>
        _ring.ReserveReadBatch(requestedCount, out reservedSpan);

    public void CommitWrite(int count) => _ring.CommitWrite(count);

    public void CommitRead(int count) => _ring.CommitRead(count);

    public void TakeCheckpoint()
    {
        ThrowIfDisposed();
        _checkpointWriteReserved = _ring.CommittedWriteSequence;
        _checkpointWriteCommitted = _ring.CommittedWriteSequence;
        _checkpointReadReserved = _ring.CommittedReadSequence;
        _checkpointReadCommitted = _ring.CommittedReadSequence;
    }

    public void Rewind()
    {
        ThrowIfDisposed();
        TakeCheckpoint();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(ArenaScope<T, TBackoff>));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
        }
    }
}
