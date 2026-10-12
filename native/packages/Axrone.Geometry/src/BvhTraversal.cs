namespace Axrone.Geometry;

/// <summary>
/// One node of a linearized BVH: the bounds corners inline, then either the
/// second-child slot (internal node) or the first primitive index (leaf), plus
/// how many primitives the leaf covers. The 32-byte explicit layout keeps a
/// node to one cache line.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 32)]
public readonly struct LinearBvhNode
{
    /// <summary>Inclusive lower corner.</summary>
    [FieldOffset(0)]
    public readonly Vec3 Min;

    /// <summary>Second-child slot on internal nodes, first primitive index on leaves.</summary>
    [FieldOffset(12)]
    public readonly NodeIndex ChildOrIndex;

    /// <summary>Inclusive upper corner.</summary>
    [FieldOffset(16)]
    public readonly Vec3 Max;

    /// <summary>Primitives covered; zero on internal nodes.</summary>
    [FieldOffset(28)]
    public readonly int PrimitiveCount;

    /// <summary>Creates a node over <paramref name="bounds"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LinearBvhNode(in Aabb3D bounds, NodeIndex childOrIndex, int primitiveCount)
    {
        Min = bounds.Min;
        ChildOrIndex = childOrIndex;
        Max = bounds.Max;
        PrimitiveCount = primitiveCount;
    }

    /// <summary>Whether this node is a leaf, which is exactly when it covers primitives.</summary>
    public bool IsLeaf
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => PrimitiveCount > 0;
    }

    /// <summary>
    /// Tests the ray against the stored corners with the scalar slab method,
    /// honouring the ray's distance window: the entry is clamped to
    /// <see cref="Ray.MinDistance"/> and a hit needs overlap within
    /// <see cref="Ray.MaxDistance"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in Ray ray, out Distance enterDistance)
    {
        float tx1 = (Min.X - ray.Origin.X) * ray.InvDirection.X;
        float tx2 = (Max.X - ray.Origin.X) * ray.InvDirection.X;
        float tmin = MathF.Min(tx1, tx2);
        float tmax = MathF.Max(tx1, tx2);

        float ty1 = (Min.Y - ray.Origin.Y) * ray.InvDirection.Y;
        float ty2 = (Max.Y - ray.Origin.Y) * ray.InvDirection.Y;
        tmin = MathF.Max(tmin, MathF.Min(ty1, ty2));
        tmax = MathF.Min(tmax, MathF.Max(ty1, ty2));

        float tz1 = (Min.Z - ray.Origin.Z) * ray.InvDirection.Z;
        float tz2 = (Max.Z - ray.Origin.Z) * ray.InvDirection.Z;
        tmin = MathF.Max(tmin, MathF.Min(tz1, tz2));
        tmax = MathF.Min(tmax, MathF.Max(tz1, tz2));

        float enter = MathF.Max(tmin, ray.MinDistance.Value);
        enterDistance = new Distance(enter);
        return tmax >= enter && tmin <= ray.MaxDistance.Value;
    }
}

/// <summary>
/// Decides how a BVH traversal prunes and what a primitive contact does to
/// the running search. Static-abstract so the traversal closes over the
/// policy type with no virtual dispatch.
/// </summary>
/// <typeparam name="TSelf">The policy type itself.</typeparam>
public interface IRayTraversalPolicy<TSelf>
    where TSelf : struct, IRayTraversalPolicy<TSelf>
{
    /// <summary>Whether a node entered at this distance can be skipped.</summary>
    static abstract bool ShouldPrune(Distance nodeEnterDistance, Distance currentMaxDistance);

    /// <summary>Folds one primitive contact into the running answer; <see langword="false"/> stops the walk.</summary>
    static abstract bool OnPrimitiveHit(ref PrimitiveHit closestHit, ref Distance maxDistance, in PrimitiveHit candidateHit);

    /// <summary>Whether the first contact ends the traversal.</summary>
    static abstract bool TerminatesOnFirstHit { get; }
}

