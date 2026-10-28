namespace Axrone.Ecs;

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
/// Entity-aware job policy over three components.
/// Monomorphized, no vtable.
/// </summary>
/// <typeparam name="T1">First component type.</typeparam>
/// <typeparam name="T2">Second component type.</typeparam>
/// <typeparam name="T3">Third component type.</typeparam>
public interface IJobPolicy<T1, T2, T3>
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
{
    /// <summary>Executes over one row.</summary>
    static abstract void Execute(Entity entity, ref T1 c1, ref T2 c2, ref T3 c3);
}

/// <summary>
/// Entity-aware job policy over four components.
/// Monomorphized, no vtable.
/// </summary>
/// <typeparam name="T1">First component type.</typeparam>
/// <typeparam name="T2">Second component type.</typeparam>
/// <typeparam name="T3">Third component type.</typeparam>
/// <typeparam name="T4">Fourth component type.</typeparam>
public interface IJobPolicy<T1, T2, T3, T4>
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
    where T4 : unmanaged
{
    /// <summary>Executes over one row.</summary>
    static abstract void Execute(Entity entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4);
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
/// Three-component chunk.
/// </summary>
/// <typeparam name="T1">First component type.</typeparam>
/// <typeparam name="T2">Second component type.</typeparam>
/// <typeparam name="T3">Third component type.</typeparam>
public readonly ref struct Chunk3<T1, T2, T3>
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
{
    /// <summary>Chunk entities.</summary>
    public readonly ReadOnlySpan<Entity> Entities;

    /// <summary>First slice.</summary>
    public readonly Span<T1> Column1;

    /// <summary>Second slice.</summary>
    public readonly Span<T2> Column2;

    /// <summary>Third slice.</summary>
    public readonly Span<T3> Column3;

    /// <summary>Creates a chunk.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk3(ReadOnlySpan<Entity> entities, Span<T1> c1, Span<T2> c2, Span<T3> c3)
    {
        Entities = entities;
        Column1 = c1;
        Column2 = c2;
        Column3 = c3;
    }

    /// <summary>Row count.</summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Entities.Length;
    }
}

