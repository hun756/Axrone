namespace Axrone.Ecs;

using Axrone.Collections;

/// <summary>
/// Entity-aware job policy over one component.
/// Monomorphized, no vtable.
/// </summary>
/// <typeparam name="T1">Component type.</typeparam>
public interface IJobPolicy<T1> where T1 : unmanaged
{
    /// <summary>Executes over one row.</summary>
    static abstract void Execute(Entity entity, ref T1 c1);
}

/// <summary>
/// Entity-aware job policy over two components.
/// Monomorphized, no vtable.
/// </summary>
/// <typeparam name="T1">First component type.</typeparam>
/// <typeparam name="T2">Second component type.</typeparam>
public interface IJobPolicy<T1, T2> where T1 : unmanaged where T2 : unmanaged
{
    /// <summary>Executes over one row.</summary>
    static abstract void Execute(Entity entity, ref T1 c1, ref T2 c2);
}

/// <summary>
/// Entity visitor with stack-only context.
/// Monomorphized, no vtable.
/// </summary>
/// <typeparam name="TContext">Context type, may be a ref struct.</typeparam>
public interface IEntityVisitor<TContext> where TContext : allows ref struct
{
    /// <summary>Visits one entity.</summary>
    static abstract void Visit(Entity entity, ref TContext context);
}

/// <summary>
/// One-component chunk: entities plus the column slice.
/// </summary>
/// <typeparam name="T1">Component type.</typeparam>
public readonly ref struct Chunk1<T1> where T1 : unmanaged
{
    /// <summary>Chunk entities.</summary>
    public readonly ReadOnlySpan<Entity> Entities;

    /// <summary>Component slice.</summary>
    public readonly Span<T1> Column1;

    /// <summary>Creates a chunk.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk1(ReadOnlySpan<Entity> entities, Span<T1> c1)
    {
        Entities = entities;
        Column1 = c1;
    }

    /// <summary>Row count.</summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Entities.Length;
    }
}

/// <summary>
/// Two-component chunk.
/// </summary>
/// <typeparam name="T1">First component type.</typeparam>
/// <typeparam name="T2">Second component type.</typeparam>
public readonly ref struct Chunk2<T1, T2> where T1 : unmanaged where T2 : unmanaged
{
    /// <summary>Chunk entities.</summary>
    public readonly ReadOnlySpan<Entity> Entities;

    /// <summary>First slice.</summary>
    public readonly Span<T1> Column1;

    /// <summary>Second slice.</summary>
    public readonly Span<T2> Column2;

    /// <summary>Creates a chunk.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk2(ReadOnlySpan<Entity> entities, Span<T1> c1, Span<T2> c2)
    {
        Entities = entities;
        Column1 = c1;
        Column2 = c2;
    }

    /// <summary>Row count.</summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Entities.Length;
    }
}

/// <summary>
/// Single-component query with duck-typed enumeration over matching chunks.
/// </summary>
/// <typeparam name="T1">Component type.</typeparam>
public readonly ref struct Query1<T1> where T1 : unmanaged
{
    private readonly World _world;
    private readonly ComponentMask _mask;

    /// <summary>Creates a query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query1(World world)
    {
        _world = world;
        _mask = ComponentType<T1>.Mask;
    }

    /// <summary>Enumerates matching chunks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_world, _mask);

    /// <summary>Duck-typed chunk enumerator.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<Archetype> _archetypes;
        private readonly ComponentMask _mask;
        private int _index;
        private Chunk1<T1> _current;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator(World world, ComponentMask mask)
        {
            ArgumentNullException.ThrowIfNull(world);
            _archetypes = world.Archetypes.AsSpan();
            _mask = mask;
            _index = -1;
            _current = default;
        }

        /// <summary>Advances to the next non-empty matching chunk.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (++_index < _archetypes.Length)
            {
                Archetype arch = _archetypes[_index];
                if (arch.Count > 0 && arch.Mask.ContainsAll(_mask))
                {
                    _current = new Chunk1<T1>(arch.Entities, arch.GetColumn<T1>()!.AsSpan());
                    return true;
                }
            }

            return false;
        }

        /// <summary>Current chunk.</summary>
        public readonly Chunk1<T1> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }
    }
}

/// <summary>
/// Two-component query.
/// </summary>
/// <typeparam name="T1">First component type.</typeparam>
/// <typeparam name="T2">Second component type.</typeparam>
public readonly ref struct Query2<T1, T2> where T1 : unmanaged where T2 : unmanaged
{
    private readonly World _world;
    private readonly ComponentMask _mask;

    /// <summary>Creates a query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query2(World world)
    {
        _world = world;
        _mask = ComponentType<T1>.Mask | ComponentType<T2>.Mask;
    }

    /// <summary>Enumerates matching chunks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_world, _mask);

    /// <summary>Duck-typed chunk enumerator.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<Archetype> _archetypes;
        private readonly ComponentMask _mask;
        private int _index;
        private Chunk2<T1, T2> _current;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator(World world, ComponentMask mask)
        {
            ArgumentNullException.ThrowIfNull(world);
            _archetypes = world.Archetypes.AsSpan();
            _mask = mask;
            _index = -1;
            _current = default;
        }

        /// <summary>Advances to the next non-empty matching chunk.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (++_index < _archetypes.Length)
            {
                Archetype arch = _archetypes[_index];
                if (arch.Count > 0 && arch.Mask.ContainsAll(_mask))
                {
                    _current = new Chunk2<T1, T2>(
                        arch.Entities,
                        arch.GetColumn<T1>()!.AsSpan(),
                        arch.GetColumn<T2>()!.AsSpan());
                    return true;
                }
            }

            return false;
        }

        /// <summary>Current chunk.</summary>
        public readonly Chunk2<T1, T2> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }
    }
}
