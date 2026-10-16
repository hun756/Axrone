namespace Axrone.Ecs;

/// <summary>
/// Root world orchestrator: owns the archetype graph, the entity store and
/// diagnostics. Structural operations serialize on one gate; component reads
/// go straight to columns.
/// </summary>
public sealed class World : IDisposable
{
    private readonly ArchetypeGraph _archetypes = new();
    private readonly EntityStore _entities;
    private readonly IDiagnosticsSink _diagnostics;
    private readonly Lock _structuralSync = new();
    private long _frameCount;

    /// <summary>Archetype graph.</summary>
    public ArchetypeGraph Archetypes => _archetypes;

    /// <summary>Entity store.</summary>
    public EntityStore Entities => _entities;

    /// <summary>Diagnostics sink.</summary>
    public IDiagnosticsSink Diagnostics => _diagnostics;

    /// <summary>Stepped frame count.</summary>
    public long FrameCount => Volatile.Read(ref _frameCount);

    /// <summary>Creates a world.</summary>
    public World(int initialCapacity = 4096, IDiagnosticsSink? diagnostics = null)
    {
        _entities = new EntityStore(initialCapacity);
        _diagnostics = diagnostics ?? NullDiagnosticsSink.Instance;
    }

    /// <summary>Begins a draft entity in the root archetype.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityTransaction<DraftState> BeginEntity()
    {
        Entity entity;
        using (_structuralSync.EnterScope())
        {
            Archetype root = _archetypes.GetArchetype(new ArchetypeId(0));
            int row = root.AllocateRow(Entity.Null);
            entity = _entities.Allocate(root.Id, row);
            _diagnostics.OnEntityCreated(entity);
        }

        return new EntityTransaction<DraftState>(this, entity);
    }

    /// <summary>Adds or replaces a component, moving archetypes as needed.</summary>
    public void AddComponent<T>(scoped in Entity entity, scoped in T component) where T : unmanaged
    {
        using (_structuralSync.EnterScope())
        {
            EntityLookupResult loc = _entities.Resolve(entity);
            if (loc.Kind != EntityLookupResultKind.Found)
            {
                EcsThrowHelper.ThrowEntityNotFound(entity);
            }

            Archetype currentArch = _archetypes.GetArchetype(loc.ArchetypeId);
            ComponentId compId = ComponentType<T>.Id;

            if (currentArch.Mask.IsSet(compId))
            {
                currentArch.GetColumn<T>()!.Set(loc.Row, in component);
                return;
            }

            int nextArchId = Volatile.Read(ref currentArch.TransitionsAdd[compId.Value]);
            Archetype nextArch;

            if (nextArchId >= 0)
            {
                nextArch = _archetypes.GetArchetype(new ArchetypeId(nextArchId));
            }
            else
            {
                ComponentMask nextMask = currentArch.Mask.With(compId);
                nextArch = _archetypes.GetOrCreate(nextMask, CreateColumnsForMask);
                currentArch.TransitionsAdd[compId.Value] = nextArch.Id.Value;
                nextArch.TransitionsRemove[compId.Value] = currentArch.Id.Value;
            }

            int newRow = nextArch.AllocateRow(entity);

            for (ushort c = 0; c < 256; c++)
            {
                if (c == compId.Value)
                {
                    continue;
                }

                ComponentId cid = new(c);
                if (currentArch.Mask.IsSet(cid))
                {
                    IComponentColumn srcCol = currentArch.GetColumn(cid)!;
                    IComponentColumn dstCol = nextArch.GetColumn(cid)!;
                    srcCol.CopyTo(loc.Row, dstCol, newRow);
                }
            }

            nextArch.GetColumn<T>()!.Set(newRow, in component);

            if (currentArch.RemoveRow(loc.Row, out Entity movedEntity))
            {
                _entities.UpdateRecord(movedEntity.Id, currentArch.Id, loc.Row);
            }

            _entities.UpdateRecord(entity.Id, nextArch.Id, newRow);
        }
    }

