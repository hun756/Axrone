namespace Axrone.Physics3D;

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
    public static Vec3 Support(in Sphere3 shape, in Vec3 direction)
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
    public static Vec3 Support(in Box3 shape, in Vec3 direction) => new(
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

/// <summary>Principal axis selector for axis-aligned shapes; mirrors the web shape descriptors.</summary>
public enum ShapeAxis3D : byte
{
    /// <summary>Local X axis.</summary>
    X = 0,

    /// <summary>Local Y axis.</summary>
    Y = 1,

    /// <summary>Local Z axis.</summary>
    Z = 2,
}

/// <summary>Axis helpers shared by cylinder and cone shapes.</summary>
internal static class ShapeAxis
{
    /// <summary>Unit vector of a principal axis.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Vector(ShapeAxis3D axis) => axis switch
    {
        ShapeAxis3D.X => new Vec3(1.0f, 0.0f, 0.0f),
        ShapeAxis3D.Z => new Vec3(0.0f, 0.0f, 1.0f),
        _ => new Vec3(0.0f, 1.0f, 0.0f),
    };

    /// <summary>Half extents of a radius/height solid around an axis.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Extents(float radius, float halfHeight, ShapeAxis3D axis) => axis switch
    {
        ShapeAxis3D.X => new Vec3(halfHeight, radius, radius),
        ShapeAxis3D.Z => new Vec3(radius, radius, halfHeight),
        _ => new Vec3(radius, halfHeight, radius),
    };
}

/// <summary>
/// Stadium solid: a segment swept by a ball. A zero-length segment degrades
/// gracefully to a ball. Mass follows the web descriptors: cylinder plus
/// caps with isotropic inertia.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Capsule3 : ISupportShape3D<Capsule3>, IEquatable<Capsule3>
{
    /// <summary>First cap center.</summary>
    public readonly Vec3 P0;

    /// <summary>Second cap center.</summary>
    public readonly Vec3 P1;

    /// <summary>Cap radius; never negative.</summary>
    public readonly float Radius;

    /// <summary>Creates a capsule, rejecting a negative radius.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Capsule3(Vec3 p0, Vec3 p1, float radius)
    {
        if (radius < 0.0f || float.IsNaN(radius))
        {
            ThrowHelper.ThrowInvalidOperation("Radius must not be negative.");
        }

        P0 = p0;
        P1 = p1;
        Radius = radius;
    }

    /// <summary>Segment length between the cap centers.</summary>
    public float Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (P1 - P0).Length();
    }

    /// <summary>Midpoint of the cap segment.</summary>
    public Vec3 Midpoint
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (P0 + P1) * 0.5f;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in Capsule3 shape, in Vec3 direction)
    {
        float d0 = Vec3.Dot(shape.P0, direction);
        Vec3 base_ = d0 >= Vec3.Dot(shape.P1, direction) ? shape.P0 : shape.P1;
        if (direction.LengthSquared() < 1e-12f)
        {
            return base_;
        }

        return base_ + (Vec3.Normalize(direction) * shape.Radius);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Capsule3 shape) => shape.Midpoint;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Capsule3 shape)
    {
        Vec3 min = Vec3.Min(shape.P0, shape.P1);
        Vec3 max = Vec3.Max(shape.P0, shape.P1);
        var r = new Vec3(shape.Radius, shape.Radius, shape.Radius);
        return new Aabb3D(min - r, max + r);
    }

    /// <summary>Computes mass properties at <paramref name="density"/> per the web descriptors.</summary>
    /// <param name="density">Mass per unit volume.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData3D ComputeMass(float density)
    {
        float segment = (P1 - P0).Length();
        float cylinderMass = MathF.PI * Radius * Radius * segment * density;
        float sphereMass = (4.0f / 3.0f) * MathF.PI * Radius * Radius * Radius * density;
        float mass = cylinderMass + sphereMass;
        if (mass <= 0.0f)
        {
            return MassData3D.Zero;
        }

        float inertia = mass * Radius * Radius;
        var diagonal = new Vec3(inertia, inertia, inertia);
        return new MassData3D(mass, 1.0f / mass, diagonal, new Vec3(1.0f / inertia, 1.0f / inertia, 1.0f / inertia), Midpoint);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Capsule3 other) => P0 == other.P0 && P1 == other.P1 && Radius == other.Radius;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Capsule3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(P0, P1, Radius);

    /// <summary>Determines whether two capsules match.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Capsule3 left, Capsule3 right) => left.Equals(right);

    /// <summary>Determines whether two capsules differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Capsule3 left, Capsule3 right) => !left.Equals(right);
}