/// <summary>Nearest-contact policy: every contact tightens the search window.</summary>
public readonly struct ClosestHitPolicy : IRayTraversalPolicy<ClosestHitPolicy>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldPrune(Distance nodeEnterDistance, Distance currentMaxDistance) =>
        nodeEnterDistance > currentMaxDistance;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OnPrimitiveHit(ref PrimitiveHit closestHit, ref Distance maxDistance, in PrimitiveHit candidateHit)
    {
        if (candidateHit.Distance < maxDistance)
        {
            closestHit = candidateHit;
            maxDistance = candidateHit.Distance;
        }

        return true;
    }

    /// <inheritdoc/>
    public static bool TerminatesOnFirstHit => false;
}

/// <summary>Occlusion policy: the first contact wins and the walk stops.</summary>
public readonly struct AnyHitPolicy : IRayTraversalPolicy<AnyHitPolicy>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldPrune(Distance nodeEnterDistance, Distance currentMaxDistance) =>
        nodeEnterDistance > currentMaxDistance;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OnPrimitiveHit(ref PrimitiveHit closestHit, ref Distance maxDistance, in PrimitiveHit candidateHit)
    {
        closestHit = candidateHit;
        return false;
    }

    /// <inheritdoc/>
    public static bool TerminatesOnFirstHit => true;
}

/// <summary>
/// Sink collecting traversal contacts into caller-owned storage. A
/// <see langword="ref"/> struct may implement it, so accumulation never boxes.
/// </summary>
/// <typeparam name="TSelf">The collector type itself.</typeparam>
public interface ITraversalCollector<TSelf>
    where TSelf : allows ref struct
{
    /// <summary>Furthest contact still accepted.</summary>
    Distance MaxDistance { get; }

    /// <summary>Whether any contact was collected.</summary>
    bool HasHit { get; }

    /// <summary>Most recent contact, or a miss value when empty.</summary>
    PrimitiveHit Hit { get; }

    /// <summary>Collects one contact; <see langword="false"/> stops the walk.</summary>
    bool OnHit(in PrimitiveHit hit);
}

/// <summary>
/// Collector buffering contacts into a caller-provided span; a full buffer
/// stops the walk rather than growing.
/// </summary>
public ref struct HitBufferCollector : ITraversalCollector<HitBufferCollector>
{
    private readonly Span<PrimitiveHit> _buffer;
    private int _count;
    private Distance _maxDistance;

    /// <summary>Creates a collector over <paramref name="buffer"/> with an explicit limit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HitBufferCollector(Span<PrimitiveHit> buffer, Distance maxDistance)
    {
        _buffer = buffer;
        _count = 0;
        _maxDistance = maxDistance;
    }

    /// <summary>Creates a collector over <paramref name="buffer"/> with no far limit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HitBufferCollector(Span<PrimitiveHit> buffer)
        : this(buffer, Distance.PositiveInfinity)
    {
    }

    /// <summary>Contacts collected so far.</summary>
    public readonly int Count => _count;

    /// <summary>Collected contacts in arrival order.</summary>
    public readonly ReadOnlySpan<PrimitiveHit> Hits => _buffer[.._count];

    /// <inheritdoc/>
    public readonly Distance MaxDistance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _maxDistance;
    }

    /// <inheritdoc/>
    public readonly bool HasHit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count > 0;
    }

    /// <inheritdoc/>
    public readonly PrimitiveHit Hit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count > 0 ? _buffer[_count - 1] : PrimitiveHit.None;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OnHit(in PrimitiveHit hit)
    {
        if ((uint)_count < (uint)_buffer.Length)
        {
            _buffer[_count++] = hit;
            return true;
        }

        return false;
    }
}

/// <summary>
/// Observer notified once per traversal with its counters. A
/// <see langword="ref"/> struct may implement it, so instrumentation never boxes.
/// </summary>
/// <typeparam name="TSelf">The observer type itself.</typeparam>
public interface ITraversalObserver<TSelf>
    where TSelf : allows ref struct
{
    /// <summary>Reports one finished traversal.</summary>
    void OnTraversalComplete(int nodesVisited, int primitivesTested, bool hitOccurred);
}

/// <summary>Discards every traversal counter; what production builds want.</summary>
public readonly struct NullTraversalObserver : ITraversalObserver<NullTraversalObserver>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnTraversalComplete(int nodesVisited, int primitivesTested, bool hitOccurred)
    {
    }
}

/// <summary>Fixed traversal stack storage; used by the leaf enumerator.</summary>
[InlineArray(64)]
internal struct TraversalStack64
{
    private int _element;
}
