namespace Axrone.Physics3D;

/// <summary>
/// Support-mapping contract of a 3D collider: the furthest point, centroid and
/// bounds, all reachable without a virtual call so GJK-style traversals close
/// over the shape type.
/// </summary>
/// <typeparam name="TSelf">The shape type itself.</typeparam>
public interface ISupportShape3D<TSelf>
    where TSelf : ISupportShape3D<TSelf>
{
    /// <summary>Returns the point of the shape furthest along <paramref name="direction"/>.</summary>
    static abstract Vec3 Support(in TSelf shape, Vec3 direction);

    /// <summary>Returns the volume centroid of the shape.</summary>
    static abstract Vec3 ComputeCentroid(in TSelf shape);

    /// <summary>Returns the tight bounds of the shape.</summary>
    static abstract Aabb3D ComputeBounds(in TSelf shape);
}

/// <summary>
/// Mass properties of a shape at unit-agnostic density scale: mass, diagonal
/// of the body-frame inertia tensor with its inverse (zero for static or
/// zero-mass bodies so the solver never divides), and the centroid they are
/// measured from. All shapes here are principal-axis aligned, so the tensor
/// stays diagonal.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MassData3D(
    float Mass,
    float InverseMass,
    Vec3 InertiaDiagonal,
    Vec3 InverseInertiaDiagonal,
    Vec3 Centroid)
{
    /// <summary>Massless data; every field zero.</summary>
    public static MassData3D Zero => new(0.0f, 0.0f, Vec3.Zero, Vec3.Zero, Vec3.Zero);
}

/// <summary>Solid ball: a center plus a non-negative radius.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Sphere3 : ISupportShape3D<Sphere3>, IEquatable<Sphere3>
{
    /// <summary>Center of the ball.</summary>
    public readonly Vec3 Center;

    /// <summary>Radius; never negative.</summary>
    public readonly float Radius;

    /// <summary>Creates a ball, rejecting a negative radius.</summary>
    /// <param name="center">The center of the ball.</param>
    /// <param name="radius">The radius; must not be negative.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sphere3(Vec3 center, float radius)
    {
        if (radius < 0.0f || float.IsNaN(radius))
        {
            ThrowHelper.ThrowInvalidOperation("Radius must not be negative.");
        }

        Center = center;
        Radius = radius;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in Sphere3 shape, Vec3 direction)
    {
        if (direction.LengthSquared() < 1e-12f)
        {
            return shape.Center;
        }

        return shape.Center + (Vec3.Normalize(direction) * shape.Radius);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Sphere3 shape) => shape.Center;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Sphere3 shape) =>
        Aabb3D.FromCenterExtents(shape.Center, new Vec3(shape.Radius, shape.Radius, shape.Radius));

    /// <summary>Computes mass properties at <paramref name="density"/>: volume 4/3 pi r cubed, two-fifths-m-r-squared inertia.</summary>
    /// <param name="density">Mass per unit volume.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData3D ComputeMass(float density)
    {
        float mass = (4.0f / 3.0f) * MathF.PI * Radius * Radius * Radius * density;
        if (mass <= 0.0f)
        {
            return MassData3D.Zero;
        }

        float inertia = 0.4f * mass * Radius * Radius;
        var diagonal = new Vec3(inertia, inertia, inertia);
        return new MassData3D(mass, 1.0f / mass, diagonal, new Vec3(1.0f / inertia, 1.0f / inertia, 1.0f / inertia), Center);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Sphere3 other) => Center == other.Center && Radius == other.Radius;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Sphere3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius);

    /// <summary>Determines whether two balls share a center and a radius.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Sphere3 left, Sphere3 right) => left.Equals(right);

    /// <summary>Determines whether two balls differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Sphere3 left, Sphere3 right) => !left.Equals(right);
}

/// <summary>
/// Solid axis-aligned box: a center plus non-negative half extents.
/// Principal-axis aligned by construction, so its inertia tensor is diagonal.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Box3 : ISupportShape3D<Box3>, IEquatable<Box3>
{
    /// <summary>Center of the box.</summary>
    public readonly Vec3 Center;

    /// <summary>Half extents along each axis; never negative.</summary>
    public readonly Vec3 HalfExtents;

    /// <summary>Creates a box, rejecting negative half extents.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Box3(Vec3 center, Vec3 halfExtents)
    {
        if (halfExtents.X < 0.0f || halfExtents.Y < 0.0f || halfExtents.Z < 0.0f ||
            float.IsNaN(halfExtents.X + halfExtents.Y + halfExtents.Z))
        {
            ThrowHelper.ThrowInvalidOperation("Half extents must not be negative.");
        }

        Center = center;
        HalfExtents = halfExtents;
    }

    /// <summary>Creates a box centered at the origin.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Box3 FromHalfExtents(Vec3 halfExtents) => new(Vec3.Zero, halfExtents);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in Box3 shape, Vec3 direction) => new(
        shape.Center.X + (direction.X >= 0.0f ? shape.HalfExtents.X : -shape.HalfExtents.X),
        shape.Center.Y + (direction.Y >= 0.0f ? shape.HalfExtents.Y : -shape.HalfExtents.Y),
        shape.Center.Z + (direction.Z >= 0.0f ? shape.HalfExtents.Z : -shape.HalfExtents.Z));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Box3 shape) => shape.Center;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Box3 shape) => Aabb3D.FromCenterExtents(shape.Center, shape.HalfExtents);

    /// <summary>Computes mass properties at <paramref name="density"/>: volume eight h-x h-y h-z, m-over-three inertia diagonal.</summary>
    /// <param name="density">Mass per unit volume.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData3D ComputeMass(float density)
    {
        float mass = 8.0f * HalfExtents.X * HalfExtents.Y * HalfExtents.Z * density;
        if (mass <= 0.0f)
        {
            return MassData3D.Zero;
        }

        float xx = HalfExtents.X * HalfExtents.X;
        float yy = HalfExtents.Y * HalfExtents.Y;
        float zz = HalfExtents.Z * HalfExtents.Z;
        var diagonal = new Vec3(
            (mass / 3.0f) * (yy + zz),
            (mass / 3.0f) * (xx + zz),
            (mass / 3.0f) * (xx + yy));
        var inverse = new Vec3(
            diagonal.X > 0.0f ? 1.0f / diagonal.X : 0.0f,
            diagonal.Y > 0.0f ? 1.0f / diagonal.Y : 0.0f,
            diagonal.Z > 0.0f ? 1.0f / diagonal.Z : 0.0f);
        return new MassData3D(mass, 1.0f / mass, diagonal, inverse, Center);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Box3 other) => Center == other.Center && HalfExtents == other.HalfExtents;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Box3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, HalfExtents);

    /// <summary>Determines whether two boxes share a center and half extents.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Box3 left, Box3 right) => left.Equals(right);

    /// <summary>Determines whether two boxes differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Box3 left, Box3 right) => !left.Equals(right);
}
