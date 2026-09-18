namespace Axrone.Memory.Arena;

public ref struct BatchReadReservation<T> where T : unmanaged
{
    private readonly IArenaCommitCoordinator<T> _coordinator;
    private readonly ulong _sequence;
    private readonly int _count;
    private int _committed;

    public ReadOnlySpan<T> FirstSegment { get; }
    public ReadOnlySpan<T> SecondSegment { get; }
    public int TotalReserved => _count;
    public bool IsCommitted => _committed != 0;

    internal BatchReadReservation(
        IArenaCommitCoordinator<T> coordinator,
        ulong sequence,
        int count,
        ReadOnlySpan<T> firstSegment,
        ReadOnlySpan<T> secondSegment)
    {
        _coordinator = coordinator;
        _sequence = sequence;
        _count = count;
        FirstSegment = firstSegment;
        SecondSegment = secondSegment;
        _committed = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Commit()
    {
        if (_committed != 0)
        {
            ThrowHelper.ThrowDoubleCommit();
        }
        _coordinator.CommitRead(_sequence, _count);
        _committed = 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_committed == 0)
        {
            _coordinator.AbandonRead(_sequence, _count);
            _committed = 1;
        }
    }
}
