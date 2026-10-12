namespace Axrone.Geometry;

using System.Runtime.Intrinsics;

/// <summary>
/// Culler-friendly bounding volume: a kind tag plus classification against a
/// <see cref="CameraFrustum"/>. Implemented by the professional volumes
/// (<see cref="Aabb3D"/>, <see cref="BoundingSphere3"/>) through partials, so
/// one generic culler walks either shape with no virtual dispatch.
/// </summary>
/// <typeparam name="TSelf">The volume type itself.</typeparam>
public interface IBoundingVolume<TSelf>
    where TSelf : struct, IBoundingVolume<TSelf>
{
    /// <summary>Short tag naming the shape ("aabb", "sphere", ...).</summary>
    static abstract string KindIdentifier { get; }

    /// <summary>Classifies this volume against <paramref name="frustum"/>.</summary>
    ContainmentType Classify(in CameraFrustum frustum);

    /// <summary>Whether this volume reaches <paramref name="frustum"/> at all.</summary>
    bool Intersects(in CameraFrustum frustum);
}

/// <summary>
/// Decides how one bound shape is classified. Static-abstract so the culler
/// closes over the policy type with no virtual dispatch.
/// </summary>
/// <typeparam name="TBounds">The bound shape.</typeparam>
public interface IClassificationPolicy<TBounds>
    where TBounds : struct
{
    /// <summary>Classifies <paramref name="bounds"/> against <paramref name="frustum"/>.</summary>
    static abstract ContainmentType Classify(in CameraFrustum frustum, in TBounds bounds);

    /// <summary>Whether <paramref name="bounds"/> reaches <paramref name="frustum"/> at all.</summary>
    static abstract bool Intersects(in CameraFrustum frustum, in TBounds bounds);
}

/// <summary>Classifies through the volume's own contract.</summary>
/// <typeparam name="TVolume">The volume type.</typeparam>
public readonly struct DirectVolumePolicy<TVolume> : IClassificationPolicy<TVolume>
    where TVolume : struct, IBoundingVolume<TVolume>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ContainmentType Classify(in CameraFrustum frustum, in TVolume bounds) => bounds.Classify(in frustum);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Intersects(in CameraFrustum frustum, in TVolume bounds) => bounds.Intersects(in frustum);
}

/// <summary>Classifies spheres without routing through the volume contract.</summary>
public readonly struct SphereVolumePolicy : IClassificationPolicy<BoundingSphere3>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ContainmentType Classify(in CameraFrustum frustum, in BoundingSphere3 bounds) => frustum.ClassifySphere(in bounds);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Intersects(in CameraFrustum frustum, in BoundingSphere3 bounds) => frustum.IntersectsSphere(in bounds);
}

/// <summary>Classifies boxes without routing through the volume contract.</summary>
public readonly struct AabbVolumePolicy : IClassificationPolicy<Aabb3D>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ContainmentType Classify(in CameraFrustum frustum, in Aabb3D bounds) => frustum.ClassifyAabb(in bounds);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Intersects(in CameraFrustum frustum, in Aabb3D bounds) => frustum.IntersectsAabb(in bounds);
}