/// <summary>
/// Solid cylinder around a principal axis: a center, a radius and a full
/// height. Mass follows the web descriptors with the radial/axial split
/// mapped onto the selected axis.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Cylinder3 : ISupportShape3D<Cylinder3>, IEquatable<Cylinder3>
{
    /// <summary>Center of the cylinder.</summary>
    public readonly Vec3 Center;

    /// <summary>Radius; never negative.</summary>
    public readonly float Radius;

    /// <summary>Full height along <see cref="Axis"/>; never negative.</summary>
    public readonly float Height;

    /// <summary>Principal axis of the cylinder.</summary>
    public readonly ShapeAxis3D Axis;

    /// <summary>Creates a cylinder, rejecting negative dimensions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cylinder3(Vec3 center, float radius, float height, ShapeAxis3D axis = ShapeAxis3D.Y)
    {
        if (radius < 0.0f || float.IsNaN(radius) || height < 0.0f || float.IsNaN(height))
        {
            ThrowHelper.ThrowInvalidOperation("Cylinder dimensions must not be negative.");
        }

        Center = center;
        Radius = radius;
        Height = height;
        Axis = axis;
    }

    /// <summary>Unit axis vector.</summary>
    public Vec3 AxisVector
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ShapeAxis.Vector(Axis);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in Cylinder3 shape, in Vec3 direction)
    {
        Vec3 axis = shape.AxisVector;
        float axial = Vec3.Dot(direction, axis);
        Vec3 radial = direction - (axis * axial);
        Vec3 rim = axial >= 0.0f
            ? shape.Center + (axis * (shape.Height * 0.5f))
            : shape.Center - (axis * (shape.Height * 0.5f));
        if (radial.LengthSquared() < 1e-12f)
        {
            return rim;
        }

        return rim + (Vec3.Normalize(radial) * shape.Radius);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Cylinder3 shape) => shape.Center;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Cylinder3 shape) =>
        Aabb3D.FromCenterExtents(shape.Center, ShapeAxis.Extents(shape.Radius, shape.Height * 0.5f, shape.Axis));

    /// <summary>Computes mass properties at <paramref name="density"/> per the web descriptors.</summary>
    /// <param name="density">Mass per unit volume.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData3D ComputeMass(float density)
    {
        float mass = MathF.PI * Radius * Radius * Height * density;
        if (mass <= 0.0f)
        {
            return MassData3D.Zero;
        }

        float radial = (mass * ((3.0f * Radius * Radius) + (Height * Height))) / 12.0f;
        float axial = 0.5f * mass * Radius * Radius;
        Vec3 diagonal = Axis switch
        {
            ShapeAxis3D.X => new Vec3(axial, radial, radial),
            ShapeAxis3D.Z => new Vec3(radial, radial, axial),
            _ => new Vec3(radial, axial, radial),
        };
        Vec3 inverse = new(
            diagonal.X > 0.0f ? 1.0f / diagonal.X : 0.0f,
            diagonal.Y > 0.0f ? 1.0f / diagonal.Y : 0.0f,
            diagonal.Z > 0.0f ? 1.0f / diagonal.Z : 0.0f);
        return new MassData3D(mass, 1.0f / mass, diagonal, inverse, Center);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Cylinder3 other) => Center == other.Center && Radius == other.Radius && Height == other.Height && Axis == other.Axis;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Cylinder3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius, Height, Axis);

    /// <summary>Determines whether two cylinders match.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Cylinder3 left, Cylinder3 right) => left.Equals(right);

    /// <summary>Determines whether two cylinders differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Cylinder3 left, Cylinder3 right) => !left.Equals(right);
}

