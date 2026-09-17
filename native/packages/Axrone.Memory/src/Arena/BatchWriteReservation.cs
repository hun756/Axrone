namespace Axrone.Memory.Arena;

public ref struct BatchWriteReservation<T> where T : unmanaged
{
    private readonly ArenaMemoryRing<T, DefaultNoOpBackoff> _ring;
    private readonly int _reservedCount;
    private Span<T> _span;
    private int _committedCount;

    internal BatchWriteReservation(ArenaMemoryRing<T, DefaultNoOpBackoff> ring, Span<T> span, int reservedCount)
    {
        _ring = ring;
        _span = span;
        _reservedCount = reservedCount;
        _committedCount = 0;
    }

    public Span<T> Span => _span;

    public int ReservedCount => _reservedCount;

    public bool IsEmpty => _reservedCount == 0;

    public void Commit(int count)
    {
        if (_committedCount != 0) ThrowHelper.ThrowInvalidOperationException("Batch write reservation already committed.");
        if ((uint)count > (uint)_reservedCount) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));

        _committedCount = count;
        _ring.CommitWrite(count);
    }

    public void CommitAll()
    {
        Commit(_reservedCount);
    }
}

public readonly struct DefaultNoOpBackoff : IBackoffPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnSpin(int spinCount)
    {
        if (spinCount < 10)
        {
            Thread.SpinWait(1 << spinCount);
        }
        else
        {
            Thread.Yield();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() { }
}
