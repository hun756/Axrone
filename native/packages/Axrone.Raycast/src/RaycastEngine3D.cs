namespace Axrone.Raycast;

using Axrone.Geometry;
using Axrone.Numeric;

/// <summary>Broadphase backing a <see cref="RaycastEngine3D"/>.</summary>
public enum RaycastBroadphase3D
{
    /// <summary>Unbounded uniform spatial hash; the default.</summary>
    SpatialHash,

    /// <summary>Fixed-domain native octree; out-of-domain shapes are rejected.</summary>
    Octree,
}

/// <summary>
/// Dynamic 3D raycast world. Shapes are registered once, indexed by a spatial hash or
/// octree, and queried with allocation-free traversal. Planes are unbounded and are tested
/// outside the broadphase. Registration is not thread-safe; queries are read-only and may
/// run concurrently with each other while no shape is being changed and the
/// nearest-contact cache is disabled.
/// </summary>
public sealed class RaycastEngine3D : IDisposable
{
    private const int DefaultVisitCapacity = 64;

    [ThreadStatic]
    private static int[]? s_visited3D;

    [ThreadStatic]
    private static int s_queryId3D;

    private readonly SpatialHashGrid3D<int>? _grid;
    private readonly OctreeBroadphase3D<int>? _octree;
    private readonly Dictionary<RaycastShapeId, int> _slots = [];
    private readonly Stack<int> _freeSlots = new();
    private readonly List<int> _planeSlots = [];
    private readonly RaycastCache3D _cache;
    private ShapeEntry3D[] _entries;
    private int _slotCount;
    private uint _nextId = 1;
    private bool _disposed;

    /// <summary>Creates an engine backed by an unbounded spatial hash.</summary>
    /// <param name="cellSize">Broadphase cell size in world units.</param>
    public RaycastEngine3D(float cellSize = 4f)
    {
        _grid = new SpatialHashGrid3D<int>(cellSize);
        Broadphase = RaycastBroadphase3D.SpatialHash;
        _entries = new ShapeEntry3D[16];
        _cache = new RaycastCache3D();
        Statistics = new RaycastStatistics();
    }

    /// <summary>Creates an engine backed by a native octree over a fixed root domain.</summary>
    /// <param name="rootBounds">Root domain; shapes outside it are rejected at registration.</param>
    public RaycastEngine3D(in Aabb3D rootBounds)
    {
        _octree = new OctreeBroadphase3D<int>(in rootBounds);
        Broadphase = RaycastBroadphase3D.Octree;
        _entries = new ShapeEntry3D[16];
        _cache = new RaycastCache3D();
        Statistics = new RaycastStatistics();
    }

    /// <summary>Gets the configured broadphase.</summary>
    public RaycastBroadphase3D Broadphase { get; }

    /// <summary>Gets the live counters of this engine.</summary>
    public RaycastStatistics Statistics { get; }

    /// <summary>
    /// Gets or sets whether nearest-contact queries are cached for the current frame.
    /// The cache is not thread-safe, so disable it when queries run concurrently.
    /// </summary>
    public bool EnableCache { get; set; }

    /// <summary>Gets the number of registered shapes.</summary>
    public int ShapeCount => _slots.Count;

