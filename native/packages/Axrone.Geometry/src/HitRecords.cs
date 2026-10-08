namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

/// <summary>
/// A single item a ray or segment reached, keyed by <see cref="RayHit.Distance"/> so a
/// query can keep the closest candidate without a second comparison pass.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
[SuppressMessage(
    "Design",
    "CA1036:Override methods on comparable types",
    Justification = "Ordering travels through IComparable; the record already supplies Equals and the ==/!= operators, so the four relational operators would only duplicate CompareTo.")]
public readonly record struct RayHit(SpatialItemId ItemId, float Distance) : IComparable<RayHit>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(RayHit other) => Distance.CompareTo(other.Distance);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"Hit(Id={ItemId.Value}, Distance={Distance:F4})");
}

/// <summary>
/// Outcome of a ray or segment query against a single candidate, laid out explicitly so
/// the whole record travels in one 16-byte slot of an intersection buffer. A miss is a
/// value, not a null: traversal writes results in place and never allocates.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 16)]
[SuppressMessage(
    "Performance",
    "CA1815:Override equals and operator equals on value types",
    Justification = "Traversal payload written in place and read through Kind/IsHit; a generated Equals would box and defeat the 16-byte slot, so equality stays out of the surface.")]
public readonly struct RayIntersection
{
    [FieldOffset(0)]
    private readonly IntersectionKind _kind;

    [FieldOffset(4)]
    private readonly SpatialItemId _itemId;

    [FieldOffset(8)]
    private readonly float _distance;

    private RayIntersection(IntersectionKind kind, SpatialItemId itemId, float distance)
    {
        _kind = kind;
        _itemId = itemId;
        _distance = distance;
    }

    /// <summary>Whether the query reached a candidate, i.e. <see cref="Kind"/> is <see cref="IntersectionKind.Hit"/>.</summary>
    public bool IsHit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind == IntersectionKind.Hit;
    }

    /// <summary>Whether the query hit or missed the candidate.</summary>
    public IntersectionKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _kind;
    }

    /// <summary>Identity of the hit item; <see cref="SpatialItemId.Invalid"/> on a miss.</summary>
    public SpatialItemId ItemId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _itemId;
    }

    /// <summary>Distance from the ray origin to the hit point, in world units.</summary>
    public float Distance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _distance;
    }

    /// <summary>A miss: no candidate was reached, so the payload fields are meaningless.</summary>
    public static RayIntersection None
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(IntersectionKind.None, SpatialItemId.Invalid, 0F);
    }

    /// <summary>Builds a hit that reached <paramref name="itemId"/> at <paramref name="distance"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RayIntersection FromHit(SpatialItemId itemId, float distance) =>
        new(IntersectionKind.Hit, itemId, distance);
}

/// <summary>
/// Detailed hit against one triangle: the barycentric pair <see cref="U"/>/<see cref="V"/>
/// locates the hit inside the triangle, which texture and interpolation lookups need.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TriangleHit3(float Distance, float U, float V, Vec3 Normal, Vec3 Point)
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"TriangleHit(Dist={Distance:F4}, UV=<{U:F3},{V:F3}>, Pt={Point})");
}

/// <summary>
/// Contact point of a swept volume against a candidate, expressed as a normalized time
/// along the sweep direction plus the surface normal and world-space contact point.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SweepHit3(float Time, Vec3 Normal, Vec3 Point)
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"SweepHit(Time={Time:F4}, Normal={Normal}, Pt={Point})");
}

/// <summary>
/// Outcome of a swept-volume query against a single candidate. Like
/// <see cref="RayIntersection"/> a miss is a value, so the hot sweep loop stays
/// branch-free and allocation-free.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SweepResult(SweepKind Kind, SweepHit3 Hit)
{
    /// <summary>Whether the sweep reached a candidate, i.e. <see cref="Kind"/> is <see cref="SweepKind.Hit"/>.</summary>
    public bool IsHit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Kind == SweepKind.Hit;
    }

    /// <summary>A miss: the swept volume never reached the candidate.</summary>
    public static SweepResult Miss
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(SweepKind.Miss, default);
    }

    /// <summary>Builds a hit that contacted the candidate at <paramref name="hit"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SweepResult FromHit(in SweepHit3 hit) => new(SweepKind.Hit, hit);
}

/// <summary>
/// One entry of a nearest-neighbour answer, ordered by <see cref="DistanceSq"/> so a
/// k-nearest query can keep its running worst-entry heap comparator free of square roots.
/// <typeparam name="TUserData">Payload type carried alongside the item identity.</typeparam>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
[SuppressMessage(
    "Performance",
    "CA1815:Override equals and operator equals on value types",
    Justification = "Neighbour entries are ordered through IComparable only; a generated Equals would box on the managed TUserData field and slow the k-nearest heap.")]
[SuppressMessage(
    "Design",
    "CA1036:Override methods on comparable types",
    Justification = "Neighbour ordering travels through IComparable; a relational operator set would only duplicate CompareTo over DistanceSq.")]
public readonly struct KnnResult<TUserData> : IComparable<KnnResult<TUserData>>
{
    private readonly SpatialItemId _itemId;
    private readonly TUserData _userData;
    private readonly DistanceSquared _distanceSq;

    /// <summary>Creates a result for <paramref name="itemId"/> carrying <paramref name="userData"/>.</summary>
    public KnnResult(SpatialItemId itemId, TUserData userData, DistanceSquared distanceSq)
    {
        _itemId = itemId;
        _userData = userData;
        _distanceSq = distanceSq;
    }

    /// <summary>Identity of the neighbour.</summary>
    public SpatialItemId ItemId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _itemId;
    }

    /// <summary>Caller payload associated with the neighbour.</summary>
    public TUserData UserData
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _userData;
    }

    /// <summary>Squared distance from the query point to the neighbour.</summary>
    public DistanceSquared DistanceSq
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _distanceSq;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(KnnResult<TUserData> other) => _distanceSq.CompareTo(other._distanceSq);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"Knn(Id={_itemId.Value}, DistSq={_distanceSq.Value:F4})");
}