/// <summary>
/// Six-plane view volume with a Structure-of-Arrays SIMD fast path: extraction
/// packs the planes into 8-wide lanes (two neutral sentinels pad the six
/// planes), so point/sphere/box tests evaluate all planes in a handful of
/// vector ops and fall back to the scalar two-vertex walk where 256-bit
/// vectors are unavailable. Planes are the shared numeric <see cref="Plane"/>
/// rather than a local copy. Call <see cref="SetFromMatrix"/> before querying;
/// a default frustum classifies everything as inside.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CameraFrustum : IEquatable<CameraFrustum>
{
    /// <summary>Left clipping plane; inward normal.</summary>
    public Plane Left;

    /// <summary>Right clipping plane; inward normal.</summary>
    public Plane Right;

    /// <summary>Bottom clipping plane; inward normal.</summary>
    public Plane Bottom;

    /// <summary>Top clipping plane; inward normal.</summary>
    public Plane Top;

    /// <summary>Near clipping plane; inward normal.</summary>
    public Plane Near;

    /// <summary>Far clipping plane; inward normal.</summary>
    public Plane Far;

    private Vector256<float> _simdPlanesX;
    private Vector256<float> _simdPlanesY;
    private Vector256<float> _simdPlanesZ;
    private Vector256<float> _simdPlanesD;
    private Vector256<float> _simdAbsPlanesX;
    private Vector256<float> _simdAbsPlanesY;
    private Vector256<float> _simdAbsPlanesZ;

    /// <summary>Indexes the six planes in Left/Right/Bottom/Top/Near/Far order.</summary>
    public ref readonly Plane this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= 6u)
            {
                ThrowHelper.ThrowIndexOutOfRange();
            }

            return ref Unsafe.Add(ref Unsafe.AsRef(in Left), index);
        }
    }

    /// <summary>Read-only view over the six planes.</summary>
    public readonly ReadOnlySpan<Plane> Planes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(in Left, 6);
    }

    /// <summary>
    /// Extracts the six inward-facing planes of a view-projection matrix and
    /// packs the SIMD lanes. Follows the same row-vector extraction as
    /// <see cref="Frustum3"/> so both volumes agree on one matrix.
    /// </summary>
    /// <param name="m">The combined view-projection matrix.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SetFromMatrix(in Mat4 m)
    {
        Left = NormalizePlane(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41);
        Right = NormalizePlane(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41);
        Bottom = NormalizePlane(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42);
        Top = NormalizePlane(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42);
        Near = NormalizePlane(m.M13, m.M23, m.M33, m.M43);
        Far = NormalizePlane(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43);

        if (Vector256.IsHardwareAccelerated)
        {
            Span<float> xs = stackalloc float[8];
            Span<float> ys = stackalloc float[8];
            Span<float> zs = stackalloc float[8];
            Span<float> ds = stackalloc float[8];
            Span<float> axs = stackalloc float[8];
            Span<float> ays = stackalloc float[8];
            Span<float> azs = stackalloc float[8];

            for (int i = 0; i < 6; i++)
            {
                Plane plane = Unsafe.Add(ref Unsafe.AsRef(in Left), i);
                Vec3 normal = plane.Normal;
                xs[i] = normal.X;
                ys[i] = normal.Y;
                zs[i] = normal.Z;
                ds[i] = plane.D;
                axs[i] = MathF.Abs(normal.X);
                ays[i] = MathF.Abs(normal.Y);
                azs[i] = MathF.Abs(normal.Z);
            }

            const float SafeDistance = 1e30f;
            xs[6] = 0.0f; ys[6] = 0.0f; zs[6] = 0.0f; ds[6] = SafeDistance;
            xs[7] = 0.0f; ys[7] = 0.0f; zs[7] = 0.0f; ds[7] = SafeDistance;
            axs[6] = 0.0f; ays[6] = 0.0f; azs[6] = 0.0f;
            axs[7] = 0.0f; ays[7] = 0.0f; azs[7] = 0.0f;

            _simdPlanesX = Vector256.Create(xs);
            _simdPlanesY = Vector256.Create(ys);
            _simdPlanesZ = Vector256.Create(zs);
            _simdPlanesD = Vector256.Create(ds);
            _simdAbsPlanesX = Vector256.Create(axs);
            _simdAbsPlanesY = Vector256.Create(ays);
            _simdAbsPlanesZ = Vector256.Create(azs);
        }
    }

    /// <summary>Builds a frustum from a view-projection matrix in one call.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static CameraFrustum CreateFromMatrix(in Mat4 m)
    {
        CameraFrustum frustum = default;
        frustum.SetFromMatrix(in m);
        return frustum;
    }

    /// <summary>Whether the point satisfies all six half-space tests.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool ContainsPoint(Vec3 point)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> d = (Vector256.Create(point.X) * _simdPlanesX) +
                                 (Vector256.Create(point.Y) * _simdPlanesY) +
                                 (Vector256.Create(point.Z) * _simdPlanesZ) + _simdPlanesD;

            return Vector256.LessThan(d, Vector256<float>.Zero).ExtractMostSignificantBits() == 0u;
        }

        for (int i = 0; i < 6; i++)
        {
            if (Plane.DotCoordinate(Unsafe.Add(ref Unsafe.AsRef(in Left), i), point) < 0.0f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Classifies a sphere: outside when any plane clears the radius, inside when none is touched.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly ContainmentType ClassifySphere(in BoundingSphere3 sphere)
    {
        Vec3 center = sphere.Center;
        float radius = sphere.Radius;

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> d = (Vector256.Create(center.X) * _simdPlanesX) +
                                 (Vector256.Create(center.Y) * _simdPlanesY) +
                                 (Vector256.Create(center.Z) * _simdPlanesZ) + _simdPlanesD;

            if (Vector256.LessThan(d, Vector256.Create(-radius)).ExtractMostSignificantBits() != 0u)
            {
                return ContainmentType.Disjoint;
            }

            if (Vector256.LessThan(d, Vector256.Create(radius)).ExtractMostSignificantBits() != 0u)
            {
                return ContainmentType.Intersects;
            }

            return ContainmentType.Contains;
        }

        bool intersects = false;
        for (int i = 0; i < 6; i++)
        {
            float distance = Plane.DotCoordinate(Unsafe.Add(ref Unsafe.AsRef(in Left), i), center);
            if (distance < -radius)
            {
                return ContainmentType.Disjoint;
            }

            if (distance < radius)
            {
                intersects = true;
            }
        }

        return intersects ? ContainmentType.Intersects : ContainmentType.Contains;
    }

    /// <summary>Whether a sphere reaches the volume at all.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool IntersectsSphere(in BoundingSphere3 sphere)
    {
        Vec3 center = sphere.Center;
        float radius = sphere.Radius;

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> d = (Vector256.Create(center.X) * _simdPlanesX) +
                                 (Vector256.Create(center.Y) * _simdPlanesY) +
                                 (Vector256.Create(center.Z) * _simdPlanesZ) + _simdPlanesD;

            return Vector256.LessThan(d, Vector256.Create(-radius)).ExtractMostSignificantBits() == 0u;
        }

        for (int i = 0; i < 6; i++)
        {
            if (Plane.DotCoordinate(Unsafe.Add(ref Unsafe.AsRef(in Left), i), center) < -radius)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Classifies a box with the center/extents walk: the projected radius
    /// decides per plane, so one pass yields the trichotomy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly ContainmentType ClassifyAabb(in Aabb3D box)
    {
        Vec3 center = box.Center;
        Vec3 extents = box.Extents;

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> r = (Vector256.Create(extents.X) * _simdAbsPlanesX) +
                                 (Vector256.Create(extents.Y) * _simdAbsPlanesY) +
                                 (Vector256.Create(extents.Z) * _simdAbsPlanesZ);

            Vector256<float> d = (Vector256.Create(center.X) * _simdPlanesX) +
                                 (Vector256.Create(center.Y) * _simdPlanesY) +
                                 (Vector256.Create(center.Z) * _simdPlanesZ) + _simdPlanesD;

            if (Vector256.LessThan(d, -r).ExtractMostSignificantBits() != 0u)
            {
                return ContainmentType.Disjoint;
            }

            if (Vector256.LessThan(d, r).ExtractMostSignificantBits() != 0u)
            {
                return ContainmentType.Intersects;
            }

            return ContainmentType.Contains;
        }

        bool intersects = false;
        for (int i = 0; i < 6; i++)
        {
            Plane plane = Unsafe.Add(ref Unsafe.AsRef(in Left), i);
            Vec3 absNormal = Vec3.Abs(plane.Normal);
            float r = Vec3.Dot(absNormal, extents);
            float d = Vec3.Dot(plane.Normal, center) + plane.D;

            if (d < -r)
            {
                return ContainmentType.Disjoint;
            }

            if (d < r)
            {
                intersects = true;
            }
        }

        return intersects ? ContainmentType.Intersects : ContainmentType.Contains;
    }

    /// <summary>Whether a box reaches the volume at all.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool IntersectsAabb(in Aabb3D box)
    {
        Vec3 center = box.Center;
        Vec3 extents = box.Extents;

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> r = (Vector256.Create(extents.X) * _simdAbsPlanesX) +
                                 (Vector256.Create(extents.Y) * _simdAbsPlanesY) +
                                 (Vector256.Create(extents.Z) * _simdAbsPlanesZ);

            Vector256<float> d = (Vector256.Create(center.X) * _simdPlanesX) +
                                 (Vector256.Create(center.Y) * _simdPlanesY) +
                                 (Vector256.Create(center.Z) * _simdPlanesZ) + _simdPlanesD;

            return Vector256.LessThan(d, -r).ExtractMostSignificantBits() == 0u;
        }

        for (int i = 0; i < 6; i++)
        {
            Plane plane = Unsafe.Add(ref Unsafe.AsRef(in Left), i);
            Vec3 absNormal = Vec3.Abs(plane.Normal);
            float r = Vec3.Dot(absNormal, extents);
            float d = Vec3.Dot(plane.Normal, center) + plane.D;

            if (d < -r)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Classifies any bound volume through its own contract.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly ContainmentType Classify<TBounds>(in TBounds bounds)
        where TBounds : struct, IBoundingVolume<TBounds> =>
        bounds.Classify(in this);

    /// <summary>Tests any bound volume through its own contract.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Intersects<TBounds>(in TBounds bounds)
        where TBounds : struct, IBoundingVolume<TBounds> =>
        bounds.Intersects(in this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(CameraFrustum other) =>
        Left.Equals(other.Left) && Right.Equals(other.Right) &&
        Bottom.Equals(other.Bottom) && Top.Equals(other.Top) &&
        Near.Equals(other.Near) && Far.Equals(other.Far);

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is CameraFrustum other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode() =>
        HashCode.Combine(Left, Right, Bottom, Top, Near, Far);

    /// <summary>Determines whether two frustums share all six planes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(CameraFrustum left, CameraFrustum right) => left.Equals(right);

    /// <summary>Determines whether two frustums differ in any plane.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(CameraFrustum left, CameraFrustum right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Plane NormalizePlane(float a, float b, float c, float d)
    {
        float lengthSq = (a * a) + (b * b) + (c * c);
        if (lengthSq <= 1e-15f)
        {
            return new Plane(Vec3.Zero, 0.0f);
        }

        float invLength = 1.0f / MathF.Sqrt(lengthSq);
        return new Plane(new Vec3(a * invLength, b * invLength, c * invLength), d * invLength);
    }
}
