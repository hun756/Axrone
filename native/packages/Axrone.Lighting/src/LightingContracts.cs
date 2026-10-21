namespace Axrone.Lighting;

/// <summary>Zero-alloc light visitor contract.</summary>
/// <typeparam name="TState">Visitor state.</typeparam>
public interface ILightVisitor<TState>
    where TState : allows ref struct
{
    /// <summary>Visits a light.</summary>
    void Visit(ref readonly LightData light, scoped ref TState state);
}

/// <summary>Zero-alloc light predicate contract.</summary>
/// <typeparam name="TContext">Predicate context.</typeparam>
public interface ILightPredicate<TContext>
    where TContext : allows ref struct
{
    /// <summary>Tests a light.</summary>
    bool Matches(ref readonly LightData light, scoped ref readonly TContext context);
}

/// <summary>Light source read contract.</summary>
public interface ILightSourceReader
{
    /// <summary>Rig identity.</summary>
    RigId Id { get; }

    /// <summary>Rig version.</summary>
    RigVersion Version { get; }

    /// <summary>Environment.</summary>
    LightingEnvironment Environment { get; }

    /// <summary>Light count.</summary>
    int Count { get; }

    /// <summary>Whether a light exists.</summary>
    bool Contains(LightId id);

    /// <summary>Tries to get a light.</summary>
    bool TryGet(LightId id, out LightData light);
}

/// <summary>Light source write contract.</summary>
public interface ILightSourceWriter
{
    /// <summary>Adds a light.</summary>
    void Add(in LightData light);

    /// <summary>Removes a light.</summary>
    bool Remove(LightId id);

    /// <summary>Updates a light.</summary>
    void Update(in LightData light);

    /// <summary>Clears all lights.</summary>
    void Clear();

    /// <summary>Sets the environment.</summary>
    void SetEnvironment(in LightingEnvironment environment);
}

/// <summary>Zero-allocation duck-typed light span enumerator.</summary>
public ref struct LightSpanEnumerator
{
    private readonly ReadOnlySpan<LightData> _span;
    private readonly int _length;
    private int _index;

    /// <summary>Creates an enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LightSpanEnumerator(ReadOnlySpan<LightData> span)
    {
        _span = span;
        _length = span.Length;
        _index = -1;
    }

    /// <summary>Advances to the next element.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        int next = _index + 1;
        if ((uint)next < (uint)_length)
        {
            _index = next;
            return true;
        }

        return false;
    }

    /// <summary>Current element.</summary>
    public ref readonly LightData Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _span[_index];
    }

    /// <summary>Returns this enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LightSpanEnumerator GetEnumerator() => this;
}