/// <summary>
/// Solid cone around a principal axis: a mass center, a base radius and a
/// full height, the apex sitting three quarters of the height above the
/// center along the axis. Mass follows the web descriptors.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Cone3 : ISupportShape3D<Cone3>, IEquatable<Cone3>
{
    /// <summary>Mass center of the cone.</summary>
    public readonly Vec3 Center;

    /// <summary>Base radius; never negative.</summary>
    public readonly float Radius;

    /// <summary>Full height along <see cref="Axis"/>; never negative.</summary>
    public readonly float Height;

    /// <summary>Principal axis from base to apex.</summary>
    public readonly ShapeAxis3D Axis;

    /// <summary>Creates a cone, rejecting negative dimensions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cone3(Vec3 center, float radius, float height, ShapeAxis3D axis = ShapeAxis3D.Y)
    {
        if (radius < 0.0f || float.IsNaN(radius) || height < 0.0f || float.IsNaN(height))
        {
            ThrowHelper.ThrowInvalidOperation("Cone dimensions must not be negative.");
        }

        Center = center;
        Radius = radius;
        Height = height;
        Axis = axis;
    }

    /// <summary>Unit axis vector from base to apex.</summary>
    public Vec3 AxisVector
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ShapeAxis.Vector(Axis);
    }

    /// <summary>Apex position.</summary>
    public Vec3 Apex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Center + (AxisVector * (Height * 0.75f));
    }

    /// <summary>Base disk center.</summary>
    public Vec3 BaseCenter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Center - (AxisVector * (Height * 0.25f));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in Cone3 shape, in Vec3 direction)
    {
        Vec3 axis = shape.AxisVector;
        float axial = Vec3.Dot(direction, axis);
        Vec3 radial = direction - (axis * axial);
        Vec3 apex = shape.Apex;
        float apexScore = Vec3.Dot(apex, direction);
        Vec3 rim = shape.BaseCenter;
        if (radial.LengthSquared() >= 1e-12f)
        {
            rim += Vec3.Normalize(radial) * shape.Radius;
        }

        return Vec3.Dot(rim, direction) >= apexScore ? rim : apex;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Cone3 shape) => shape.Center;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Cone3 shape)
    {
        Vec3 axis = shape.AxisVector;
        Vec3 baseCenter = shape.BaseCenter;
        Vec3 apex = shape.Apex;
        Vec3 radial = new Vec3(shape.Radius, shape.Radius, shape.Radius) - (axis * shape.Radius);
        Vec3 min = Vec3.Min(baseCenter - radial, apex);
        Vec3 max = Vec3.Max(baseCenter + radial, apex);
        return new Aabb3D(min, max);
    }

    /// <summary>Computes mass properties at <paramref name="density"/> per the web descriptors.</summary>
    /// <param name="density">Mass per unit volume.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData3D ComputeMass(float density)
    {
        float mass = ((MathF.PI * Radius * Radius * Height) / 3.0f) * density;
        if (mass <= 0.0f)
        {
            return MassData3D.Zero;
        }

        float transverse = ((3.0f / 20.0f) * mass * Radius * Radius) + ((3.0f / 5.0f) * mass * Height * Height);
        float axial = (3.0f / 10.0f) * mass * Radius * Radius;
        Vec3 diagonal = Axis switch
        {
            ShapeAxis3D.X => new Vec3(axial, transverse, transverse),
            ShapeAxis3D.Z => new Vec3(transverse, transverse, axial),
            _ => new Vec3(transverse, axial, transverse),
        };
        Vec3 inverse = new(
            diagonal.X > 0.0f ? 1.0f / diagonal.X : 0.0f,
            diagonal.Y > 0.0f ? 1.0f / diagonal.Y : 0.0f,
            diagonal.Z > 0.0f ? 1.0f / diagonal.Z : 0.0f);
        return new MassData3D(mass, 1.0f / mass, diagonal, inverse, Center);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Cone3 other) => Center == other.Center && Radius == other.Radius && Height == other.Height && Axis == other.Axis;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Cone3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius, Height, Axis);

    /// <summary>Determines whether two cones match.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Cone3 left, Cone3 right) => left.Equals(right);

    /// <summary>Determines whether two cones differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Cone3 left, Cone3 right) => !left.Equals(right);
}

