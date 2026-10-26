namespace Axrone.Scene;

/// <summary>
/// Groups a sequence of structural mutations. The scope is a <see langword="ref"/>
/// struct, so it cannot escape the calling frame or be boxed. It does not
/// bypass the iteration guard: structural changes during traversal remain
/// forbidden.
/// </summary>
public ref struct SceneStructuralScope : IDisposable
{
    private SceneWorld? _world;

    internal SceneStructuralScope(SceneWorld world)
    {
        _world = world;
    }

    /// <summary>Whether the scope still owns its world lifetime.</summary>
    public bool IsActive
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _world is not null;
    }

    /// <summary>Closes the structural batch.</summary>
    public void Dispose()
    {
        SceneWorld? world = _world;
        _world = null;
        world?.EndStructuralBatch();
    }
}
