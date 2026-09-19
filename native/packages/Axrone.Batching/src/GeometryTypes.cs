namespace Axrone.Batching;

/// <summary>
/// Axis-aligned bounding box as two corners.
/// </summary>
/// <remarks>
/// Corners are stored expanded: <c>Min</c> holds the smallest coordinate per axis, <c>Max</c> the
/// largest. The constructor rejects an inverted box so a poisoned min/max pair fails at the
/// producing call rather than surfacing as a NaN three passes later.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Aabb : IEquatable<Aabb>
{
    /// <summary>Smallest corner.</summary>
    public float MinX { get; }

    /// <summary>Smallest corner.</summary>
    public float MinY { get; }

    /// <summary>Smallest corner.</summary>
    public float MinZ { get; }

    /// <summary>Largest corner.</summary>
    public float MaxX { get; }

    /// <summary>Largest corner.</summary>
    public float MaxY { get; }

    /// <summary>Largest corner.</summary>
    public float MaxZ { get; }

    /// <summary>Creates a box from its corners.</summary>
    /// <exception cref="ArgumentException">A min component exceeds its max component.</exception>
    public Aabb(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        if (minX > maxX || minY > maxY || minZ > maxZ)
        {
            ThrowHelper.ThrowArgumentException("Aabb min corner must not exceed max corner.");
        }

        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
    }

    /// <summary>Box center: <c>(min + max) * 0.5</c> per axis.</summary>
    public Vector3 Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f, (MinZ + MaxZ) * 0.5f);

    /// <summary>Box half-extent: <c>(max - min) * 0.5</c> per axis.</summary>
    public Vector3 Extent => new((MaxX - MinX) * 0.5f, (MaxY - MinY) * 0.5f, (MaxZ - MinZ) * 0.5f);

    /// <summary>Rebuilds a box from center and half-extent.</summary>
    public static Aabb FromCenterExtent(Vector3 center, Vector3 extent) => new(
        center.X - extent.X, center.Y - extent.Y, center.Z - extent.Z,
        center.X + extent.X, center.Y + extent.Y, center.Z + extent.Z);

    /// <inheritdoc />
    public bool Equals(Aabb other) =>
        MinX == other.MinX && MinY == other.MinY && MinZ == other.MinZ &&
        MaxX == other.MaxX && MaxY == other.MaxY && MaxZ == other.MaxZ;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Aabb other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(MinX, MinY, MinZ, MaxX, MaxY, MaxZ);

    /// <summary>Value equality.</summary>
    public static bool operator ==(Aabb left, Aabb right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(Aabb left, Aabb right) => !left.Equals(right);
}

/// <summary>
/// Four bone influences for one vertex: weights paired with joint indices.
/// </summary>
/// <remarks>
/// Weights are caller-normalized; the skinning kernel blends them as-is so an unnormalized set
/// scales the vertex rather than failing. Joint indices must be non-negative and are validated
/// here, at fill time, so the per-frame kernel never branches on them.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct BoneInfluence4 : IEquatable<BoneInfluence4>
{
    /// <summary>Weight for <see cref="J0"/>.</summary>
    public float W0 { get; }

    /// <summary>Weight for <see cref="J1"/>.</summary>
    public float W1 { get; }

    /// <summary>Weight for <see cref="J2"/>.</summary>
    public float W2 { get; }

    /// <summary>Weight for <see cref="J3"/>.</summary>
    public float W3 { get; }

    /// <summary>Joint index for <see cref="W0"/>.</summary>
    public int J0 { get; }

    /// <summary>Joint index for <see cref="J1"/>.</summary>
    public int J1 { get; }

    /// <summary>Joint index for <see cref="J2"/>.</summary>
    public int J2 { get; }

    /// <summary>Joint index for <see cref="J3"/>.</summary>
    public int J3 { get; }

    /// <summary>Creates an influence set.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A joint index is negative.</exception>
    public BoneInfluence4(float w0, float w1, float w2, float w3, int j0, int j1, int j2, int j3)
    {
        if (j0 < 0 || j1 < 0 || j2 < 0 || j3 < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(j0));
        }

        W0 = w0;
        W1 = w1;
        W2 = w2;
        W3 = w3;
        J0 = j0;
        J1 = j1;
        J2 = j2;
        J3 = j3;
    }

    /// <inheritdoc />
    public bool Equals(BoneInfluence4 other) =>
        W0 == other.W0 && W1 == other.W1 && W2 == other.W2 && W3 == other.W3 &&
        J0 == other.J0 && J1 == other.J1 && J2 == other.J2 && J3 == other.J3;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BoneInfluence4 other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(HashCode.Combine(W0, W1, W2, W3), HashCode.Combine(J0, J1, J2, J3));

    /// <summary>Value equality.</summary>
    public static bool operator ==(BoneInfluence4 left, BoneInfluence4 right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(BoneInfluence4 left, BoneInfluence4 right) => !left.Equals(right);
}