/// <summary>
/// Convex point cloud: collision resolves over the convex hull of the
/// vertices through the support map (vertex argmax), mirroring the web
/// descriptors. Mass follows the bounding box, like the web.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Hull3D : ISupportShape3D<Hull3D>, IEquatable<Hull3D>
{
    /// <summary>Hull vertices; never empty.</summary>
    public readonly Vec3[] Vertices;

    /// <summary>Creates a hull, rejecting empty or degenerate vertex sets.</summary>
    public Hull3D(Vec3[] vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        if (vertices.Length == 0)
        {
            ThrowHelper.ThrowInvalidOperation("Hull requires at least one vertex.");
        }

        Vertices = vertices;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in Hull3D shape, in Vec3 direction) => Hull3DHelpers.Support(shape.Vertices, direction);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Hull3D shape) => Hull3DHelpers.Centroid(shape.Vertices);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Hull3D shape) => Hull3DHelpers.Bounds(shape.Vertices);

    /// <summary>Computes bounding-box mass at <paramref name="density"/> per the web descriptors.</summary>
    /// <param name="density">Mass per unit volume.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData3D ComputeMass(float density) => Hull3DHelpers.Mass(Vertices, density);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Hull3D other) => ReferenceEquals(Vertices, other.Vertices);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Hull3D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Vertices != null ? Vertices.GetHashCode() : 0;

    /// <summary>Determines whether two hulls share their vertex buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Hull3D left, Hull3D right) => left.Equals(right);

    /// <summary>Determines whether two hulls differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Hull3D left, Hull3D right) => !left.Equals(right);
}

/// <summary>
/// Triangle soup: narrowphase resolves over the convex vertex set like the
/// web descriptors, while raycasts strike exact triangles. Best suited to
/// static scenery; dynamic meshes carry bounding-box mass.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Mesh3D : ISupportShape3D<Mesh3D>, IEquatable<Mesh3D>
{
    /// <summary>Mesh vertices.</summary>
    public readonly Vec3[] Vertices;

    /// <summary>Triangle indices into <see cref="Vertices"/>; a multiple of three.</summary>
    public readonly int[] Indices;

    /// <summary>Creates a mesh, rejecting empty or malformed topology.</summary>
    public Mesh3D(Vec3[] vertices, int[] indices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(indices);
        if (vertices.Length == 0 || indices.Length == 0 || indices.Length % 3 != 0)
        {
            ThrowHelper.ThrowInvalidOperation("Mesh requires vertices and a multiple-of-three index list.");
        }

        Vertices = vertices;
        Indices = indices;
    }

    /// <summary>Triangle count.</summary>
    public int TriangleCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Indices.Length / 3;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in Mesh3D shape, in Vec3 direction) =>
        Hull3DHelpers.Support(shape.Vertices, direction);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Mesh3D shape) => Hull3DHelpers.Centroid(shape.Vertices);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Mesh3D shape) => Hull3DHelpers.Bounds(shape.Vertices);

    /// <summary>Computes bounding-box mass at <paramref name="density"/> per the web descriptors.</summary>
    /// <param name="density">Mass per unit volume.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData3D ComputeMass(float density) => Hull3DHelpers.Mass(Vertices, density);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Mesh3D other) => ReferenceEquals(Vertices, other.Vertices) && ReferenceEquals(Indices, other.Indices);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Mesh3D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Vertices != null ? Vertices.GetHashCode() : 0;

    /// <summary>Determines whether two meshes share their buffers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Mesh3D left, Mesh3D right) => left.Equals(right);

    /// <summary>Determines whether two meshes differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Mesh3D left, Mesh3D right) => !left.Equals(right);
}

