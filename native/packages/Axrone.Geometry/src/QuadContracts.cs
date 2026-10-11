namespace Axrone.Geometry;

/// <summary>
/// Callback invoked for every point a 2D range query reaches. The visitor is a
/// generic type parameter at the query site, so a traversal closes over the
/// callback type rather than a delegate instance.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
public interface IQuadVisitor<T> where T : unmanaged
{
    /// <summary>Reports one point inside the range.</summary>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool Visit(in Vec2 point, in T value);

    /// <summary>Clears the accumulated state so a retried traversal restarts clean.</summary>
    void Reset();
}

/// <summary>
/// Records what a quad tree did: inserts, removes, subdivisions and queries.
/// The sink is a static-abstract contract so the tree stays instrumented
/// without a delegate, an interface instance, or a counter object per visit.
/// </summary>
public interface IQuadMetricsSink
{
    /// <summary>Records that one element was added.</summary>
    static abstract void RecordInsert();

    /// <summary>Records that one element was removed.</summary>
    static abstract void RecordRemove();

    /// <summary>Records that one node was split.</summary>
    static abstract void RecordSubdivision();

    /// <summary>Records one range query and how many points it reported.</summary>
    static abstract void RecordQuery(int count);
}

/// <summary>Discards every counter, which is what production builds want.</summary>
public readonly struct NullQuadMetricsSink : IQuadMetricsSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordInsert()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordRemove()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordSubdivision()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordQuery(int count)
    {
    }
}

/// <summary>
/// Read half of a 2D point index: range scans and nearest search over the
/// fixed root bounds the tree was built with.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
public interface IQuadReader<T> where T : unmanaged
{
    /// <summary>Number of live elements in the tree.</summary>
    int Count { get; }

    /// <summary>Fixed root bounds the tree was built with.</summary>
    Aabb2D Bounds { get; }

    /// <summary>Reports every point inside <paramref name="range"/> to a visitor.</summary>
    void QueryRange<TVisitor>(in Aabb2D range, ref TVisitor visitor)
        where TVisitor : IQuadVisitor<T>, allows ref struct;

    /// <summary>Returns the nearest element to <paramref name="target"/>, or an empty answer.</summary>
    NearestQueryResult<T> FindNearest(in Vec2 target);

    /// <summary>Returns the nearest element to <paramref name="target"/> through out-params.</summary>
    bool FindNearest(in Vec2 target, out Vec2 foundPoint, out T foundValue, out float distanceSquared);
}

/// <summary>
/// Write half of a 2D point index, kept apart from reads so a consumer that
/// only queries can never trigger a structural change.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
public interface IQuadWriter<T> where T : unmanaged
{
    /// <summary>Adds a point; <see langword="false"/> when it falls outside the root bounds.</summary>
    bool TryInsert(in Vec2 point, in T value);

    /// <summary>Drops a point; <see langword="false"/> when no such point is stored.</summary>
    bool TryRemove(in Vec2 point, in T value);

    /// <summary>Adds a point; <see langword="false"/> when it falls outside the root bounds.</summary>
    bool Insert(in Vec2 point, in T value);

    /// <summary>Drops a point; <see langword="false"/> when no such point is stored.</summary>
    bool Remove(in Vec2 point, in T value);

    /// <summary>Drops every element, keeping the pool for reuse.</summary>
    void Clear();
}

/// <summary>
/// Combined contract of a 2D point index: handled reads, handled writes and
/// the shared bulk-drain lifecycle. The reader and writer halves are spelled
/// out on this one interface (rather than reusing the 3D box-keyed contracts)
/// because points and boxes never mix: a 2D point query has no fattening
/// margin and no generational handle.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
public interface IQuadIndex<T> : IQuadReader<T>, IQuadWriter<T>, ISpatialLifecycle, IDisposable, IAsyncDisposable
    where T : unmanaged
{
}

/// <summary>Visitor buffering values into a caller-provided span; a full span stops the walk.</summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
public ref struct SpanBufferVisitor<T> : IQuadVisitor<T> where T : unmanaged
{
    private readonly Span<T> _destination;

    /// <summary>Values collected so far.</summary>
    public int Count { get; private set; }

    /// <summary>Creates a visitor over <paramref name="destination"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanBufferVisitor(Span<T> destination)
    {
        _destination = destination;
        Count = 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Visit(in Vec2 point, in T value)
    {
        if ((uint)Count < (uint)_destination.Length)
        {
            _destination[Count++] = value;
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Count = 0;
}

/// <summary>Visitor buffering point payloads into a caller-provided span; a full span stops the walk.</summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
public ref struct ElementSpanVisitor<T> : IQuadVisitor<T> where T : unmanaged
{
    private readonly Span<SpatialElement<T>> _destination;

    /// <summary>Elements collected so far.</summary>
    public int Count { get; private set; }

    /// <summary>Creates a visitor over <paramref name="destination"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ElementSpanVisitor(Span<SpatialElement<T>> destination)
    {
        _destination = destination;
        Count = 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Visit(in Vec2 point, in T value)
    {
        if ((uint)Count < (uint)_destination.Length)
        {
            _destination[Count++] = new SpatialElement<T>(point, value);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Count = 0;
}
