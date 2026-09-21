namespace Axrone.Memory.Arena;

public ref struct LinearArenaScope<T> where T : unmanaged
{
    private readonly LinearArena<T> _arena;
    private readonly ArenaMarker _marker;
    private int _committed;

    internal LinearArenaScope(LinearArena<T> arena, ArenaMarker marker)
    {
        _arena = arena;
        _marker = marker;
        _committed = 0;
    }

    public Span<T> Allocate(nuint elementCount) => _arena.Allocate(elementCount);

    public Span<T> AllocateZeroed(nuint elementCount) => _arena.AllocateZeroed(elementCount);

    public void Commit()
    {
        _committed = 1;
    }

    public void Dispose()
    {
        if (_committed == 0)
        {
            _arena.RewindTo(_marker);
        }
    }
}
