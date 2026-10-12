namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Reports one item a box or sphere query reaches. The sink is a generic type
/// parameter at the query site, so a traversal closes over the callback type
/// rather than a delegate instance.
/// </summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
public interface ISpatialQuerySink<T>
{
    /// <summary>Reports one overlapping item.</summary>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnHit(SpatialHandle handle, in Aabb3D bounds, ref readonly T payload);
}

/// <summary>
/// Reports one item a ray reaches together with its distance. The sink owns
/// no search limit here; the closest-hit reduction lives in
/// <c>RaycastClosest</c>, which keeps this traversal simple until the raycast
/// pass is revisited.
/// </summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
public interface ISpatialRaySink<T>
{
    /// <summary>Reports one item the ray reached.</summary>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnHit(SpatialHandle handle, in Aabb3D bounds, ref readonly T payload, Distance distance);
}

/// <summary>
/// Reports one overlapping pair a self-query discovers. Each pair is reported
/// once, so a broad-phase contact pass consumes it without de-duplicating.
/// </summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
public interface ISpatialPairSink<T>
{
    /// <summary>Reports one overlapping pair.</summary>
    /// <returns><see langword="true"/> to keep traversing; <see langword="false"/> to stop early.</returns>
    bool OnPair(SpatialHandle handleA, ref readonly T payloadA, SpatialHandle handleB, ref readonly T payloadB);
}

/// <summary>One row of a batch insert: the tight bounds plus its payload.</summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
public readonly record struct SpatialEntry<T>(Aabb3D Bounds, T Payload);

/// <summary>Outcome of a handled bounds update.</summary>
public enum TreeUpdateOutcome : byte
{
    /// <summary>The fat box still covers the new bounds; the tree did not move.</summary>
    BoundsUnchanged = 0,

    /// <summary>The item was re-inserted under refreshed fat bounds.</summary>
    Reinserted = 1,

    /// <summary>The handle names no live item.</summary>
    HandleNotFound = 2,

    /// <summary>The tree is disposed.</summary>
    Disposed = 3,
}

/// <summary>Verdict of <c>UpdateEx</c>: what happened plus which handle it happened to.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TreeUpdateResult(TreeUpdateOutcome Outcome, SpatialHandle Handle)
{
    /// <summary>Whether the item is live under this handle after the call.</summary>
    public bool IsSuccessful
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Outcome is TreeUpdateOutcome.BoundsUnchanged or TreeUpdateOutcome.Reinserted;
    }
}

/// <summary>Outcome of a closest-ray query.</summary>
public enum RaycastStatus : byte
{
    /// <summary>The ray reached nothing.</summary>
    Miss = 0,

    /// <summary>The ray reached an item.</summary>
    Hit = 1,
}

/// <summary>Closest hit of a ray query, or a miss value when the ray reached nothing.</summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct RaycastResult<T>
{
    private readonly RaycastStatus _status;
    private readonly SpatialHandle _handle;
    private readonly Distance _distance;
    private readonly Aabb3D _bounds;
    private readonly T _payload;

    /// <summary>Builds a hit record.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RaycastResult(SpatialHandle handle, Distance distance, in Aabb3D bounds, T payload)
    {
        _status = RaycastStatus.Hit;
        _handle = handle;
        _distance = distance;
        _bounds = bounds;
        _payload = payload;
    }

    private RaycastResult(RaycastStatus status)
    {
        _status = status;
        _handle = SpatialHandle.Invalid;
        _distance = Distance.PositiveInfinity;
        _bounds = default;
        _payload = default!;
    }

    /// <summary>Whether the ray reached an item.</summary>
    public RaycastStatus Status
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _status;
    }

    /// <summary>Handle of the closest item; invalid on a miss.</summary>
    public SpatialHandle Handle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _handle;
    }

    /// <summary>Distance to the closest contact; infinity on a miss.</summary>
    public Distance Distance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _distance;
    }

    /// <summary>Tight bounds of the closest item.</summary>
    public Aabb3D Bounds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bounds;
    }

    /// <summary>Payload of the closest item.</summary>
    public T Payload
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _payload;
    }

    /// <summary>A miss: the ray reached nothing.</summary>
    public static RaycastResult<T> Miss => new(RaycastStatus.Miss);

    /// <summary>Whether the ray reached an item.</summary>
    public bool HasHit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Status == RaycastStatus.Hit;
    }
}