/// <summary>
/// Heightfield terrain: a grid of heights over the XZ plane. Narrowphase
/// resolves over the grid vertices like the web descriptors; best suited to
/// static scenery. Mass follows the bounding box, like the web.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Heightfield3D : ISupportShape3D<Heightfield3D>, IEquatable<Heightfield3D>
{
    /// <summary>Row-major heights; Width by Depth entries.</summary>
    public readonly float[] Heights;

    /// <summary>Grid columns.</summary>
    public readonly int Width;

    /// <summary>Grid rows.</summary>
    public readonly int Depth;

    /// <summary>World units per column.</summary>
    public readonly float ScaleX;

    /// <summary>World units per height unit.</summary>
    public readonly float ScaleY;

    /// <summary>World units per row.</summary>
    public readonly float ScaleZ;

    /// <summary>Creates a heightfield, rejecting malformed grids.</summary>
    public Heightfield3D(float[] heights, int width, int depth, float scaleX = 1.0f, float scaleY = 1.0f, float scaleZ = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(heights);
        if (width < 2 || depth < 2 || heights.Length != width * depth)
        {
            ThrowHelper.ThrowInvalidOperation("Heightfield requires a Width-by-Depth height grid at least two by two.");
        }

        if (!(scaleX > 0.0f) || !(scaleY > 0.0f) || !(scaleZ > 0.0f))
        {
            ThrowHelper.ThrowInvalidOperation("Heightfield scales must be positive.");
        }

        Heights = heights;
        Width = width;
        Depth = depth;
        ScaleX = scaleX;
        ScaleY = scaleY;
        ScaleZ = scaleZ;
    }

    /// <summary>Grid vertex in local space.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 Vertex(int x, int z) => new(
        (x - ((Width - 1) * 0.5f)) * ScaleX,
        Heights[(z * Width) + x] * ScaleY,
        (z - ((Depth - 1) * 0.5f)) * ScaleZ);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in Heightfield3D shape, in Vec3 direction)
    {
        Vec3 best = shape.Vertex(0, 0);
        float bestDot = Vec3.Dot(best, direction);
        for (int z = 0; z < shape.Depth; z++)
        {
            for (int x = 0; x < shape.Width; x++)
            {
                Vec3 candidate = shape.Vertex(x, z);
                float dot = Vec3.Dot(candidate, direction);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = candidate;
                }
            }
        }

        return best;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in Heightfield3D shape)
    {
        Aabb3D bounds = ComputeBounds(shape);
        return (bounds.Min + bounds.Max) * 0.5f;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in Heightfield3D shape)
    {
        Vec3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vec3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int z = 0; z < shape.Depth; z++)
        {
            for (int x = 0; x < shape.Width; x++)
            {
                Vec3 vertex = shape.Vertex(x, z);
                min = Vec3.Min(min, vertex);
                max = Vec3.Max(max, vertex);
            }
        }

        return new Aabb3D(min, max);
    }

    /// <summary>Computes bounding-box mass at <paramref name="density"/> per the web descriptors.</summary>
    /// <param name="density">Mass per unit volume.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MassData3D ComputeMass(float density)
    {
        Aabb3D bounds = ComputeBounds(this);
        Vec3 full = bounds.Max - bounds.Min;
        float mass = full.X * full.Y * full.Z * density;
        if (mass <= 0.0f)
        {
            return MassData3D.Zero;
        }

        var diagonal = new Vec3(
            (mass * ((full.Y * full.Y) + (full.Z * full.Z))) / 12.0f,
            (mass * ((full.X * full.X) + (full.Z * full.Z))) / 12.0f,
            (mass * ((full.X * full.X) + (full.Y * full.Y))) / 12.0f);
        Vec3 inverse = new(
            diagonal.X > 0.0f ? 1.0f / diagonal.X : 0.0f,
            diagonal.Y > 0.0f ? 1.0f / diagonal.Y : 0.0f,
            diagonal.Z > 0.0f ? 1.0f / diagonal.Z : 0.0f);
        return new MassData3D(mass, 1.0f / mass, diagonal, inverse, (bounds.Min + bounds.Max) * 0.5f);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Heightfield3D other) =>
        ReferenceEquals(Heights, other.Heights) && Width == other.Width && Depth == other.Depth;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Heightfield3D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Heights != null ? Heights.GetHashCode() : 0;

    /// <summary>Determines whether two heightfields share their grid.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Heightfield3D left, Heightfield3D right) => left.Equals(right);

    /// <summary>Determines whether two heightfields differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Heightfield3D left, Heightfield3D right) => !left.Equals(right);
}