/// <summary>
/// Four-component chunk.
/// </summary>
/// <typeparam name="T1">First component type.</typeparam>
/// <typeparam name="T2">Second component type.</typeparam>
/// <typeparam name="T3">Third component type.</typeparam>
/// <typeparam name="T4">Fourth component type.</typeparam>
public readonly ref struct Chunk4<T1, T2, T3, T4>
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
    where T4 : unmanaged
{
    /// <summary>Chunk entities.</summary>
    public readonly ReadOnlySpan<Entity> Entities;

    /// <summary>First slice.</summary>
    public readonly Span<T1> Column1;

    /// <summary>Second slice.</summary>
    public readonly Span<T2> Column2;

    /// <summary>Third slice.</summary>
    public readonly Span<T3> Column3;

    /// <summary>Fourth slice.</summary>
    public readonly Span<T4> Column4;

    /// <summary>Creates a chunk.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk4(ReadOnlySpan<Entity> entities, Span<T1> c1, Span<T2> c2, Span<T3> c3, Span<T4> c4)
    {
        Entities = entities;
        Column1 = c1;
        Column2 = c2;
        Column3 = c3;
        Column4 = c4;
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
    private readonly ComponentMask _all;
    private readonly ComponentMask _none;
    private readonly int[] _matches;

    /// <summary>Creates a query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query1(World world)
        : this(world, ComponentType<T1>.Mask, ComponentMask.Empty)
    {
    }

    internal Query1(World world, ComponentMask all, ComponentMask none)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        _all = all;
        _none = none;
        _matches = world.QueryCache.Resolve(all, none, world.Archetypes);
    }

    /// <summary>Requires an additional component.</summary>
    /// <typeparam name="T2">Required component type.</typeparam>
    public Query1<T1> With<T2>() where T2 : unmanaged =>
        new(_world, _all | ComponentType<T2>.Mask, _none);

    /// <summary>Excludes a component.</summary>
    /// <typeparam name="T2">Excluded component type.</typeparam>
    public Query1<T1> Without<T2>() where T2 : unmanaged =>
        new(_world, _all, _none | ComponentType<T2>.Mask);

    /// <summary>Enumerates matching chunks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_world, _matches);

    /// <summary>Duck-typed chunk enumerator.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<Archetype> _archetypes;
        private readonly ReadOnlySpan<int> _matches;
        private int _index;
        private Chunk1<T1> _current;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(World world, int[] matches)
        {
            ArgumentNullException.ThrowIfNull(world);
            _archetypes = world.Archetypes.AsSpan();
            _matches = matches;
            _index = -1;
            _current = default;
        }

        /// <summary>Advances to the next non-empty matching chunk.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (++_index < _matches.Length)
            {
                Archetype arch = _archetypes[_matches[_index]];
                if (arch.Count > 0)
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
    private readonly ComponentMask _all;
    private readonly ComponentMask _none;
    private readonly int[] _matches;

    /// <summary>Creates a query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query2(World world)
        : this(world, ComponentType<T1>.Mask | ComponentType<T2>.Mask, ComponentMask.Empty)
    {
    }

    internal Query2(World world, ComponentMask all, ComponentMask none)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        _all = all;
        _none = none;
        _matches = world.QueryCache.Resolve(all, none, world.Archetypes);
    }

    /// <summary>Requires an additional component.</summary>
    /// <typeparam name="T3">Required component type.</typeparam>
    public Query2<T1, T2> With<T3>() where T3 : unmanaged =>
        new(_world, _all | ComponentType<T3>.Mask, _none);

    /// <summary>Excludes a component.</summary>
    /// <typeparam name="T3">Excluded component type.</typeparam>
    public Query2<T1, T2> Without<T3>() where T3 : unmanaged =>
        new(_world, _all, _none | ComponentType<T3>.Mask);

    /// <summary>Enumerates matching chunks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_world, _matches);

    /// <summary>Duck-typed chunk enumerator.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<Archetype> _archetypes;
        private readonly ReadOnlySpan<int> _matches;
        private int _index;
        private Chunk2<T1, T2> _current;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(World world, int[] matches)
        {
            ArgumentNullException.ThrowIfNull(world);
            _archetypes = world.Archetypes.AsSpan();
            _matches = matches;
            _index = -1;
            _current = default;
        }

        /// <summary>Advances to the next non-empty matching chunk.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (++_index < _matches.Length)
            {
                Archetype arch = _archetypes[_matches[_index]];
                if (arch.Count > 0)
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

/// <summary>
/// Three-component query.
/// </summary>
/// <typeparam name="T1">First component type.</typeparam>
/// <typeparam name="T2">Second component type.</typeparam>
/// <typeparam name="T3">Third component type.</typeparam>
public readonly ref struct Query3<T1, T2, T3>
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
{
    private readonly World _world;
    private readonly ComponentMask _all;
    private readonly ComponentMask _none;
    private readonly int[] _matches;

    /// <summary>Creates a query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query3(World world)
        : this(
            world,
            ComponentType<T1>.Mask | ComponentType<T2>.Mask | ComponentType<T3>.Mask,
            ComponentMask.Empty)
    {
    }

    internal Query3(World world, ComponentMask all, ComponentMask none)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        _all = all;
        _none = none;
        _matches = world.QueryCache.Resolve(all, none, world.Archetypes);
    }

    /// <summary>Requires an additional component.</summary>
    /// <typeparam name="T4">Required component type.</typeparam>
    public Query3<T1, T2, T3> With<T4>() where T4 : unmanaged =>
        new(_world, _all | ComponentType<T4>.Mask, _none);

    /// <summary>Excludes a component.</summary>
    /// <typeparam name="T4">Excluded component type.</typeparam>
    public Query3<T1, T2, T3> Without<T4>() where T4 : unmanaged =>
        new(_world, _all, _none | ComponentType<T4>.Mask);

    /// <summary>Enumerates matching chunks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_world, _matches);

    /// <summary>Duck-typed chunk enumerator.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<Archetype> _archetypes;
        private readonly ReadOnlySpan<int> _matches;
        private int _index;
        private Chunk3<T1, T2, T3> _current;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(World world, int[] matches)
        {
            ArgumentNullException.ThrowIfNull(world);
            _archetypes = world.Archetypes.AsSpan();
            _matches = matches;
            _index = -1;
            _current = default;
        }

        /// <summary>Advances to the next non-empty matching chunk.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (++_index < _matches.Length)
            {
                Archetype arch = _archetypes[_matches[_index]];
                if (arch.Count > 0)
                {
                    _current = new Chunk3<T1, T2, T3>(
                        arch.Entities,
                        arch.GetColumn<T1>()!.AsSpan(),
                        arch.GetColumn<T2>()!.AsSpan(),
                        arch.GetColumn<T3>()!.AsSpan());
                    return true;
                }
            }

            return false;
        }

        /// <summary>Current chunk.</summary>
        public readonly Chunk3<T1, T2, T3> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }
    }
}

