namespace Axrone.Raycast;

using Axrone.Geometry;
using Axrone.Numeric;

/// <summary>
/// Dynamic 2D raycast world. Shapes are registered once, indexed by a uniform spatial
/// hash, and queried with allocation-free traversal. Registration is not thread-safe;
/// queries are read-only and may run concurrently with each other, provided no thread is
/// registering, unregistering or toggling a shape and the nearest-contact cache is
/// disabled when more than one thread queries.
/// </summary>
public sealed class RaycastEngine2D : IDisposable
{
    private const int DefaultVisitCapacity = 64;

    [ThreadStatic]
    private static int[]? s_visited2D;

    [ThreadStatic]
    private static int s_queryId2D;

    private readonly SpatialHashGrid2D<int> _grid;
    private readonly Dictionary<RaycastShapeId, int> _slots = [];
    private readonly Stack<int> _freeSlots = new();
    private readonly RaycastCache2D _cache;
    private ShapeEntry2D[] _entries;
    private int _slotCount;
    private uint _nextId = 1;
    private bool _disposed;

    /// <summary>Creates a 2D raycast engine.</summary>
    /// <param name="cellSize">Broadphase cell size in world units.</param>
    public RaycastEngine2D(float cellSize = 4f)
    {
        _grid = new SpatialHashGrid2D<int>(cellSize);
        _entries = new ShapeEntry2D[16];
        _cache = new RaycastCache2D();
        Statistics = new RaycastStatistics();
    }

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
    public RaycastShapeId Register(in RaycastShape2D shape)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var id = new RaycastShapeId(AllocateId());
        RegisterCore(id, in shape);
        return id;
    }

    /// <summary>Registers a shape under an explicit id.</summary>
    /// <param name="id">Identity to assign.</param>
    /// <param name="shape">Shape descriptor.</param>
    public void Register(RaycastShapeId id, in RaycastShape2D shape)
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

        _ = _grid.Remove(slot);
        _entries[slot] = default;
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

        ref ShapeEntry2D entry = ref _entries[slot];
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
    public bool TryGetShape(RaycastShapeId id, out RaycastShape2D shape)
    {
        if (_slots.TryGetValue(id, out int slot))
        {
            shape = _entries[slot].Shape;
            return true;
        }

        shape = default;
        return false;
    }

    /// <summary>Runs a query and writes at most one slot per required contact.</summary>
    /// <param name="query">Query descriptor.</param>
    /// <param name="results">Contact destination.</param>
    /// <returns>The number of contacts written.</returns>
    public int Raycast(in RaycastQuery2D query, Span<RaycastHit2D> results)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RaycastValidation.Validate(in query, results.Length);

        if (_slots.Count == 0)
        {
            Statistics.RecordQuery(false);
            return 0;
        }

        bool closest = RaycastValidation.IsClosest(query.Flags);
        if (closest && EnableCache && _cache.TryGet(in query, out RaycastHit2D cached))
        {
            Statistics.RecordCacheHit();
            results[0] = cached;
            return 1;
        }

        EnsureVisitCapacity(_entries.Length);
        int queryId = NextQueryId();
        Ray2D ray = query.Ray;
        var state = new QueryState2D(_entries, s_visited2D!, queryId, in query, results);
        _grid.Raycast(in ray, query.MaxDistance, ref state);
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
    public bool Raycast(in RaycastQuery2D query, out RaycastHit2D hit)
    {
        Span<RaycastHit2D> results = stackalloc RaycastHit2D[1];
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
    public bool RaycastAny(in RaycastQuery2D query)
    {
        Span<RaycastHit2D> results = stackalloc RaycastHit2D[1];
        var anyQuery = query with
        {
            Flags = (query.Flags | RaycastFlags.StopAtFirstHit) & ~RaycastFlags.ClosestOnly,
            MaxHits = 1,
        };
        return Raycast(in anyQuery, results) > 0;
    }

    /// <summary>Runs one nearest-contact query per slot; each slot receives its own result.</summary>
    /// <param name="queries">Parallel queries.</param>
    /// <param name="results">One destination slot per query.</param>
    public void RaycastBatch(ReadOnlySpan<RaycastQuery2D> queries, Span<RaycastHit2D> results)
    {
        if (queries.Length != results.Length)
        {
            ThrowHelper.ThrowArgumentMismatch("Every batch query needs exactly one result slot.");
        }

        for (var index = 0; index < queries.Length; index++)
        {
            RaycastQuery2D query = queries[index] with
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
    public void RaycastBatchAny(ReadOnlySpan<RaycastQuery2D> queries, Span<bool> results)
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
        _grid.Clear();
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
            _grid.Clear();
            _slots.Clear();
            _freeSlots.Clear();
            Array.Clear(_entries);
            _slotCount = 0;
            _cache.Clear();
        }

        _disposed = true;
    }

    private void RegisterCore(RaycastShapeId id, in RaycastShape2D shape)
    {
        if (_slots.ContainsKey(id))
        {
            ThrowHelper.ThrowDuplicateShape();
        }

        if (!shape.Bounds.IsValid)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(shape));
        }

        int slot = AcquireSlot();
        _entries[slot] = new ShapeEntry2D
        {
            Shape = shape,
            Id = id,
            Active = true,
            Occupied = true,
        };

        try
        {
            Aabb2D bounds = shape.Bounds;
            _grid.Insert(slot, in bounds);
        }
        catch
        {
            _entries[slot] = default;
            _freeSlots.Push(slot);
            throw;
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

    private static void EnsureVisitCapacity(int slots)
    {
        int[]? visited = s_visited2D;
        if (visited is null || visited.Length < slots)
        {
            s_visited2D = new int[Math.Max(slots, DefaultVisitCapacity)];
            s_queryId2D = 0;
        }
    }

    private static int NextQueryId()
    {
        int id = s_queryId2D + 1;
        if (id <= 0)
        {
            Array.Clear(s_visited2D!);
            id = 1;
        }

        s_queryId2D = id;
        return id;
    }

    private struct ShapeEntry2D
    {
        public RaycastShape2D Shape;
        public RaycastShapeId Id;
        public bool Active;
        public bool Occupied;
    }

    private ref struct QueryState2D : ISpatialHashItemVisitor<int>
    {
        private readonly ShapeEntry2D[] _entries;
        private readonly int[] _visited;
        private readonly int _queryId;
        private readonly Ray2D _ray;
        private readonly float _maxDistance;
        private readonly uint _layerMask;
        private readonly Span<RaycastHit2D> _results;
        private readonly int _maxHits;
        private readonly bool _closestOnly;
        private readonly bool _sortByDistance;
        private readonly bool _stopAtFirst;
        private readonly bool _ignoreTriggers;
        private readonly bool _ignoreBackfaces;
        private readonly bool _includeInactive;
        private RaycastHit2D _best;
        private float _bestDistance;
        private bool _hasBest;
        private int _count;

        public QueryState2D(
            ShapeEntry2D[] entries,
            int[] visited,
            int queryId,
            in RaycastQuery2D query,
            Span<RaycastHit2D> results)
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
            ref readonly ShapeEntry2D entry = ref _entries[slot];
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

            if (!RaycastIntersections.TryIntersect2D(in _ray, in entry.Shape, entry.Id, _maxDistance, out RaycastHit2D hit))
            {
                return true;
            }

            if (_ignoreBackfaces && Vec2.Dot(_ray.Direction, hit.Normal) > 0f)
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
                RaycastSort.Sort2D(_results[.._count]);
            }

            return _count;
        }
    }
}
