namespace Axrone.Geometry;

/// <summary>
/// Callback invoked for every item a box or sphere query reaches. The visitor
/// is a generic type parameter at the query site, so a traversal closes over
/// the callback type rather than a delegate instance.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
public interface IOctreeVisitor<T> where T : unmanaged
{
    /// <summary>Reports one overlapping item.</summary>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnVisit(OctreeHandle handle, in Aabb3D bounds, in T value);
}

/// <summary>
/// Callback invoked for every item a ray reaches together with its distance.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
public interface IRaycastVisitor<T> where T : unmanaged
{
    /// <summary>Reports one item the ray reached.</summary>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnHit(OctreeHandle handle, in Aabb3D bounds, in T value, Distance distance);
}

/// <summary>
/// Read half of a 3D box index: box, sphere and ray queries plus nearest
/// search over the fixed root domain the tree was built with.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
public interface IOctreeReader<T> where T : unmanaged
{
    /// <summary>Number of live items in the tree.</summary>
    uint Count { get; }

    /// <summary>Fixed root domain the tree was built with.</summary>
    Aabb3D RootBounds { get; }

    /// <summary>Reports every item overlapping <paramref name="queryBox"/>; <see langword="false"/> on early-out.</summary>
    bool QueryBox<TVisitor>(in Aabb3D queryBox, ref TVisitor visitor)
        where TVisitor : IOctreeVisitor<T>, allows ref struct;

    /// <summary>Reports every item the ray reaches within <paramref name="maxDistance"/>; <see langword="false"/> on early-out.</summary>
    bool Raycast<TVisitor>(in Ray3D ray, Distance maxDistance, ref TVisitor visitor)
        where TVisitor : IRaycastVisitor<T>, allows ref struct;

    /// <summary>Reports every item overlapping <paramref name="sphere"/>; <see langword="false"/> on early-out.</summary>
    bool QuerySphere<TVisitor>(in BoundingSphere3 sphere, ref TVisitor visitor)
        where TVisitor : IOctreeVisitor<T>, allows ref struct;

    /// <summary>Returns the nearest item to <paramref name="point"/> within <paramref name="maxDistance"/>.</summary>
    NearestNeighborResult<T> FindNearest(in Vec3 point, Distance maxDistance);
}

/// <summary>
/// Write half of a 3D box index, kept apart from reads so a consumer that
/// only queries can never trigger a structural change.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
public interface IOctreeWriter<T> where T : unmanaged
{
    /// <summary>Adds an item and returns its handle; out-of-domain and disposed inputs throw.</summary>
    OctreeHandle Insert(in Aabb3D bounds, in T value);

    /// <summary>Adds an item, reporting the outcome instead of throwing.</summary>
    InsertResult TryInsert(in Aabb3D bounds, in T value);

    /// <summary>Drops an item; <see langword="false"/> when the handle names nothing live.</summary>
    bool Remove(OctreeHandle handle);

    /// <summary>Replaces an item's bounds and payload, relocating it when it outgrows its node.</summary>
    bool Update(OctreeHandle handle, in Aabb3D newBounds, in T newValue);

    /// <summary>Drops every item, keeping the pools for reuse.</summary>
    void Clear();
}

/// <summary>
/// Combined contract of a 3D box index: handled reads, handled writes and the
/// shared bulk-drain lifecycle.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
public interface IOctreeIndex<T> : IOctreeReader<T>, IOctreeWriter<T>, ISpatialLifecycle, IDisposable, IAsyncDisposable
    where T : unmanaged
{
}

/// <summary>Visitor buffering items into a caller-provided span; a full span stops the walk.</summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
public ref struct SpanCollectorVisitor<T> : IOctreeVisitor<T> where T : unmanaged
{
    private readonly Span<SpatialItem<T>> _buffer;
    private int _count;

    /// <summary>Items collected so far.</summary>
    public readonly int Count => _count;

    /// <summary>Creates a visitor over <paramref name="buffer"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanCollectorVisitor(Span<SpatialItem<T>> buffer)
    {
        _buffer = buffer;
        _count = 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OnVisit(OctreeHandle handle, in Aabb3D bounds, in T value)
    {
        if ((uint)_count < (uint)_buffer.Length)
        {
            _buffer[_count++] = new SpatialItem<T>(handle, bounds, value);
            return true;
        }

        return false;
    }
}

/// <summary>Visitor counting overlaps without retaining anything.</summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
public ref struct CounterVisitor<T> : IOctreeVisitor<T> where T : unmanaged
{
    private int _count;

    /// <summary>Overlaps seen so far.</summary>
    public readonly int Count => _count;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OnVisit(OctreeHandle handle, in Aabb3D bounds, in T value)
    {
        _count++;
        return true;
    }
}

/// <summary>Ray visitor keeping the nearest contact; the walk itself never stops early.</summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
public ref struct NearestHitVisitor<T> : IRaycastVisitor<T> where T : unmanaged
{
    /// <summary>Nearest item so far.</summary>
    public SpatialItem<T> NearestItem { get; private set; }

    /// <summary>Distance of the nearest contact.</summary>
    public Distance Distance { get; private set; }

    /// <summary>Whether any contact was seen.</summary>
    public bool HasHit { get; private set; }

    /// <summary>Creates an empty visitor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NearestHitVisitor()
    {
        Distance = Distance.PositiveInfinity;
        HasHit = false;
        NearestItem = default;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OnHit(OctreeHandle handle, in Aabb3D bounds, in T value, Distance distance)
    {
        if (distance < Distance)
        {
            Distance = distance;
            NearestItem = new SpatialItem<T>(handle, bounds, value);
            HasHit = true;
        }

        return true;
    }
}
