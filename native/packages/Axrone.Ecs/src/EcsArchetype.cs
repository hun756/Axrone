namespace Axrone.Ecs;

/// <summary>
/// One archetype: entities sharing an exact component set, stored in dense
/// columns with swap-remove. Transition caches memoize add/remove edges.
/// </summary>
public sealed class Archetype : IDisposable
{
    private readonly IComponentColumn[] _columns;
    private readonly short[] _componentMap = new short[256];
    private Entity[] _entities;
    private int _count;

    /// <summary>Archetype index.</summary>
    public readonly ArchetypeId Id;

    /// <summary>Component set.</summary>
    public readonly ComponentMask Mask;

    /// <summary>Add-edge cache per component.</summary>
    public readonly int[] TransitionsAdd = new int[256];

    /// <summary>Remove-edge cache per component.</summary>
    public readonly int[] TransitionsRemove = new int[256];

    /// <summary>Live row count.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Row capacity.</summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entities.Length;
    }

    /// <summary>Creates an archetype.</summary>
    public Archetype(ArchetypeId id, in ComponentMask mask, IComponentColumn[] columns, int initialCapacity = 32)
    {
        ArgumentNullException.ThrowIfNull(columns);
        Id = id;
        Mask = mask;
        _columns = columns;
        _entities = GC.AllocateArray<Entity>(initialCapacity, pinned: true);
        _count = 0;

        Array.Fill(_componentMap, (short)-1);
        Array.Fill(TransitionsAdd, -1);
        Array.Fill(TransitionsRemove, -1);

        for (int i = 0; i < columns.Length; i++)
        {
            _componentMap[columns[i].Id.Value] = (short)i;
        }
    }

    /// <summary>Live entities.</summary>
    public ReadOnlySpan<Entity> Entities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_entities, 0, _count);
    }

    /// <summary>Finds a typed column.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentColumn<T>? GetColumn<T>() where T : unmanaged
    {
        short idx = _componentMap[ComponentType<T>.Id.Value];
        return idx >= 0 ? Unsafe.As<ComponentColumn<T>>(_columns[idx]) : null;
    }

    /// <summary>Finds a column by id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IComponentColumn? GetColumn(ComponentId id)
    {
        short idx = _componentMap[id.Value];
        return idx >= 0 ? _columns[idx] : null;
    }

    /// <summary>Appends a row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AllocateRow(in Entity entity)
    {
        if (_count == _entities.Length)
        {
            Grow();
        }

        int row = _count++;
        ref Entity start = ref MemoryMarshal.GetArrayDataReference(_entities);
        Unsafe.Add(ref start, (uint)row) = entity;
        return row;
    }

    /// <summary>Swap-removes a row; reports the moved entity when one shifts.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveRow(int row, out Entity movedEntity)
    {
        int lastRow = --_count;
        if (row < lastRow)
        {
            ref Entity start = ref MemoryMarshal.GetArrayDataReference(_entities);
            movedEntity = Unsafe.Add(ref start, (uint)lastRow);
            Unsafe.Add(ref start, (uint)row) = movedEntity;

            for (int i = 0; i < _columns.Length; i++)
            {
                _columns[i].MoveRow(lastRow, row);
            }

            return true;
        }

        movedEntity = Entity.Null;
        return false;
    }

    private void Grow()
    {
        int newCap = _entities.Length * 2;
        Entity[] grown = GC.AllocateArray<Entity>(newCap, pinned: true);
        Array.Copy(_entities, grown, _count);
        _entities = grown;

        for (int i = 0; i < _columns.Length; i++)
        {
            _columns[i].Resize(newCap);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (int i = 0; i < _columns.Length; i++)
        {
            _columns[i].Dispose();
        }

        _entities = Array.Empty<Entity>();
        _count = 0;
    }
}

/// <summary>
/// Archetype graph: root empty archetype plus mask-keyed creation with
/// transition caching. Structural changes take the gate.
/// </summary>
public sealed class ArchetypeGraph : IDisposable
{
    private Archetype[] _archetypes = new Archetype[32];
    private int _count;
    private int _version;
    private readonly Lock _gate = new();

    /// <summary>Archetype count.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Graph version, bumped whenever an archetype is created.</summary>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Creates a graph with the root empty archetype.</summary>
    public ArchetypeGraph()
    {
        _archetypes[0] = new Archetype(new ArchetypeId(0), ComponentMask.Empty, Array.Empty<IComponentColumn>());
        _count = 1;
    }

    /// <summary>Fetches an archetype by id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Archetype GetArchetype(ArchetypeId id)
    {
        ref Archetype start = ref MemoryMarshal.GetArrayDataReference(_archetypes);
        return Unsafe.Add(ref start, (uint)id.Value);
    }

    /// <summary>Views live archetypes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<Archetype> AsSpan() => new(_archetypes, 0, _count);

    /// <summary>Finds or creates the archetype for a mask.</summary>
    public Archetype GetOrCreate(in ComponentMask mask, Func<ComponentMask, IComponentColumn[]> columnFactory)
    {
        ArgumentNullException.ThrowIfNull(columnFactory);
        using (_gate.EnterScope())
        {
            for (int i = 0; i < _count; i++)
            {
                if (_archetypes[i].Mask == mask)
                {
                    return _archetypes[i];
                }
            }

            int id = _count;
            if (id == _archetypes.Length)
            {
                Array.Resize(ref _archetypes, _archetypes.Length * 2);
            }

            IComponentColumn[] columns = columnFactory(mask);
            Archetype created = new(new ArchetypeId(id), mask, columns);
            _archetypes[id] = created;
            _count++;
            Interlocked.Increment(ref _version);
            return created;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (int i = 0; i < _count; i++)
        {
            _archetypes[i].Dispose();
        }

        _archetypes = Array.Empty<Archetype>();
        _count = 0;
    }
}