/// <summary>
/// Tuning knobs of a generational spatial tree: how many slots the node pool
/// opens with, how much slack every stored box carries, and how that slack
/// scales with the speed an item moves at. Guards live in the properties, so an
/// invalid combination is rejected at assignment rather than at the first
/// traversal that trips over it.
/// </summary>
public sealed class SpatialTreeOptions
{
    /// <summary>Slots the node pool opens with; at least <see cref="TreeCapacity.MinimumCapacity"/>.</summary>
    public TreeCapacity InitialCapacity { get; init; } = new(1024);

    /// <summary>Per-side slack grown onto every stored box; never negative.</summary>
    public float FatMargin
    {
        get => field;
        init => field = (value < 0.0f || float.IsNaN(value)) ? ThrowHelper.ThrowInvalidFatMargin() : value;
    } = 0.2f;

    /// <summary>Scale applied to the margin for moving items; at least one.</summary>
    public float VelocityMultiplier
    {
        get => field;
        init => field = (value < 1.0f || float.IsNaN(value)) ? ThrowHelper.ThrowInvalidVelocityMultiplier() : value;
    } = 2.0f;
}

/// <summary>
/// Combined contract of a generational spatial index: handled reads, handled
/// writes and disposal. The reader and writer halves are spelled out on this
/// one interface (rather than inheriting the <c>SpatialItemId</c>-keyed
/// <see cref="ISpatialReader{TUserData}"/>/<see cref="ISpatialWriter{TUserData}"/>)
/// because generational handles and slot identities never mix: a handle that
/// survived a recycle must fail closed, which a plain slot id cannot express.
/// </summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
public interface ISpatialIndex<T> : IDisposable
{
    /// <summary>Number of live items in the tree.</summary>
    int Count { get; }

    /// <summary>Slots the node pool currently holds.</summary>
    TreeCapacity Capacity { get; }

    /// <summary>Returns the tight bounds an item was last updated with.</summary>
    bool TryGetBounds(SpatialHandle handle, out Aabb3D bounds);

    /// <summary>Returns the payload an item was inserted with.</summary>
    bool TryGetPayload(SpatialHandle handle, [MaybeNullWhen(false)] out T payload);

    /// <summary>Reports every item whose bounds overlap <paramref name="box"/>.</summary>
    /// <returns>Number of overlaps reported.</returns>
    int Query<TSink>(in Aabb3D box, ref TSink sink)
        where TSink : ISpatialQuerySink<T>, allows ref struct;

    /// <summary>Reports every item whose bounds overlap <paramref name="sphere"/>.</summary>
    /// <returns>Number of overlaps reported.</returns>
    int QuerySphere<TSink>(in BoundingSphere3 sphere, ref TSink sink)
        where TSink : ISpatialQuerySink<T>, allows ref struct;

    /// <summary>
    /// Reports every item the ray reaches within <paramref name="maxDistance"/>
    /// using the simple scalar slab test on the professional box; the full
    /// raycast overhaul reuses this traversal shape with a richer primitive.
    /// </summary>
    /// <returns>Number of hits reported.</returns>
    int Raycast<TSink>(in Ray3D ray, Distance maxDistance, ref TSink sink)
        where TSink : ISpatialRaySink<T>, allows ref struct;

    /// <summary>Reports each pair of overlapping items once.</summary>
    void QueryOverlaps<TSink>(ref TSink sink)
        where TSink : ISpatialPairSink<T>, allows ref struct;

    /// <summary>Adds an item and returns its generational handle.</summary>
    SpatialHandle Insert(in Aabb3D bounds, T payload);

    /// <summary>Adds a batch of items; returns how many were added.</summary>
    int InsertRange(params ReadOnlySpan<SpatialEntry<T>> entries);

    /// <summary>Adds a batch of items, writing one handle per entry.</summary>
    int InsertRange(ReadOnlySpan<SpatialEntry<T>> entries, Span<SpatialHandle> handles);

    /// <summary>Replaces an item's bounds; <see langword="true"/> when it was re-inserted.</summary>
    bool Update(SpatialHandle handle, in Aabb3D newBounds, Vec3 displacement = default);

    /// <summary>Replaces an item's bounds, reporting what happened.</summary>
    TreeUpdateResult UpdateEx(SpatialHandle handle, in Aabb3D newBounds, Vec3 displacement = default);

    /// <summary>Drops an item; <see langword="false"/> when the handle names nothing live.</summary>
    bool Remove(SpatialHandle handle);

    /// <summary>Drops every item, keeping the pool for reuse.</summary>
    void Clear();
}
