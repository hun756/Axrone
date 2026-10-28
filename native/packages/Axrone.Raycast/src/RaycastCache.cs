namespace Axrone.Raycast;

using Axrone.Geometry;
using Axrone.Numeric;

/// <summary>
/// Fixed-capacity, direct-mapped cache of nearest contacts for one frame. A lookup never
/// allocates and never probes past one slot, so a collision simply replaces the entry.
/// Entries from an older frame are ignored, which makes <see cref="AdvanceFrame"/> and
/// <see cref="Invalidate"/> O(1). The cache is not thread-safe; engines document when it
/// may be enabled.
/// </summary>
public sealed class RaycastCache2D
{
    private const int DefaultCapacity = 64;

    private readonly Entry[] _entries;
    private readonly int _mask;
    private int _generation = 1;

    /// <summary>Creates a cache.</summary>
    /// <param name="capacity">Power-of-two slot count.</param>
    public RaycastCache2D(int capacity = DefaultCapacity)
    {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(capacity));
        }

        _entries = GC.AllocateArray<Entry>(capacity, pinned: true);
        _mask = capacity - 1;
    }

    /// <summary>Gets the number of slots.</summary>
    public int Capacity => _entries.Length;

    /// <summary>Tries to read a contact recorded for the current frame.</summary>
    /// <param name="query">Query that produced the contact.</param>
    /// <param name="hit">Cached contact when found.</param>
    /// <returns><see langword="true"/> when the current frame holds this exact query.</returns>
    public bool TryGet(in RaycastQuery2D query, out RaycastHit2D hit)
    {
        int index = (int)ComputeHash(in query) & _mask;
        ref readonly Entry entry = ref _entries[index];
        if (entry.Generation != _generation ||
            entry.Origin != query.Ray.Origin ||
            entry.Direction != query.Ray.Direction ||
            entry.MaxDistance != query.MaxDistance ||
            entry.LayerMask != query.LayerMask ||
            entry.Flags != (uint)query.Flags ||
            entry.MaxHits != query.MaxHits)
        {
            hit = default;
            return false;
        }

        hit = entry.Hit;
        return true;
    }

    /// <summary>Records a contact for the current frame, replacing any colliding slot.</summary>
    /// <param name="query">Query that produced the contact.</param>
    /// <param name="hit">Contact to cache.</param>
    public void Set(in RaycastQuery2D query, in RaycastHit2D hit)
    {
        int index = (int)ComputeHash(in query) & _mask;
        ref Entry entry = ref _entries[index];
        entry.Generation = _generation;
        entry.Origin = query.Ray.Origin;
        entry.Direction = query.Ray.Direction;
        entry.MaxDistance = query.MaxDistance;
        entry.LayerMask = query.LayerMask;
        entry.Flags = (uint)query.Flags;
        entry.MaxHits = query.MaxHits;
        entry.Hit = hit;
    }

    /// <summary>Advances to a new frame, ignoring every older entry.</summary>
    public void AdvanceFrame() => AdvanceGeneration();

    /// <summary>Invalidates every entry in O(1) by moving to a new generation.</summary>
    public void Invalidate() => AdvanceGeneration();

    /// <summary>Drops every entry and resets the frame generation.</summary>
    public void Clear()
    {
        Array.Clear(_entries);
        _generation = 1;
    }

    private void AdvanceGeneration()
    {
        if (_generation == int.MaxValue)
        {
            Clear();
            return;
        }

        _generation++;
    }

    private static uint ComputeHash(in RaycastQuery2D query)
    {
        uint hash = 2166136261u;
        hash = CacheHash.Combine(hash, query.Ray.Origin.X);
        hash = CacheHash.Combine(hash, query.Ray.Origin.Y);
        hash = CacheHash.Combine(hash, query.Ray.Direction.X);
        hash = CacheHash.Combine(hash, query.Ray.Direction.Y);
        hash = CacheHash.Combine(hash, query.MaxDistance);
        hash = CacheHash.Combine(hash, query.LayerMask);
        hash = CacheHash.Combine(hash, (uint)query.Flags);
        return CacheHash.Combine(hash, (uint)query.MaxHits);
    }

    private struct Entry
    {
        public int Generation;
        public Vec2 Origin;
        public Vec2 Direction;
        public float MaxDistance;
        public uint LayerMask;
        public uint Flags;
        public int MaxHits;
        public RaycastHit2D Hit;
    }
}

