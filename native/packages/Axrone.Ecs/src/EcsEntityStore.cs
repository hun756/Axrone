namespace Axrone.Ecs;

/// <summary>
/// Row record: home archetype, row and version.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EntityRecord(ArchetypeId ArchetypeId, int Row, EntityVersion Version);

/// <summary>
/// Lookup outcome.
/// </summary>
public enum EntityLookupResultKind : byte
{
    /// <summary>No record.</summary>
    NotFound = 0,

    /// <summary>Version mismatch.</summary>
    StaleVersion = 1,

    /// <summary>Live handle.</summary>
    Found = 2,
}

/// <summary>
/// Lookup result: kind plus row and archetype when found.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public readonly struct EntityLookupResult
{
    /// <summary>Outcome.</summary>
    [FieldOffset(0)]
    public readonly EntityLookupResultKind Kind;

    /// <summary>Row when found.</summary>
    [FieldOffset(4)]
    public readonly int Row;

    /// <summary>Archetype when found.</summary>
    [FieldOffset(8)]
    public readonly ArchetypeId ArchetypeId;

    /// <summary>Creates a not-found result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityLookupResult NotFound() => new(EntityLookupResultKind.NotFound, -1, default);

    /// <summary>Creates a stale result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityLookupResult Stale() => new(EntityLookupResultKind.StaleVersion, -1, default);

    /// <summary>Creates a found result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityLookupResult Found(ArchetypeId archetypeId, int row) =>
        new(EntityLookupResultKind.Found, row, archetypeId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EntityLookupResult(EntityLookupResultKind kind, int row, ArchetypeId archetypeId)
    {
        Kind = kind;
        Row = row;
        ArchetypeId = archetypeId;
    }
}

/// <summary>
/// Sparse entity store: id-indexed records with a free list. Destroyed ids
/// recycle with a bumped version so stale handles mismatch.
/// </summary>
public sealed class EntityStore
{
    private EntityRecord[] _records;
    private int[] _freeList;
    private int _freeCount;
    private int _nextId;
    private int _aliveCount;
    private readonly Lock _sync = new();

    /// <summary>Live entity count.</summary>
    public int AliveCount => Volatile.Read(ref _aliveCount);

    /// <summary>Creates a store.</summary>
    public EntityStore(int initialCapacity = 4096)
    {
        _records = GC.AllocateArray<EntityRecord>(initialCapacity, pinned: true);
        _freeList = GC.AllocateArray<int>(initialCapacity, pinned: true);
        _freeCount = 0;
        _nextId = 1;
        _aliveCount = 0;

        for (int i = 0; i < _records.Length; i++)
        {
            _records[i] = new EntityRecord(new ArchetypeId(-1), -1, new EntityVersion(1));
        }
    }

    /// <summary>Allocates a handle, recycling freed ids first.</summary>
    public Entity Allocate(ArchetypeId initialArchetype, int initialRow)
    {
        using (_sync.EnterScope())
        {
            int idVal;
            if (_freeCount > 0)
            {
                idVal = _freeList[--_freeCount];
            }
            else
            {
                idVal = _nextId++;
                if (idVal >= _records.Length)
                {
                    Grow();
                }
            }

            EntityVersion version = _records[idVal].Version;
            _records[idVal] = new EntityRecord(initialArchetype, initialRow, version);
            _aliveCount++;
            return new Entity(new EntityId(idVal), version);
        }
    }

    /// <summary>Frees a handle; stale or unknown handles fail.</summary>
    public bool TryFree(in Entity entity, out EntityRecord deletedRecord)
    {
        using (_sync.EnterScope())
        {
            int id = entity.Id.Value;
            if ((uint)id >= (uint)_records.Length)
            {
                deletedRecord = default;
                return false;
            }

            ref EntityRecord rec = ref _records[id];
            if (rec.ArchetypeId.Value < 0 || rec.Version != entity.Version)
            {
                deletedRecord = default;
                return false;
            }

            deletedRecord = rec;
            rec = new EntityRecord(new ArchetypeId(-1), -1, new EntityVersion(rec.Version.Value + 1));

            if (_freeCount == _freeList.Length)
            {
                Array.Resize(ref _freeList, _freeList.Length * 2);
            }

            _freeList[_freeCount++] = id;
            _aliveCount--;
            return true;
        }
    }

    /// <summary>Resolves a handle to its row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityLookupResult Resolve(in Entity entity)
    {
        int id = entity.Id.Value;
        if ((uint)id >= (uint)_records.Length)
        {
            return EntityLookupResult.NotFound();
        }

        ref EntityRecord rec = ref _records[id];
        if (rec.ArchetypeId.Value < 0)
        {
            return EntityLookupResult.NotFound();
        }

        if (rec.Version != entity.Version)
        {
            return EntityLookupResult.Stale();
        }

        return EntityLookupResult.Found(rec.ArchetypeId, rec.Row);
    }

    /// <summary>Repoints a record after an archetype move.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateRecord(EntityId id, ArchetypeId archetypeId, int row)
    {
        ref EntityRecord rec = ref _records[id.Value];
        rec = new EntityRecord(archetypeId, row, rec.Version);
    }

    private void Grow()
    {
        int newCap = _records.Length * 2;
        EntityRecord[] grown = GC.AllocateArray<EntityRecord>(newCap, pinned: true);
        Array.Copy(_records, grown, _records.Length);
        for (int i = _records.Length; i < newCap; i++)
        {
            grown[i] = new EntityRecord(new ArchetypeId(-1), -1, new EntityVersion(1));
        }

        _records = grown;
    }
}
