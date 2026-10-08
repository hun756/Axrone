namespace Axrone.Batching;

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