/// <summary>
/// Four-component query.
/// </summary>
/// <typeparam name="T1">First component type.</typeparam>
/// <typeparam name="T2">Second component type.</typeparam>
/// <typeparam name="T3">Third component type.</typeparam>
/// <typeparam name="T4">Fourth component type.</typeparam>
public readonly ref struct Query4<T1, T2, T3, T4>
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
    where T4 : unmanaged
{
    private readonly World _world;
    private readonly ComponentMask _all;
    private readonly ComponentMask _none;
    private readonly int[] _matches;

    /// <summary>Creates a query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query4(World world)
        : this(
            world,
            ComponentType<T1>.Mask | ComponentType<T2>.Mask | ComponentType<T3>.Mask | ComponentType<T4>.Mask,
            ComponentMask.Empty)
    {
    }

    internal Query4(World world, ComponentMask all, ComponentMask none)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        _all = all;
        _none = none;
        _matches = world.QueryCache.Resolve(all, none, world.Archetypes);
    }

    /// <summary>Requires an additional component.</summary>
    /// <typeparam name="T5">Required component type.</typeparam>
    public Query4<T1, T2, T3, T4> With<T5>() where T5 : unmanaged =>
        new(_world, _all | ComponentType<T5>.Mask, _none);

    /// <summary>Excludes a component.</summary>
    /// <typeparam name="T5">Excluded component type.</typeparam>
    public Query4<T1, T2, T3, T4> Without<T5>() where T5 : unmanaged =>
        new(_world, _all, _none | ComponentType<T5>.Mask);

    /// <summary>Enumerates matching chunks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_world, _matches);

    /// <summary>Duck-typed chunk enumerator.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<Archetype> _archetypes;
        private readonly ReadOnlySpan<int> _matches;
        private int _index;
        private Chunk4<T1, T2, T3, T4> _current;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(World world, int[] matches)
        {
            ArgumentNullException.ThrowIfNull(world);
            _archetypes = world.Archetypes.AsSpan();
            _matches = matches;
            _index = -1;
            _current = default;
        }

        /// <summary>Advances to the next non-empty matching chunk.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (++_index < _matches.Length)
            {
                Archetype arch = _archetypes[_matches[_index]];
                if (arch.Count > 0)
                {
                    _current = new Chunk4<T1, T2, T3, T4>(
                        arch.Entities,
                        arch.GetColumn<T1>()!.AsSpan(),
                        arch.GetColumn<T2>()!.AsSpan(),
                        arch.GetColumn<T3>()!.AsSpan(),
                        arch.GetColumn<T4>()!.AsSpan());
                    return true;
                }
            }

            return false;
        }

        /// <summary>Current chunk.</summary>
        public readonly Chunk4<T1, T2, T3, T4> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }
    }
}