    /// <summary>Removes a component, moving archetypes as needed.</summary>
    public void RemoveComponent<T>(scoped in Entity entity) where T : unmanaged
    {
        using (_structuralSync.EnterScope())
        {
            EntityLookupResult loc = _entities.Resolve(entity);
            if (loc.Kind != EntityLookupResultKind.Found)
            {
                EcsThrowHelper.ThrowEntityNotFound(entity);
            }

            Archetype currentArch = _archetypes.GetArchetype(loc.ArchetypeId);
            ComponentId compId = ComponentType<T>.Id;

            if (!currentArch.Mask.IsSet(compId))
            {
                return;
            }

            int nextArchId = Volatile.Read(ref currentArch.TransitionsRemove[compId.Value]);
            Archetype nextArch;

            if (nextArchId >= 0)
            {
                nextArch = _archetypes.GetArchetype(new ArchetypeId(nextArchId));
            }
            else
            {
                ComponentMask nextMask = currentArch.Mask.Without(compId);
                nextArch = _archetypes.GetOrCreate(nextMask, CreateColumnsForMask);
                currentArch.TransitionsRemove[compId.Value] = nextArch.Id.Value;
                nextArch.TransitionsAdd[compId.Value] = currentArch.Id.Value;
            }

            int newRow = nextArch.AllocateRow(entity);

            for (ushort c = 0; c < 256; c++)
            {
                if (c == compId.Value)
                {
                    continue;
                }

                ComponentId cid = new(c);
                if (nextArch.Mask.IsSet(cid))
                {
                    IComponentColumn srcCol = currentArch.GetColumn(cid)!;
                    IComponentColumn dstCol = nextArch.GetColumn(cid)!;
                    srcCol.CopyTo(loc.Row, dstCol, newRow);
                }
            }

            if (currentArch.RemoveRow(loc.Row, out Entity movedEntity))
            {
                _entities.UpdateRecord(movedEntity.Id, currentArch.Id, loc.Row);
            }

            _entities.UpdateRecord(entity.Id, nextArch.Id, newRow);
        }
    }

    /// <summary>References a component for direct mutation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetComponent<T>(scoped in Entity entity) where T : unmanaged
    {
        EntityLookupResult loc = _entities.Resolve(entity);
        if (loc.Kind != EntityLookupResultKind.Found)
        {
            EcsThrowHelper.ThrowEntityNotFound(entity);
        }

        Archetype arch = _archetypes.GetArchetype(loc.ArchetypeId);
        ComponentColumn<T>? col = arch.GetColumn<T>();
        if (col == null)
        {
            EcsThrowHelper.ThrowComponentNotFound(typeof(T).Name);
        }

        return ref col.Get(loc.Row);
    }

    /// <summary>Whether an entity carries a component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasComponent<T>(scoped in Entity entity) where T : unmanaged
    {
        EntityLookupResult loc = _entities.Resolve(entity);
        if (loc.Kind != EntityLookupResultKind.Found)
        {
            return false;
        }

        return _archetypes.GetArchetype(loc.ArchetypeId).Mask.IsSet(ComponentType<T>.Id);
    }

    /// <summary>Destroys an entity; unknown handles are ignored.</summary>
    public void DestroyEntity(scoped in Entity entity)
    {
        using (_structuralSync.EnterScope())
        {
            if (_entities.TryFree(entity, out EntityRecord record))
            {
                Archetype arch = _archetypes.GetArchetype(record.ArchetypeId);
                if (arch.RemoveRow(record.Row, out Entity movedEntity))
                {
                    _entities.UpdateRecord(movedEntity.Id, arch.Id, record.Row);
                }

                _diagnostics.OnEntityDestroyed(entity);
            }
        }
    }

    /// <summary>Advances the frame counter.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Step()
    {
        Interlocked.Increment(ref _frameCount);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _archetypes.Dispose();
    }

    private static IComponentColumn[] CreateColumnsForMask(ComponentMask mask)
    {
        int count = 0;
        for (ushort i = 0; i < 256; i++)
        {
            if (mask.IsSet(new ComponentId(i)))
            {
                count++;
            }
        }

        IComponentColumn[] columns = new IComponentColumn[count];
        int idx = 0;

        for (ushort i = 0; i < 256; i++)
        {
            ComponentId cid = new(i);
            if (!mask.IsSet(cid))
            {
                continue;
            }

            columns[idx++] = ComponentRegistry.Create(cid, 32);
        }

        return columns;
    }
}
