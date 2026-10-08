namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Axis-aligned 3D bounding box expressed as inclusive minimum and maximum corners.
/// The two corners are stored as plain <see cref="Vec3"/> triples rather than as fields:
/// broad-phase code hands them to culling kernels and SIMD loads wholesale, and the
/// scalar corners keep the layout stable for interop without exposing an implementation
/// detail as part of the contract.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 16)]
public readonly partial struct Aabb3D : IEquatable<Aabb3D>
{
    private readonly Vec3 _min;
    private readonly Vec3 _max;

    /// <summary>
    /// An inverted box that any merge or expansion absorbs: every corner is infinite in
    /// the opposing direction, so it contributes nothing to the result.
    /// </summary>
    public static Aabb3D Empty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => new(new Vec3(float.PositiveInfinity), new Vec3(float.NegativeInfinity));
    }

    /// <summary>A degenerate box at the origin; valid, with zero size, area and volume.</summary>
    public static Aabb3D Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => new(Vec3.Zero, Vec3.Zero);
    }

    /// <summary>A cube spanning one unit centred on the origin.</summary>
    public static Aabb3D UnitCube
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => new(new Vec3(-0.5F), new Vec3(0.5F));
    }

    /// <summary>Creates a box from its inclusive corners.</summary>
    /// <param name="min">The inclusive lower corner.</param>
    /// <param name="max">The inclusive upper corner.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Aabb3D(Vec3 min, Vec3 max)
    {
        _min = min;
        _max = max;
    }

    /// <summary>Creates a box from its inclusive corner components.</summary>
    /// <param name="minX">The smallest X coordinate the box spans.</param>
    /// <param name="minY">The smallest Y coordinate the box spans.</param>
    /// <param name="minZ">The smallest Z coordinate the box spans.</param>
    /// <param name="maxX">The largest X coordinate the box spans.</param>
    /// <param name="maxY">The largest Y coordinate the box spans.</param>
    /// <param name="maxZ">The largest Z coordinate the box spans.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Aabb3D(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        _min = new Vec3(minX, minY, minZ);
        _max = new Vec3(maxX, maxY, maxZ);
    }

    /// <summary>Inclusive lower corner; the smallest coordinates the box spans.</summary>
    public Vec3 Min
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _min;
    }

    /// <summary>Inclusive upper corner; the largest coordinates the box spans.</summary>
    public Vec3 Max
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _max;
    }

    /// <summary>Midpoint of the box.</summary>
    public Vec3 Center
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Min + Max) * 0.5F;
    }

    /// <summary>Half the box size per axis; negative for any inverted axis.</summary>
    public Vec3 Extents
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Max - Min) * 0.5F;
    }

    /// <summary>Full size per axis; negative for any inverted axis.</summary>
    public Vec3 Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Max - Min;
    }

    /// <summary>Whether the corners are ordered, that is <see cref="Min"/> does not exceed <see cref="Max"/> on any axis.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;
    }

    /// <summary>Whether the corners are inverted; the negation of <see cref="IsValid"/>.</summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => !IsValid;
    }

    /// <summary>
    /// Creates a box from a centre and half-sizes, taking the magnitude of the extents so a
    /// negative component still grows the box outward instead of inverting it.
    /// </summary>
    /// <param name="center">The midpoint of the box.</param>
    /// <param name="extents">The half-size per axis; sign is ignored.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Aabb3D FromCenterExtents(Vec3 center, Vec3 extents)
    {
        Vec3 e = new(MathF.Abs(extents.X), MathF.Abs(extents.Y), MathF.Abs(extents.Z));
        return new(center - e, center + e);
    }

    /// <summary>Creates the smallest box that encloses the sphere.</summary>
    /// <param name="sphere">The sphere to enclose.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Aabb3D FromSphere(in BoundingSphere3 sphere)
    {
        Vec3 r = new(sphere.Radius);
        return new(sphere.Center - r, sphere.Center + r);
    }

    /// <summary>
    /// Creates the smallest box containing every point, accumulating the component-wise
    /// bounds from the inverted seeds of <see cref="Empty"/> so a single pass suffices.
    /// </summary>
    /// <param name="points">The points to enclose; an empty span yields <see cref="Empty"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D CreateFromPoints(params ReadOnlySpan<Vec3> points)
    {
        if (points.Length == 0)
        {
            return Empty;
        }

        Vec3 min = new(float.PositiveInfinity);
        Vec3 max = new(float.NegativeInfinity);

        for (int i = 0; i < points.Length; ++i)
        {
            min = Vec3.Min(min, points[i]);
            max = Vec3.Max(max, points[i]);
        }

        return new(min, max);
    }

    /// <summary>Returns the smallest box containing both boxes.</summary>
    /// <param name="a">The first box to union.</param>
    /// <param name="b">The second box to union.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Aabb3D CreateMerged(in Aabb3D a, in Aabb3D b) => new(Vec3.Min(a._min, b._min), Vec3.Max(a._max, b._max));

    /// <summary>Determines whether the two boxes share at least one point; touching faces count as overlapping.</summary>
    /// <param name="other">The candidate overlapping box.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Overlaps(in Aabb3D other) =>
        Min.X <= other.Max.X &&
        Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y &&
        Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z &&
        Max.Z >= other.Min.Z;

    /// <summary>Determines whether the other box lies wholly inside this one; shared boundaries count as inside.</summary>
    /// <param name="other">The candidate inner box.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Contains(in Aabb3D other) =>
        other.Min.X >= Min.X &&
        other.Max.X <= Max.X &&
        other.Min.Y >= Min.Y &&
        other.Max.Y <= Max.Y &&
        other.Min.Z >= Min.Z &&
        other.Max.Z <= Max.Z;

    /// <summary>Classifies the relationship between the two boxes in one pass over the axes.</summary>
    /// <param name="other">The candidate box to classify against this one.</param>
    /// <returns>
    /// <see cref="ContainmentType.Disjoint"/> when the boxes share no point,
    /// <see cref="ContainmentType.Contains"/> when this box encloses <paramref name="other"/>,
    /// otherwise <see cref="ContainmentType.Intersects"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ContainmentType ContainsWithState(in Aabb3D other) =>
        !Overlaps(other) ? ContainmentType.Disjoint :
        Contains(other) ? ContainmentType.Contains :
        ContainmentType.Intersects;

    /// <summary>Determines whether the point lies inside the box; boundary points count as inside.</summary>
    /// <param name="point">The point to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Contains(in Vec3 point) =>
        point.X >= Min.X &&
        point.X <= Max.X &&
        point.Y >= Min.Y &&
        point.Y <= Max.Y &&
        point.Z >= Min.Z &&
        point.Z <= Max.Z;

    /// <summary>
    /// Computes the overlap of the two boxes. Overlap of exactly zero thickness is still a
    /// hit, matching the inclusive convention of <see cref="Overlaps"/>.
    /// </summary>
    /// <param name="other">The candidate overlapping box.</param>
    /// <param name="intersection">The overlap, or <see cref="Empty"/> when the boxes are disjoint.</param>
    /// <returns><see langword="true"/> when the boxes share at least one point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryIntersect(in Aabb3D other, out Aabb3D intersection)
    {
        Vec3 newMin = Vec3.Max(_min, other._min);
        Vec3 newMax = Vec3.Min(_max, other._max);

        if (newMin.X <= newMax.X && newMin.Y <= newMax.Y && newMin.Z <= newMax.Z)
        {
            intersection = new Aabb3D(newMin, newMax);
            return true;
        }

        intersection = Empty;
        return false;
    }

    /// <summary>Returns the box grown by the same amount on every side.</summary>
    /// <param name="margin">The per-side growth, in world units.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Aabb3D Expanded(float margin) => new(Min - new Vec3(margin), Max + new Vec3(margin));

    /// <summary>Returns the box grown by the given amount per axis on both sides.</summary>
    /// <param name="margin">The per-side growth per axis, in world units.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Aabb3D Expanded(Vec3 margin) => new(Min - margin, Max + margin);

    /// <summary>Returns the box shifted by the same offset on both corners.</summary>
    /// <param name="offset">The translation, in world units.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Aabb3D Translated(Vec3 offset) => new(Min + offset, Max + offset);

    /// <summary>
    /// Total surface area, clamped at zero per axis so an inverted or degenerate box reports
    /// zero instead of a negative or nonsensical size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float SurfaceArea()
    {
        Vec3 d = Size;
        float dx = MathF.Max(0.0F, d.X);
        float dy = MathF.Max(0.0F, d.Y);
        float dz = MathF.Max(0.0F, d.Z);
        return 2.0F * ((dx * dy) + (dy * dz) + (dz * dx));
    }

    /// <summary>Enclosed volume, clamped at zero per axis so an inverted or degenerate box reports zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float Volume()
    {
        Vec3 d = Size;
        return MathF.Max(0.0F, d.X) * MathF.Max(0.0F, d.Y) * MathF.Max(0.0F, d.Z);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Aabb3D other) => Min == other.Min && Max == other.Max;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Aabb3D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>Determines whether two boxes span the same corners.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in Aabb3D left, in Aabb3D right) => left.Equals(right);

    /// <summary>Determines whether two boxes span different corners.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in Aabb3D left, in Aabb3D right) => !left.Equals(right);
}
