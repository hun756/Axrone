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

    /// <summary>Added tick per row; aligned with <see cref="Entities"/>.</summary>
    public readonly ReadOnlySpan<int> Added1;

    /// <summary>Changed tick per row; aligned with <see cref="Entities"/>.</summary>
    public readonly ReadOnlySpan<int> Changed1;

    /// <summary>Creates a chunk without change ticks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk1(ReadOnlySpan<Entity> entities, Span<T1> c1)
        : this(entities, c1, default, default)
    {
    }

    /// <summary>Creates a chunk with change ticks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk1(
        ReadOnlySpan<Entity> entities,
        Span<T1> c1,
        ReadOnlySpan<int> added1,
        ReadOnlySpan<int> changed1)
    {
        Entities = entities;
        Column1 = c1;
        Added1 = added1;
        Changed1 = changed1;
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

    /// <summary>First added tick per row.</summary>
    public readonly ReadOnlySpan<int> Added1;

    /// <summary>First changed tick per row.</summary>
    public readonly ReadOnlySpan<int> Changed1;

    /// <summary>Second added tick per row.</summary>
    public readonly ReadOnlySpan<int> Added2;

    /// <summary>Second changed tick per row.</summary>
    public readonly ReadOnlySpan<int> Changed2;

    /// <summary>Creates a chunk without change ticks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk2(ReadOnlySpan<Entity> entities, Span<T1> c1, Span<T2> c2)
        : this(entities, c1, c2, default, default, default, default)
    {
    }

    /// <summary>Creates a chunk with change ticks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk2(
        ReadOnlySpan<Entity> entities,
        Span<T1> c1,
        Span<T2> c2,
        ReadOnlySpan<int> added1,
        ReadOnlySpan<int> changed1,
        ReadOnlySpan<int> added2,
        ReadOnlySpan<int> changed2)
    {
        Entities = entities;
        Column1 = c1;
        Column2 = c2;
        Added1 = added1;
        Changed1 = changed1;
        Added2 = added2;
        Changed2 = changed2;
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

    /// <summary>First added tick per row.</summary>
    public readonly ReadOnlySpan<int> Added1;

    /// <summary>First changed tick per row.</summary>
    public readonly ReadOnlySpan<int> Changed1;

    /// <summary>Second added tick per row.</summary>
    public readonly ReadOnlySpan<int> Added2;

    /// <summary>Second changed tick per row.</summary>
    public readonly ReadOnlySpan<int> Changed2;

    /// <summary>Third added tick per row.</summary>
    public readonly ReadOnlySpan<int> Added3;

    /// <summary>Third changed tick per row.</summary>
    public readonly ReadOnlySpan<int> Changed3;

    /// <summary>Creates a chunk without change ticks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk3(ReadOnlySpan<Entity> entities, Span<T1> c1, Span<T2> c2, Span<T3> c3)
        : this(entities, c1, c2, c3, default, default, default, default, default, default)
    {
    }

    /// <summary>Creates a chunk with change ticks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk3(
        ReadOnlySpan<Entity> entities,
        Span<T1> c1,
        Span<T2> c2,
        Span<T3> c3,
        ReadOnlySpan<int> added1,
        ReadOnlySpan<int> changed1,
        ReadOnlySpan<int> added2,
        ReadOnlySpan<int> changed2,
        ReadOnlySpan<int> added3,
        ReadOnlySpan<int> changed3)
    {
        Entities = entities;
        Column1 = c1;
        Column2 = c2;
        Column3 = c3;
        Added1 = added1;
        Changed1 = changed1;
        Added2 = added2;
        Changed2 = changed2;
        Added3 = added3;
        Changed3 = changed3;
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

    /// <summary>First added tick per row.</summary>
    public readonly ReadOnlySpan<int> Added1;

    /// <summary>First changed tick per row.</summary>
    public readonly ReadOnlySpan<int> Changed1;

    /// <summary>Second added tick per row.</summary>
    public readonly ReadOnlySpan<int> Added2;

    /// <summary>Second changed tick per row.</summary>
    public readonly ReadOnlySpan<int> Changed2;

    /// <summary>Third added tick per row.</summary>
    public readonly ReadOnlySpan<int> Added3;

    /// <summary>Third changed tick per row.</summary>
    public readonly ReadOnlySpan<int> Changed3;

    /// <summary>Fourth added tick per row.</summary>
    public readonly ReadOnlySpan<int> Added4;

    /// <summary>Fourth changed tick per row.</summary>
    public readonly ReadOnlySpan<int> Changed4;

    /// <summary>Creates a chunk without change ticks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk4(
        ReadOnlySpan<Entity> entities,
        Span<T1> c1,
        Span<T2> c2,
        Span<T3> c3,
        Span<T4> c4)
        : this(entities, c1, c2, c3, c4, default, default, default, default, default, default, default, default)
    {
    }

    /// <summary>Creates a chunk with change ticks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk4(
        ReadOnlySpan<Entity> entities,
        Span<T1> c1,
        Span<T2> c2,
        Span<T3> c3,
        Span<T4> c4,
        ReadOnlySpan<int> added1,
        ReadOnlySpan<int> changed1,
        ReadOnlySpan<int> added2,
        ReadOnlySpan<int> changed2,
        ReadOnlySpan<int> added3,
        ReadOnlySpan<int> changed3,
        ReadOnlySpan<int> added4,
        ReadOnlySpan<int> changed4)
    {
        Entities = entities;
        Column1 = c1;
        Column2 = c2;
        Column3 = c3;
        Column4 = c4;
        Added1 = added1;
        Changed1 = changed1;
        Added2 = added2;
        Changed2 = changed2;
        Added3 = added3;
        Changed3 = changed3;
        Added4 = added4;
        Changed4 = changed4;
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
                    ComponentColumn<T1> c1 = arch.GetColumn<T1>()!;
                    int count = arch.Count;
                    _current = new Chunk1<T1>(
                        arch.Entities,
                        c1.AsSpan(),
                        c1.AddedTicks[..count],
                        c1.ChangedTicks[..count]);
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
                    ComponentColumn<T1> c1 = arch.GetColumn<T1>()!;
                    ComponentColumn<T2> c2 = arch.GetColumn<T2>()!;
                    int count = arch.Count;
                    _current = new Chunk2<T1, T2>(
                        arch.Entities,
                        c1.AsSpan(),
                        c2.AsSpan(),
                        c1.AddedTicks[..count],
                        c1.ChangedTicks[..count],
                        c2.AddedTicks[..count],
                        c2.ChangedTicks[..count]);
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
                    ComponentColumn<T1> c1 = arch.GetColumn<T1>()!;
                    ComponentColumn<T2> c2 = arch.GetColumn<T2>()!;
                    ComponentColumn<T3> c3 = arch.GetColumn<T3>()!;
                    int count = arch.Count;
                    _current = new Chunk3<T1, T2, T3>(
                        arch.Entities,
                        c1.AsSpan(),
                        c2.AsSpan(),
                        c3.AsSpan(),
                        c1.AddedTicks[..count],
                        c1.ChangedTicks[..count],
                        c2.AddedTicks[..count],
                        c2.ChangedTicks[..count],
                        c3.AddedTicks[..count],
                        c3.ChangedTicks[..count]);
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
                    ComponentColumn<T1> c1 = arch.GetColumn<T1>()!;
                    ComponentColumn<T2> c2 = arch.GetColumn<T2>()!;
                    ComponentColumn<T3> c3 = arch.GetColumn<T3>()!;
                    ComponentColumn<T4> c4 = arch.GetColumn<T4>()!;
                    int count = arch.Count;
                    _current = new Chunk4<T1, T2, T3, T4>(
                        arch.Entities,
                        c1.AsSpan(),
                        c2.AsSpan(),
                        c3.AsSpan(),
                        c4.AsSpan(),
                        c1.AddedTicks[..count],
                        c1.ChangedTicks[..count],
                        c2.AddedTicks[..count],
                        c2.ChangedTicks[..count],
                        c3.AddedTicks[..count],
                        c3.ChangedTicks[..count],
                        c4.AddedTicks[..count],
                        c4.ChangedTicks[..count]);
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