    /// <summary>Registers a shape under a freshly allocated id.</summary>
    /// <param name="shape">Shape descriptor.</param>
    /// <returns>The assigned identity.</returns>
    public RaycastShapeId Register(in RaycastShape3D shape)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var id = new RaycastShapeId(AllocateId());
        RegisterCore(id, in shape);
        return id;
    }

    /// <summary>Registers a shape under an explicit id.</summary>
    /// <param name="id">Identity to assign.</param>
    /// <param name="shape">Shape descriptor.</param>
    public void Register(RaycastShapeId id, in RaycastShape3D shape)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!id.IsValid)
        {
            ThrowHelper.ThrowInvalidQuery("A shape id must be non-zero.");
        }

        RegisterCore(id, in shape);
    }

    /// <summary>Removes a shape.</summary>
    /// <param name="id">Identity to remove.</param>
    /// <returns><see langword="true"/> when the shape was registered.</returns>
    public bool Unregister(RaycastShapeId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_slots.Remove(id, out int slot))
        {
            return false;
        }

        ref ShapeEntry3D entry = ref _entries[slot];
        if (entry.IsPlane)
        {
            _planeSlots.Remove(slot);
        }
        else if (_grid is not null)
        {
            _ = _grid.Remove(slot);
        }
        else
        {
            _ = _octree!.Remove(slot);
        }

        entry = default;
        _freeSlots.Push(slot);
        _cache.Invalidate();
        return true;
    }

    /// <summary>Sets whether a registered shape participates in queries.</summary>
    /// <param name="id">Registered shape.</param>
    /// <param name="active">New participation state.</param>
    /// <returns><see langword="true"/> when the shape was registered and changed.</returns>
    public bool SetActive(RaycastShapeId id, bool active)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_slots.TryGetValue(id, out int slot))
        {
            return false;
        }

        ref ShapeEntry3D entry = ref _entries[slot];
        if (entry.Active == active)
        {
            return false;
        }

        entry.Active = active;
        _cache.Invalidate();
        return true;
    }

    /// <summary>Reads a registered shape descriptor.</summary>
    /// <param name="id">Registered shape.</param>
    /// <param name="shape">Descriptor when found.</param>
    /// <returns><see langword="true"/> when the shape is registered.</returns>
    public bool TryGetShape(RaycastShapeId id, out RaycastShape3D shape)
    {
        if (_slots.TryGetValue(id, out int slot))
        {
            shape = _entries[slot].Shape;
            return true;
        }

        shape = default;
        return false;
    }

    /// <summary>Runs a query and writes the requested contacts.</summary>
    /// <param name="query">Query descriptor.</param>
    /// <param name="results">Contact destination.</param>
    /// <returns>The number of contacts written.</returns>
    public int Raycast(in RaycastQuery3D query, Span<RaycastHit3D> results)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RaycastValidation.Validate(in query, results.Length);

        if (_slots.Count == 0)
        {
            Statistics.RecordQuery(false);
            return 0;
        }

        bool closest = RaycastValidation.IsClosest(query.Flags);
        if (closest && EnableCache && _cache.TryGet(in query, out RaycastHit3D cached))
        {
            Statistics.RecordCacheHit();
            results[0] = cached;
            return 1;
        }

        EnsureVisitCapacity(_entries.Length);
        int queryId = NextQueryId();
        Ray3D ray = query.Ray;
        var state = new QueryState3D(_entries, s_visited3D!, queryId, in query, results, false, default);
        if (_grid is not null)
        {
            _grid.Raycast(in ray, query.MaxDistance, ref state);
        }
        else
        {
            _octree!.QueryRay(in ray, query.MaxDistance, ref state);
        }

        TraversePlanes(ref state);
        int count = state.Finish();

        Statistics.RecordQuery(count > 0);
        if (closest && count > 0 && EnableCache)
        {
            _cache.Set(in query, in results[0]);
        }

        return count;
    }

    /// <summary>Runs a nearest-contact query.</summary>
    /// <param name="query">Query descriptor.</param>
    /// <param name="hit">Nearest contact when one exists.</param>
    /// <returns><see langword="true"/> when the query reached a shape.</returns>
    public bool Raycast(in RaycastQuery3D query, out RaycastHit3D hit)
    {
        Span<RaycastHit3D> results = stackalloc RaycastHit3D[1];
        if (Raycast(in query, results) > 0)
        {
            hit = results[0];
            return true;
        }

        hit = default;
        return false;
    }

    /// <summary>Probes whether any shape blocks the ray.</summary>
    /// <param name="query">Query descriptor; hit flags are evaluated too.</param>
    /// <returns><see langword="true"/> when at least one shape is reached.</returns>
    public bool RaycastAny(in RaycastQuery3D query)
    {
        Span<RaycastHit3D> results = stackalloc RaycastHit3D[1];
        var anyQuery = query with
        {
            Flags = (query.Flags | RaycastFlags.StopAtFirstHit) & ~RaycastFlags.ClosestOnly,
            MaxHits = 1,
        };
        return Raycast(in anyQuery, results) > 0;
    }

    /// <summary>
    /// Sweeps a sphere, axis-aligned box or oriented box along the ray and reports the
    /// first target it reaches. Sphere casts are exact against every supported target;
    /// box casts against non-axis-aligned targets are conservative.
    /// </summary>
    /// <param name="ray">Normalized sweep ray.</param>
    /// <param name="castVolume">Sphere, axis-aligned box or oriented box being swept.</param>
    /// <param name="maxDistance">Positive sweep limit.</param>
    /// <param name="hit">Nearest contact when one exists.</param>
    /// <param name="layerMask">Layers the sweep accepts.</param>
    /// <param name="flags">Behavior switches; closest-only is implied.</param>
    /// <returns><see langword="true"/> when the sweep reached a target.</returns>
    public bool ShapeCast(
        in Ray3D ray,
        in RaycastShape3D castVolume,
        float maxDistance,
        out RaycastHit3D hit,
        uint layerMask = uint.MaxValue,
        RaycastFlags flags = RaycastFlags.ClosestOnly)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RaycastValidation.ValidateRay(in ray, maxDistance);
        RaycastValidation.ValidateCastVolume(in castVolume);

        if (_slots.Count == 0)
        {
            Statistics.RecordQuery(false);
            hit = default;
            return false;
        }

        var query = new RaycastQuery3D(
            ray,
            maxDistance,
            layerMask,
            flags | RaycastFlags.ClosestOnly,
            1);
        Span<RaycastHit3D> results = stackalloc RaycastHit3D[1];
        EnsureVisitCapacity(_entries.Length);
        int queryId = NextQueryId();
        var state = new QueryState3D(_entries, s_visited3D!, queryId, in query, results, true, castVolume);

        Vec3 halfExtents = castVolume.CastHalfExtents;
        if (_grid is not null)
        {
            _grid.RaycastExpanded(in ray, maxDistance, halfExtents, ref state);
        }
        else
        {
            Vec3 end = ray.At(maxDistance);
            var swept = new Aabb3D(Vec3.Min(ray.Origin, end) - halfExtents, Vec3.Max(ray.Origin, end) + halfExtents);
            _octree!.QueryBounds(in swept, ref state);
        }

        TraversePlanes(ref state);
        int count = state.Finish();
        Statistics.RecordQuery(count > 0);
        if (count > 0)
        {
            hit = results[0];
            return true;
        }

        hit = default;
        return false;
    }

    /// <summary>Runs one nearest-contact query per slot; each slot receives its own result.</summary>
    /// <param name="queries">Parallel queries.</param>
    /// <param name="results">One destination slot per query.</param>
    public void RaycastBatch(ReadOnlySpan<RaycastQuery3D> queries, Span<RaycastHit3D> results)
    {
        if (queries.Length != results.Length)
        {
            ThrowHelper.ThrowArgumentMismatch("Every batch query needs exactly one result slot.");
        }

        for (var index = 0; index < queries.Length; index++)
        {
            RaycastQuery3D query = queries[index] with
            {
                Flags = (queries[index].Flags & ~RaycastFlags.AllHits) | RaycastFlags.ClosestOnly,
                MaxHits = 1,
            };
            results[index] = default;
            _ = Raycast(in query, results.Slice(index, 1));
        }
    }

    /// <summary>Probes every parallel query, writing one flag per query.</summary>
    /// <param name="queries">Parallel queries.</param>
    /// <param name="results">One destination slot per query.</param>
    public void RaycastBatchAny(ReadOnlySpan<RaycastQuery3D> queries, Span<bool> results)
    {
        if (queries.Length != results.Length)
        {
            ThrowHelper.ThrowArgumentMismatch("Every batch query needs exactly one result slot.");
        }

        for (var index = 0; index < queries.Length; index++)
        {
            results[index] = RaycastAny(in queries[index]);
        }
    }

    /// <summary>Advances the nearest-contact cache to a new frame.</summary>
    public void AdvanceFrame() => _cache.AdvanceFrame();

    /// <summary>Invalidates every cached nearest contact.</summary>
    public void ClearCache() => _cache.Invalidate();

    /// <summary>Removes every registered shape while keeping the engine alive.</summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _grid?.Clear();
        _octree?.Clear();
        _planeSlots.Clear();
        _slots.Clear();
        _freeSlots.Clear();
        Array.Clear(_entries);
        _slotCount = 0;
        _nextId = 1;
        _cache.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _octree?.Dispose();
            _grid?.Clear();
            _planeSlots.Clear();
            _slots.Clear();
            _freeSlots.Clear();
            Array.Clear(_entries);
            _slotCount = 0;
            _cache.Clear();
        }

        _disposed = true;
    }

    private void RegisterCore(RaycastShapeId id, in RaycastShape3D shape)
    {
        if (_slots.ContainsKey(id))
        {
            ThrowHelper.ThrowDuplicateShape();
        }

        bool isPlane = shape.Kind == RaycastShapeKind.Plane;
        if (!isPlane && !shape.Bounds.IsValid)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(shape));
        }

        int slot = AcquireSlot();
        _entries[slot] = new ShapeEntry3D
        {
            Shape = shape,
            Id = id,
            Active = true,
            Occupied = true,
            IsPlane = isPlane,
        };

        if (isPlane)
        {
            _planeSlots.Add(slot);
        }
        else
        {
            try
            {
                Aabb3D bounds = shape.Bounds;
                if (_grid is not null)
                {
                    _grid.Insert(slot, in bounds);
                }
                else
                {
                    _octree!.Insert(slot, in bounds);
                }
            }
            catch
            {
                _entries[slot] = default;
                _freeSlots.Push(slot);
                throw;
            }
        }

        _slots.Add(id, slot);
        _cache.Invalidate();
    }

    private int AcquireSlot()
    {
        if (_freeSlots.Count > 0)
        {
            return _freeSlots.Pop();
        }

        if (_slotCount == _entries.Length)
        {
            Array.Resize(ref _entries, _entries.Length * 2);
        }

        return _slotCount++;
    }

    private uint AllocateId()
    {
        for (var attempt = 0; attempt < int.MaxValue; attempt++)
        {
            uint candidate = _nextId++;
            if (candidate == 0)
            {
                candidate = _nextId++;
            }

            if (!_slots.ContainsKey(new RaycastShapeId(candidate)))
            {
                return candidate;
            }
        }

        ThrowHelper.ThrowInvalidOperation("No raycast shape ids remain.");
        return 0;
    }

    private void TraversePlanes(ref QueryState3D state)
    {
        for (var index = 0; index < _planeSlots.Count; index++)
        {
            if (!state.OnItem(_planeSlots[index]))
            {
                return;
            }
        }
    }

    private static void EnsureVisitCapacity(int slots)
    {
        int[]? visited = s_visited3D;
        if (visited is null || visited.Length < slots)
        {
            s_visited3D = new int[Math.Max(slots, DefaultVisitCapacity)];
            s_queryId3D = 0;
        }
    }

    private static int NextQueryId()
    {
        int id = s_queryId3D + 1;
        if (id <= 0)
        {
            Array.Clear(s_visited3D!);
            id = 1;
        }

        s_queryId3D = id;
        return id;
    }

    private struct ShapeEntry3D
    {
        public RaycastShape3D Shape;
        public RaycastShapeId Id;
        public bool Active;
        public bool Occupied;
        public bool IsPlane;
    }

    private ref struct QueryState3D : ISpatialHashItemVisitor<int>, IRaycastVisitor<int>, IOctreeVisitor<int>
    {
        private readonly ShapeEntry3D[] _entries;
        private readonly int[] _visited;
        private readonly int _queryId;
        private readonly Ray3D _ray;
        private readonly float _maxDistance;
        private readonly uint _layerMask;
        private readonly Span<RaycastHit3D> _results;
        private readonly int _maxHits;
        private readonly bool _closestOnly;
        private readonly bool _sortByDistance;
        private readonly bool _stopAtFirst;
        private readonly bool _ignoreTriggers;
        private readonly bool _ignoreBackfaces;
        private readonly bool _includeInactive;
        private readonly bool _isShapeCast;
        private readonly RaycastShape3D _castVolume;
        private RaycastHit3D _best;
        private float _bestDistance;
        private bool _hasBest;
        private int _count;

        public QueryState3D(
            ShapeEntry3D[] entries,
            int[] visited,
            int queryId,
            in RaycastQuery3D query,
            Span<RaycastHit3D> results,
            bool isShapeCast,
            in RaycastShape3D castVolume)
        {
            _entries = entries;
            _visited = visited;
            _queryId = queryId;
            _ray = query.Ray;
            _maxDistance = query.MaxDistance;
            _layerMask = query.LayerMask;
            _results = results;
            _maxHits = Math.Min(query.MaxHits, results.Length);
            _closestOnly = (query.Flags & RaycastFlags.ClosestOnly) != 0;
            _sortByDistance = (query.Flags & RaycastFlags.SortByDistance) != 0;
            _stopAtFirst = (query.Flags & RaycastFlags.StopAtFirstHit) != 0;
            _ignoreTriggers = (query.Flags & RaycastFlags.IgnoreTriggers) != 0;
            _ignoreBackfaces = (query.Flags & RaycastFlags.IgnoreBackfaces) != 0;
            _includeInactive = (query.Flags & RaycastFlags.IncludeInactive) != 0;
            _isShapeCast = isShapeCast;
            _castVolume = castVolume;
            _best = default;
            _bestDistance = float.PositiveInfinity;
            _hasBest = false;
            _count = 0;
        }

        public bool OnItem(int slot)
        {
            if (_visited[slot] == _queryId)
            {
                return true;
            }

            _visited[slot] = _queryId;
            ref readonly ShapeEntry3D entry = ref _entries[slot];
            if (!entry.Occupied || (!entry.Active && !_includeInactive))
            {
                return true;
            }

            if ((entry.Shape.LayerMask & _layerMask) == 0)
            {
                return true;
            }

            if (_ignoreTriggers &&
                (entry.Shape.Trigger || (entry.Shape.LayerMask & (uint)RaycastLayer.Trigger) != 0))
            {
                return true;
            }

            RaycastHit3D hit;
            bool intersected = _isShapeCast
                ? RaycastIntersections.TryShapeCast3D(in _ray, in entry.Shape, in _castVolume, entry.Id, _maxDistance, out hit)
                : RaycastIntersections.TryIntersect3D(in _ray, in entry.Shape, entry.Id, _maxDistance, out hit);
            if (!intersected)
            {
                return true;
            }

            if (_ignoreBackfaces && Vec3.Dot(_ray.Direction, hit.Normal) > 0f)
            {
                return true;
            }

            if (_closestOnly)
            {
                if (hit.Distance < _bestDistance)
                {
                    _best = hit;
                    _bestDistance = hit.Distance;
                    _hasBest = true;
                }

                return true;
            }

            _results[_count++] = hit;
            return !_stopAtFirst && _count < _maxHits;
        }

        public bool OnHit(OctreeHandle handle, in Aabb3D bounds, in int value, Distance distance) => OnItem(value);

        public bool OnVisit(OctreeHandle handle, in Aabb3D bounds, in int value) => OnItem(value);

        public int Finish()
        {
            if (_closestOnly)
            {
                if (!_hasBest)
                {
                    return 0;
                }

                _results[0] = _best;
                return 1;
            }

            if (_sortByDistance && _count > 1)
            {
                RaycastSort.Sort3D(_results[.._count]);
            }

            return _count;
        }
    }
}
