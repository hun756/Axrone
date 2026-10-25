namespace Axrone.Scene;

/// <summary>
/// Owns interned scene names: symbol creation, reverse lookup and collision
/// detection. Names are interned once during composition; hot paths compare
/// the returned <see cref="SceneStringId"/> values directly.
/// </summary>
/// <remarks>
/// The table is thread-safe for registration. Enumeration returns a snapshot
/// so a consumer cannot observe a dictionary during a concurrent intern.
/// </remarks>
public sealed class SceneNameTable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SceneStringId, string> _byId = new();
    private readonly Dictionary<string, SceneStringId> _byName = new(StringComparer.Ordinal);

    /// <summary>Number of interned names.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _byId.Count;
            }
        }
    }

    /// <summary>Interns a UTF-16 name and returns its stable symbol.</summary>
    /// <exception cref="SceneValidationException">
    /// The name is empty or its hash collides with a different interned name.
    /// </exception>
    public SceneStringId Intern(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty)
        {
            throw new SceneValidationException(
                SceneErrorCodes.ValidationInvalid,
                "Scene names must not be empty.");
        }

        lock (_gate)
        {
            SceneStringId id = SceneStringId.FromName(name);

            if (_byId.TryGetValue(id, out string? existing))
            {
                if (existing.AsSpan().SequenceEqual(name))
                {
                    return id;
                }

                SceneThrowHelper.ThrowNameCollision(new string(name), id);
            }

            string owned = new(name);
            _byId.Add(id, owned);
            _byName.Add(owned, id);
            return id;
        }
    }

    /// <summary>Interns a string name and returns its stable symbol.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SceneStringId Intern(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Intern(name.AsSpan());
    }

    /// <summary>Resolves a symbol to its original name.</summary>
    public bool TryResolve(SceneStringId id, [NotNullWhen(true)] out string? name)
    {
        lock (_gate)
        {
            return _byId.TryGetValue(id, out name);
        }
    }

    /// <summary>Resolves a symbol, throwing when it was never interned.</summary>
    public string Resolve(SceneStringId id)
    {
        if (TryResolve(id, out string? name))
        {
            return name;
        }

        throw new SceneValidationException(
            SceneErrorCodes.ValidationInvalid,
            $"Scene symbol {id} is not present in this name table.");
    }

    /// <summary>Looks up an already interned name.</summary>
    public bool TryGetId(string name, out SceneStringId id)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            return _byName.TryGetValue(name, out id);
        }
    }

    /// <summary>Returns a stable snapshot of the interned names.</summary>
    public string[] SnapshotNames()
    {
        lock (_gate)
        {
            string[] names = new string[_byId.Count];
            _byId.Values.CopyTo(names, 0);
            return names;
        }
    }
}
