namespace Axrone.Ecs;

/// <summary>
/// Builder typestate marker: draft stage.
/// </summary>
public readonly struct DraftState;

/// <summary>
/// Builder typestate marker: committed stage.
/// </summary>
public readonly struct CommittedState;

/// <summary>
/// Typestate entity transaction: component calls stay available until
/// commit, which invalidates the handle.
/// </summary>
/// <typeparam name="TState">Current stage.</typeparam>
public ref struct EntityTransaction<TState> where TState : struct
{
    private readonly World _world;
    private Entity _entity;

    /// <summary>Opens a transaction.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal EntityTransaction(World world, scoped in Entity entity)
    {
        _world = world;
        _entity = entity;
    }

    /// <summary>Adds a component, staying in draft.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityTransaction<DraftState> With<TComponent>(scoped in TComponent component) where TComponent : unmanaged
    {
        _world.AddComponent(_entity, component);
        return new EntityTransaction<DraftState>(_world, _entity);
    }

    /// <summary>Commits and returns the entity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity Commit()
    {
        Entity ret = _entity;
        _entity = Entity.Null;
        return ret;
    }
}
