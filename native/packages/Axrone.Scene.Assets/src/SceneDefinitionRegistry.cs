namespace Axrone.Scene.Assets;

/// <summary>Outcome of registering a definition, including the replaced entry.</summary>
/// <typeparam name="TDefinition">Definition type.</typeparam>
public readonly record struct SceneRegistrationResult<TDefinition>(TDefinition Definition, TDefinition? Previous)
    where TDefinition : class;

/// <summary>
/// Thread-safe definition registry for one asset kind. Registration is cold;
/// reads are serialized so callers never observe a partially replaced entry.
/// </summary>
/// <typeparam name="TTag">Phantom asset-kind tag.</typeparam>
/// <typeparam name="TDefinition">Definition type.</typeparam>
public sealed class SceneDefinitionRegistry<TTag, TDefinition>
    where TTag : struct
    where TDefinition : class, ISceneAssetDefinition<TTag>
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SceneAssetId<TTag>, TDefinition> _byId = new();
    private readonly List<TDefinition> _definitions = new();

    /// <summary>Number of registered definitions.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _definitions.Count;
            }
        }
    }

    /// <summary>Registers or replaces a definition.</summary>
    public SceneRegistrationResult<TDefinition> Register(TDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.Id.IsValid)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Asset definitions require a non-empty id.");
        }

        lock (_gate)
        {
            if (_byId.TryGetValue(definition.Id, out TDefinition? previous))
            {
                int index = _definitions.IndexOf(previous);
                if (index >= 0)
                {
                    _definitions[index] = definition;
                }

                _byId[definition.Id] = definition;
                return new SceneRegistrationResult<TDefinition>(definition, previous);
            }

            _byId.Add(definition.Id, definition);
            _definitions.Add(definition);
            return new SceneRegistrationResult<TDefinition>(definition, null);
        }
    }

    /// <summary>Tries to resolve a definition.</summary>
    public bool TryGet(SceneAssetId<TTag> id, [NotNullWhen(true)] out TDefinition? definition)
    {
        lock (_gate)
        {
            return _byId.TryGetValue(id, out definition);
        }
    }

    /// <summary>Resolves a definition, throwing when absent.</summary>
    public TDefinition Get(SceneAssetId<TTag> id)
    {
        if (TryGet(id, out TDefinition? definition))
        {
            return definition;
        }

        throw new SceneAssetNotFoundException<TTag>(id);
    }

    /// <summary>Whether a definition is registered.</summary>
    public bool Contains(SceneAssetId<TTag> id)
    {
        lock (_gate)
        {
            return _byId.ContainsKey(id);
        }
    }

    /// <summary>Removes a definition.</summary>
    public bool Remove(SceneAssetId<TTag> id, [NotNullWhen(true)] out TDefinition? definition)
    {
        lock (_gate)
        {
            if (!_byId.Remove(id, out definition))
            {
                definition = null;
                return false;
            }

            _definitions.Remove(definition);
            return true;
        }
    }

    /// <summary>Removes every definition and returns the removed entries.</summary>
    public TDefinition[] Clear()
    {
        lock (_gate)
        {
            TDefinition[] removed = [.. _definitions];
            _definitions.Clear();
            _byId.Clear();
            return removed;
        }
    }

    /// <summary>Returns a snapshot in registration order.</summary>
    public TDefinition[] Definitions()
    {
        lock (_gate)
        {
            return [.. _definitions];
        }
    }

    /// <summary>Returns a snapshot of registered ids in registration order.</summary>
    public SceneAssetId<TTag>[] Ids()
    {
        lock (_gate)
        {
            SceneAssetId<TTag>[] ids = new SceneAssetId<TTag>[_definitions.Count];
            for (int i = 0; i < _definitions.Count; i++)
            {
                ids[i] = _definitions[i].Id;
            }

            return ids;
        }
    }
}
