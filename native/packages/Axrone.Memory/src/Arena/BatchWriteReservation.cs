namespace Axrone.Memory.Arena;

public ref struct BatchWriteReservation<T> where T : unmanaged
{
    private readonly IArenaCommitCoordinator<T> _coordinator;
    private readonly ulong _sequence;
    private readonly int _count;
    private int _committed;

    public Span<T> FirstSegment { get; }
    public Span<T> SecondSegment { get; }
    public int TotalReserved => _count;
    public bool IsCommitted => _committed != 0;

    internal BatchWriteReservation(
        IArenaCommitCoordinator<T> coordinator,
        ulong sequence,
        int count,
        Span<T> firstSegment,
        Span<T> secondSegment)
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
        _coordinator.CommitWrite(_sequence, _count);
        _committed = 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_committed == 0)
        {
            _coordinator.AbandonWrite(_sequence, _count);
            _committed = 1;
        }
    }
}
