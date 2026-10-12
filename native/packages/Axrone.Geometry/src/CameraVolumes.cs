namespace Axrone.Geometry;

/// <summary>Which shape a <see cref="BoundingVolume"/> carries.</summary>
public enum BoundingVolumeKind : byte
{
    /// <summary>Sphere shape.</summary>
    Sphere = 0,

    /// <summary>Box shape.</summary>
    Aabb = 1,
}

/// <summary>
/// Discriminated union of the culler-ready shapes: one sphere or one box in
/// overlapping storage, dispatched by <see cref="Kind"/>. Heterogeneous
/// batches cull through one generic instantiation instead of one per shape.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public readonly struct BoundingVolume : IBoundingVolume<BoundingVolume>, IEquatable<BoundingVolume>
{
    /// <summary>Which shape is stored.</summary>
    [FieldOffset(0)]
    public readonly BoundingVolumeKind Kind;

    /// <summary>Sphere payload; meaningful when <see cref="Kind"/> is <see cref="BoundingVolumeKind.Sphere"/>.</summary>
    [FieldOffset(4)]
    public readonly BoundingSphere3 Sphere;

    /// <summary>Box payload; meaningful when <see cref="Kind"/> is <see cref="BoundingVolumeKind.Aabb"/>.</summary>
    [FieldOffset(4)]
    public readonly Aabb3D Aabb;

    /// <inheritdoc/>
    public static string KindIdentifier => "polymorphic";

    /// <summary>Wraps a sphere.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoundingVolume(BoundingSphere3 sphere)
    {
        Unsafe.SkipInit(out this);
        Kind = BoundingVolumeKind.Sphere;
        Sphere = sphere;
    }

    /// <summary>Wraps a box.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoundingVolume(Aabb3D aabb)
    {
        Unsafe.SkipInit(out this);
        Kind = BoundingVolumeKind.Aabb;
        Aabb = aabb;
    }

    /// <summary>Wraps a sphere.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BoundingVolume(BoundingSphere3 sphere) => new(sphere);

    /// <summary>Wraps a box.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BoundingVolume(Aabb3D aabb) => new(aabb);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ContainmentType Classify(in CameraFrustum frustum) => Kind switch
    {
        BoundingVolumeKind.Sphere => frustum.ClassifySphere(in Sphere),
        BoundingVolumeKind.Aabb => frustum.ClassifyAabb(in Aabb),
        _ => ThrowHelper.ThrowInvalidOperation<ContainmentType>("Corrupted bounding-volume discriminator."),
    };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in CameraFrustum frustum) => Kind switch
    {
        BoundingVolumeKind.Sphere => frustum.IntersectsSphere(in Sphere),
        BoundingVolumeKind.Aabb => frustum.IntersectsAabb(in Aabb),
        _ => ThrowHelper.ThrowInvalidOperation<bool>("Corrupted bounding-volume discriminator."),
    };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(BoundingVolume other)
    {
        if (Kind != other.Kind)
        {
            return false;
        }

        return Kind switch
        {
            BoundingVolumeKind.Sphere => Sphere.Equals(other.Sphere),
            BoundingVolumeKind.Aabb => Aabb.Equals(other.Aabb),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BoundingVolume other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Kind switch
    {
        BoundingVolumeKind.Sphere => HashCode.Combine((byte)Kind, Sphere),
        BoundingVolumeKind.Aabb => HashCode.Combine((byte)Kind, Aabb),
        _ => 0,
    };

    /// <summary>Determines whether two unions carry the same shape and payload.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BoundingVolume left, BoundingVolume right) => left.Equals(right);

    /// <summary>Determines whether two unions differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BoundingVolume left, BoundingVolume right) => !left.Equals(right);
}

/// <summary>Culler contract of the professional 3D box.</summary>
public readonly partial struct Aabb3D : IBoundingVolume<Aabb3D>
{
    /// <inheritdoc/>
    public static string KindIdentifier => "aabb";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ContainmentType Classify(in CameraFrustum frustum) => frustum.ClassifyAabb(in this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in CameraFrustum frustum) => frustum.IntersectsAabb(in this);
}

/// <summary>Culler contract of the professional 3D sphere.</summary>
public readonly partial struct BoundingSphere3 : IBoundingVolume<BoundingSphere3>
{
    /// <inheritdoc/>
    public static string KindIdentifier => "sphere";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ContainmentType Classify(in CameraFrustum frustum) => frustum.ClassifySphere(in this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in CameraFrustum frustum) => frustum.IntersectsSphere(in this);
}
