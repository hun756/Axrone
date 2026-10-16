namespace Axrone.Physics3D;

/// <summary>
/// One 3D contact point: paired witnesses on each shape, the signed
/// separation (zero or negative while touching), and accumulated solver
/// impulses carried across steps by the stable <see cref="Id"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ContactPoint3D : IEquatable<ContactPoint3D>
{
    /// <summary>Witness on the first shape.</summary>
    public Vec3 PositionA;

    /// <summary>Witness on the second shape.</summary>
    public Vec3 PositionB;

    /// <summary>Signed separation; zero or negative while touching.</summary>
    public float Separation;

    /// <summary>Accumulated normal impulse.</summary>
    public float NormalImpulse;

    /// <summary>Accumulated friction impulse along the first tangent.</summary>
    public float TangentImpulse1;

    /// <summary>Accumulated friction impulse along the second tangent.</summary>
    public float TangentImpulse2;

    /// <summary>Restitution bias frozen once per step from the approach speed.</summary>
    public float VelocityBias;

    /// <summary>Feature fingerprint stabilizing the point across steps.</summary>
    public uint Id;

    /// <summary>Creates a contact point with zeroed impulses.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ContactPoint3D(Vec3 positionA, Vec3 positionB, float separation, uint id)
    {
        PositionA = positionA;
        PositionB = positionB;
        Separation = separation;
        NormalImpulse = 0.0f;
        TangentImpulse1 = 0.0f;
        TangentImpulse2 = 0.0f;
        VelocityBias = 0.0f;
        Id = id;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ContactPoint3D other) =>
        PositionA == other.PositionA && PositionB == other.PositionB && Separation == other.Separation && Id == other.Id;

    /// <summary>Determines whether two contact points match.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ContactPoint3D left, ContactPoint3D right) => left.Equals(right);

    /// <summary>Determines whether two contact points differ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ContactPoint3D left, ContactPoint3D right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ContactPoint3D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(PositionA, PositionB, Separation, Id);
}

/// <summary>
/// One 3D contact manifold: the shared unit normal pointing from the second
/// shape toward the first, plus up to two contact points (Box2D convention).
/// Stacked boxes rest on clipped face pairs; balls collapse to one point.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Manifold3D
{
    /// <summary>Maximum points retained; extra clipped points drop deepest-first.</summary>
    public const int MaxPoints = 2;

    /// <summary>Unit normal pointing from the second shape toward the first.</summary>
    public Vec3 Normal;

    /// <summary>Live points, readable through the indexer.</summary>
    public int Count;

    private ContactPoint3D _point0;
    private ContactPoint3D _point1;

    /// <summary>Reads a live point by index.</summary>
    public ContactPoint3D this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => index == 0 ? _point0 : _point1;
    }

    /// <summary>Writes a live point by index.</summary>
    /// <param name="index">Zero or one.</param>
    /// <param name="value">The point.</param>
    public void SetPoint(int index, ContactPoint3D value)
    {
        if (index == 0)
        {
            _point0 = value;
        }
        else
        {
            _point1 = value;
        }
    }

    /// <summary>Creates a single-point manifold.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Manifold3D One(Vec3 normal, ContactPoint3D point) =>
        new() { Normal = normal, Count = 1, _point0 = point };

    /// <summary>Creates a two-point manifold.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Manifold3D Pair(Vec3 normal, ContactPoint3D first, ContactPoint3D second) =>
        new() { Normal = normal, Count = 2, _point0 = first, _point1 = second };

    /// <summary>Empty manifold; no contact.</summary>
    public static Manifold3D Empty => new() { Normal = Vec3.Zero, Count = 0 };
}
