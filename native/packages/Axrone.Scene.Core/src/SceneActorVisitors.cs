namespace Axrone.Scene;

/// <summary>
/// Callback invoked for every live actor. The visitor is a generic type
/// parameter, so iteration closes over the callback type rather than a
/// delegate instance.
/// </summary>
/// <typeparam name="TContext">Traversal state; may be a <see langword="ref"/> struct.</typeparam>
public interface ISceneActorVisitor<TContext>
    where TContext : allows ref struct
{
    /// <summary>Visits one live actor.</summary>
    /// <returns><see langword="true"/> to continue; <see langword="false"/> to stop early.</returns>
    bool Visit(SceneActor actor, scoped ref TContext context);
}

/// <summary>Allocation-free enumerable over live actors in slot order.</summary>
public readonly ref struct SceneActorEnumerable
{
    private readonly SceneActorTable _actors;

    internal SceneActorEnumerable(SceneActorTable actors)
    {
        _actors = actors;
    }

    /// <summary>Creates an actor enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SceneActorEnumerator GetEnumerator() => new(_actors);
}

/// <summary>Allocation-free enumerator over live actors in slot order.</summary>
public ref struct SceneActorEnumerator
{
    private readonly SceneActorTable _actors;
    private int _index;
    private SceneActor _current;

    internal SceneActorEnumerator(SceneActorTable actors)
    {
        _actors = actors;
        _index = -1;
        _current = SceneActor.Null;
    }

    /// <summary>Current live actor.</summary>
    public SceneActor Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    /// <summary>Advances to the next live actor.</summary>
    public bool MoveNext()
    {
        while (++_index < _actors.Capacity)
        {
            ref SceneActorRecord record = ref _actors.RecordAt(_index);
            if ((record.Meta.Flags & SceneActorFlags.IsFree) == 0)
            {
                _current = SceneActor.FromEntity(record.Entity);
                return true;
            }
        }

        _current = SceneActor.Null;
        return false;
    }
}
