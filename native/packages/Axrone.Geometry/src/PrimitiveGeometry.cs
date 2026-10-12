namespace Axrone.Geometry;

/// <summary>
/// BVH-ready primitive: bounds and centroid for building, an identity for
/// reporting, and a ray test for traversal. The contract is static-abstract so
/// the traversal closes over the primitive type with no virtual dispatch.
/// Pure-math shapes without identities stay on <see cref="Triangle3"/> and
/// <see cref="BoundingSphere3"/>; the types here carry a
/// <see cref="PrimitiveId"/> for the raycaster.
/// </summary>
/// <typeparam name="TSelf">The primitive type itself.</typeparam>
public interface IPrimitiveGeometry<TSelf>
    where TSelf : unmanaged, IPrimitiveGeometry<TSelf>
{
    /// <summary>Computes the tight bounds of a primitive.</summary>
    static abstract Aabb3D ComputeBounds(in TSelf primitive);

    /// <summary>Computes the centroid of a primitive.</summary>
    static abstract Vec3 ComputeCentroid(in TSelf primitive);

    /// <summary>Returns the identity of a primitive.</summary>
    static abstract PrimitiveId GetId(in TSelf primitive);

    /// <summary>Tests a ray against a primitive.</summary>
    static abstract bool TryIntersect(in Ray ray, in TSelf primitive, out PrimitiveHit hit);
}

/// <summary>
/// BVH triangle with an identity: three corners plus which primitive they
/// belong to. The intersection is the two-sided Möller-Trumbore test, so a hit
/// is reported no matter which face points at the ray.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Triangle : IPrimitiveGeometry<Triangle>, IEquatable<Triangle>
{
    /// <summary>First corner.</summary>
    public readonly Vec3 V0;

    /// <summary>Second corner.</summary>
    public readonly Vec3 V1;

    /// <summary>Third corner.</summary>
    public readonly Vec3 V2;

    /// <summary>Identity reported on a hit.</summary>
    public readonly PrimitiveId Id;

    /// <summary>Creates a triangle from its corners and identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Triangle(Vec3 v0, Vec3 v1, Vec3 v2, PrimitiveId id)
    {
        V0 = v0;
        V1 = v1;
        V2 = v2;
        Id = id;
    }

    /// <summary>Creates a triangle from its corners and a raw identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Triangle(Vec3 v0, Vec3 v1, Vec3 v2, int id = 0)
        : this(v0, v1, v2, new PrimitiveId(id))
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Triangle primitive)
    {
        Vec3 min = Vec3.Min(primitive.V0, Vec3.Min(primitive.V1, primitive.V2));
        Vec3 max = Vec3.Max(primitive.V0, Vec3.Max(primitive.V1, primitive.V2));
        return new Aabb3D(min, max);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Triangle primitive) =>
        (primitive.V0 + primitive.V1 + primitive.V2) * (1.0f / 3.0f);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrimitiveId GetId(in Triangle primitive) => primitive.Id;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryIntersect(in Ray ray, in Triangle primitive, out PrimitiveHit hit)
    {
        hit = PrimitiveHit.None;
        Vec3 e1 = primitive.V1 - primitive.V0;
        Vec3 e2 = primitive.V2 - primitive.V0;
        Vec3 pvec = Vec3.Cross(ray.Direction, e2);
        float det = Vec3.Dot(e1, pvec);

        if (MathF.Abs(det) < 1e-8f)
        {
            return false;
        }

        float invDet = 1.0f / det;
        Vec3 tvec = ray.Origin - primitive.V0;
        float u = Vec3.Dot(tvec, pvec) * invDet;
        if (u < 0.0f || u > 1.0f)
        {
            return false;
        }

        Vec3 qvec = Vec3.Cross(tvec, e1);
        float v = Vec3.Dot(ray.Direction, qvec) * invDet;
        if (v < 0.0f || (u + v) > 1.0f)
        {
            return false;
        }

        float t = Vec3.Dot(e2, qvec) * invDet;
        if (t < ray.MinDistance.Value || t > ray.MaxDistance.Value)
        {
            return false;
        }

        Vec3 normal = Vec3.Cross(e1, e2);
        float lenSq = normal.LengthSquared();
        if (lenSq > 1e-12f)
        {
            normal *= 1.0f / MathF.Sqrt(lenSq);
        }

        if (Vec3.Dot(normal, ray.Direction) > 0.0f)
        {
            normal = -normal;
        }

        hit = new PrimitiveHit(ray.PointAt(t), normal, new Distance(t), primitive.Id);
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Triangle other) =>
        V0 == other.V0 &&
        V1 == other.V1 &&
        V2 == other.V2 &&
        Id == other.Id;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Triangle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(V0, V1, V2, Id);

    /// <summary>Determines whether two triangles share corners and identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in Triangle left, in Triangle right) => left.Equals(right);

    /// <summary>Determines whether two triangles differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in Triangle left, in Triangle right) => !left.Equals(right);
}

/// <summary>
/// BVH sphere with an identity: a center, a radius and which primitive they
/// belong to. The analytic test assumes a unit direction; cast a normalized
/// ray, otherwise the discriminant mixes units.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Sphere : IPrimitiveGeometry<Sphere>, IEquatable<Sphere>
{
    /// <summary>Center of the sphere.</summary>
    public readonly Vec3 Center;

    /// <summary>Radius of the sphere, never negative.</summary>
    public readonly float Radius;

    /// <summary>Identity reported on a hit.</summary>
    public readonly PrimitiveId Id;

    /// <summary>Creates a sphere; a negative radius is rejected.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sphere(Vec3 center, float radius, PrimitiveId id)
    {
        if (radius < 0.0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(radius));
        }

        Center = center;
        Radius = radius;
        Id = id;
    }

    /// <summary>Creates a sphere with a raw identity; a negative radius is rejected.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sphere(Vec3 center, float radius, int id = 0)
        : this(center, radius, new PrimitiveId(id))
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Sphere primitive)
    {
        Vec3 radVec = new(primitive.Radius);
        return new Aabb3D(primitive.Center - radVec, primitive.Center + radVec);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Sphere primitive) => primitive.Center;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrimitiveId GetId(in Sphere primitive) => primitive.Id;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryIntersect(in Ray ray, in Sphere primitive, out PrimitiveHit hit)
    {
        hit = PrimitiveHit.None;
        Vec3 oc = ray.Origin - primitive.Center;
        float b = Vec3.Dot(oc, ray.Direction);
        float c = Vec3.Dot(oc, oc) - (primitive.Radius * primitive.Radius);

        float discriminant = (b * b) - c;
        if (discriminant < 0.0f)
        {
            return false;
        }

        float sqrtD = MathF.Sqrt(discriminant);
        float t = -b - sqrtD;

        if (t < ray.MinDistance.Value || t > ray.MaxDistance.Value)
        {
            t = -b + sqrtD;
            if (t < ray.MinDistance.Value || t > ray.MaxDistance.Value)
            {
                return false;
            }
        }

        Vec3 point = ray.PointAt(t);
        Vec3 normal = (point - primitive.Center) * (primitive.Radius > 1e-9f ? 1.0f / primitive.Radius : 0.0f);
        hit = new PrimitiveHit(point, normal, new Distance(t), primitive.Id);
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Sphere other) =>
        Center == other.Center &&
        Radius == other.Radius &&
        Id == other.Id;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Sphere other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius, Id);

    /// <summary>Determines whether two spheres share a center, a radius and an identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in Sphere left, in Sphere right) => left.Equals(right);

    /// <summary>Determines whether two spheres differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in Sphere left, in Sphere right) => !left.Equals(right);
}