/// <summary>
/// Fixed-capacity, direct-mapped cache of nearest 3D contacts. A lookup never allocates
/// and never probes past one slot. The cache is not thread-safe.
/// </summary>
public sealed class RaycastCache3D
{
    private const int DefaultCapacity = 64;

    private readonly Entry[] _entries;
    private readonly int _mask;
    private int _generation = 1;

    /// <summary>Creates a cache.</summary>
    /// <param name="capacity">Power-of-two slot count.</param>
    public RaycastCache3D(int capacity = DefaultCapacity)
    {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(capacity));
        }

        _entries = GC.AllocateArray<Entry>(capacity, pinned: true);
        _mask = capacity - 1;
    }

    /// <summary>Gets the number of slots.</summary>
    public int Capacity => _entries.Length;

    /// <summary>Tries to read a contact recorded for the current frame.</summary>
    /// <param name="query">Query that produced the contact.</param>
    /// <param name="hit">Cached contact when found.</param>
    /// <returns><see langword="true"/> when the current frame holds this exact query.</returns>
    public bool TryGet(in RaycastQuery3D query, out RaycastHit3D hit)
    {
        int index = (int)ComputeHash(in query) & _mask;
        ref readonly Entry entry = ref _entries[index];
        if (entry.Generation != _generation ||
            entry.Origin != query.Ray.Origin ||
            entry.Direction != query.Ray.Direction ||
            entry.MaxDistance != query.MaxDistance ||
            entry.LayerMask != query.LayerMask ||
            entry.Flags != (uint)query.Flags ||
            entry.MaxHits != query.MaxHits)
        {
            hit = default;
            return false;
        }

        hit = entry.Hit;
        return true;
    }

    /// <summary>Records a contact for the current frame, replacing any colliding slot.</summary>
    /// <param name="query">Query that produced the contact.</param>
    /// <param name="hit">Contact to cache.</param>
    public void Set(in RaycastQuery3D query, in RaycastHit3D hit)
    {
        int index = (int)ComputeHash(in query) & _mask;
        ref Entry entry = ref _entries[index];
        entry.Generation = _generation;
        entry.Origin = query.Ray.Origin;
        entry.Direction = query.Ray.Direction;
        entry.MaxDistance = query.MaxDistance;
        entry.LayerMask = query.LayerMask;
        entry.Flags = (uint)query.Flags;
        entry.MaxHits = query.MaxHits;
        entry.Hit = hit;
    }

    /// <summary>Advances to a new frame, ignoring every older entry.</summary>
    public void AdvanceFrame() => AdvanceGeneration();

    /// <summary>Invalidates every entry in O(1) by moving to a new generation.</summary>
    public void Invalidate() => AdvanceGeneration();

    /// <summary>Drops every entry and resets the frame generation.</summary>
    public void Clear()
    {
        Array.Clear(_entries);
        _generation = 1;
    }

    private void AdvanceGeneration()
    {
        if (_generation == int.MaxValue)
        {
            Clear();
            return;
        }

        _generation++;
    }

    private static uint ComputeHash(in RaycastQuery3D query)
    {
        uint hash = 2166136261u;
        hash = CacheHash.Combine(hash, query.Ray.Origin.X);
        hash = CacheHash.Combine(hash, query.Ray.Origin.Y);
        hash = CacheHash.Combine(hash, query.Ray.Origin.Z);
        hash = CacheHash.Combine(hash, query.Ray.Direction.X);
        hash = CacheHash.Combine(hash, query.Ray.Direction.Y);
        hash = CacheHash.Combine(hash, query.Ray.Direction.Z);
        hash = CacheHash.Combine(hash, query.MaxDistance);
        hash = CacheHash.Combine(hash, query.LayerMask);
        hash = CacheHash.Combine(hash, (uint)query.Flags);
        return CacheHash.Combine(hash, (uint)query.MaxHits);
    }

    private struct Entry
    {
        public int Generation;
        public Vec3 Origin;
        public Vec3 Direction;
        public float MaxDistance;
        public uint LayerMask;
        public uint Flags;
        public int MaxHits;
        public RaycastHit3D Hit;
    }
}

/// <summary>FNV-1a mixing shared by the raycast caches.</summary>
internal static class CacheHash
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Combine(uint hash, float value) =>
        (hash ^ BitConverter.SingleToUInt32Bits(value)) * 16777619u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Combine(uint hash, uint value) =>
        (hash ^ value) * 16777619u;
}
