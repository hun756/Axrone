namespace Axrone.Memory.Arena;

public ref struct ArenaBufferWriter<T> where T : unmanaged
{
    private readonly IBatchReservable<T> _batch;
    private BatchWriteReservation<T> _reservation;
    private int _advanced;
    private bool _hasReservation;

    public ArenaBufferWriter(IBatchReservable<T> batch)
    {
        _batch = batch;
        Unsafe.SkipInit(out _reservation);
        _advanced = 0;
        _hasReservation = false;
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        EnsureReservation(sizeHint);
        return _reservation.FirstSegment;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        throw new NotSupportedException(
            "ArenaBufferWriter operates on native unmanaged memory; use GetSpan instead of GetMemory.");
    }

    public void Advance(int count)
    {
        if (!_hasReservation)
        {
            ThrowHelper.ThrowNoReservation();
        }

        if (count < 0 || count > _reservation.FirstSegment.Length)
        {
            ThrowHelper.ThrowInvalidAdvance();
        }

        _advanced = count;
    }

    public void Commit()
    {
        if (!_hasReservation)
        {
            ThrowHelper.ThrowNoReservation();
        }

        if (_advanced > 0)
        {
            _reservation.Commit();
        }

        _hasReservation = false;
    }

    public void Dispose()
    {
        if (_hasReservation)
        {
            _reservation.Dispose();
            _hasReservation = false;
        }
    }

    private void EnsureReservation(int sizeHint)
    {
        if (_hasReservation) return;

        int requested = sizeHint > 0 ? sizeHint : 1;

        if (!_batch.TryReserveWrite(requested, out _reservation))
        {
            ThrowHelper.ThrowInsufficientMemory("Ring buffer has no available space for the requested write.");
        }

        _hasReservation = true;
        _advanced = 0;
    }
}
