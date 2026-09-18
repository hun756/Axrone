namespace Axrone.Memory.Arena;

public readonly ref struct BatchReadReservation<T> where T : unmanaged
{
    private readonly IArenaCommitCoordinator<T> _coordinator;
    private readonly ulong _sequence;
    private readonly int _count;
    private readonly ref bool _committedRef;

    public ReadOnlySpan<T> FirstSegment { get; }
    public ReadOnlySpan<T> SecondSegment { get; }
    public int TotalReserved => _count;
    public bool IsCommitted => _committedRef;

    internal BatchReadReservation(
        IArenaCommitCoordinator<T> coordinator,
        ulong sequence,
        int count,
        ReadOnlySpan<T> firstSegment,
        ReadOnlySpan<T> secondSegment,
        ref bool committedFlag)
    {
        _coordinator = coordinator;
        _sequence = sequence;
        _count = count;
        FirstSegment = firstSegment;
        SecondSegment = secondSegment;
        _committedRef = ref committedFlag;
        _committedRef = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Commit()
    {
        if (_committedRef)
        {
            ThrowHelper.ThrowDoubleCommit();
        }
        _coordinator.CommitRead(_sequence, _count);
        _committedRef = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (!_committedRef)
        {
            _coordinator.AbandonRead(_sequence, _count);
            _committedRef = true;
        }
    }
}