/// <summary>Shared vertex-cloud routines behind hull and mesh shapes.</summary>
internal static class Hull3DHelpers
{
    /// <summary>Vertex argmax along a direction.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(Vec3[] vertices, Vec3 direction)
    {
        Vec3 best = vertices[0];
        float bestDot = Vec3.Dot(best, direction);
        for (int i = 1; i < vertices.Length; i++)
        {
            float dot = Vec3.Dot(vertices[i], direction);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = vertices[i];
            }
        }

        return best;
    }

    /// <summary>Vertex average.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Centroid(Vec3[] vertices)
    {
        Vec3 sum = Vec3.Zero;
        for (int i = 0; i < vertices.Length; i++)
        {
            sum += vertices[i];
        }

        return sum * (1.0f / vertices.Length);
    }

    /// <summary>Tight vertex bounds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D Bounds(Vec3[] vertices)
    {
        Vec3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vec3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < vertices.Length; i++)
        {
            min = Vec3.Min(min, vertices[i]);
            max = Vec3.Max(max, vertices[i]);
        }

        return new Aabb3D(min, max);
    }

    /// <summary>Bounding-box mass per the web descriptors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MassData3D Mass(Vec3[] vertices, float density)
    {
        Aabb3D bounds = Bounds(vertices);
        Vec3 full = bounds.Max - bounds.Min;
        float mass = full.X * full.Y * full.Z * density;
        if (mass <= 0.0f)
        {
            return MassData3D.Zero;
        }

        var diagonal = new Vec3(
            (mass * ((full.Y * full.Y) + (full.Z * full.Z))) / 12.0f,
            (mass * ((full.X * full.X) + (full.Z * full.Z))) / 12.0f,
            (mass * ((full.X * full.X) + (full.Y * full.Y))) / 12.0f);
        Vec3 inverse = new(
            diagonal.X > 0.0f ? 1.0f / diagonal.X : 0.0f,
            diagonal.Y > 0.0f ? 1.0f / diagonal.Y : 0.0f,
            diagonal.Z > 0.0f ? 1.0f / diagonal.Z : 0.0f);
        return new MassData3D(mass, 1.0f / mass, diagonal, inverse, (bounds.Min + bounds.Max) * 0.5f);
    }
}

/// <summary>
/// Binds a canonical local shape to its world frame: rotates the query
/// direction into the local frame, evaluates the local support, and maps
/// the witness back. Lets GJK and EPA traverse rotated prisms, cylinders,
/// cones and hulls with zero allocation and no virtual dispatch.
/// </summary>
/// <typeparam name="TShape">The canonical local shape type.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly ref struct OrientedShape<TShape> : ISupportShape3D<OrientedShape<TShape>>
    where TShape : ISupportShape3D<TShape>, allows ref struct
{
    /// <summary>Canonical local shape.</summary>
    public readonly TShape Local;

    /// <summary>Frame carrying the shape to world space.</summary>
    public readonly RigidTransform3D Frame;

    /// <summary>Binds a local shape to its world frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OrientedShape(TShape local, in RigidTransform3D frame)
    {
        Local = local;
        Frame = frame;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Support(in OrientedShape<TShape> shape, in Vec3 direction)
    {
        Vec3 localDirection = shape.Frame.Rotation.Conjugate() * direction;
        Vec3 localSupport = TShape.Support(shape.Local, localDirection);
        return shape.Frame.TransformPoint(localSupport);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ComputeCentroid(in OrientedShape<TShape> shape) =>
        shape.Frame.TransformPoint(TShape.ComputeCentroid(shape.Local));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Aabb3D ComputeBounds(in OrientedShape<TShape> shape)
    {
        Aabb3D local = TShape.ComputeBounds(shape.Local);
        Vec3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vec3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vec3(
                (i & 1) == 0 ? local.Min.X : local.Max.X,
                (i & 2) == 0 ? local.Min.Y : local.Max.Y,
                (i & 4) == 0 ? local.Min.Z : local.Max.Z);
            Vec3 world = shape.Frame.TransformPoint(corner);
            min = Vec3.Min(min, world);
            max = Vec3.Max(max, world);
        }

        return new Aabb3D(min, max);
    }
}
