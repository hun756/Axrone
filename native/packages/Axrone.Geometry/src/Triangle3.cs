namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Triangle in 3D space, defined by its three corner vertices. The corners are public
/// fields rather than properties because mesh building hands them straight to index and
/// vertex buffers, and the winding order is part of the value: it decides which way
/// <see cref="Normal"/> points.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Triangle3 : IEquatable<Triangle3>
{
    /// <summary>First corner, the tail of the winding order.</summary>
    public readonly Vec3 A;

    /// <summary>Second corner.</summary>
    public readonly Vec3 B;

    /// <summary>Third corner, the head of the winding order.</summary>
    public readonly Vec3 C;

    /// <summary>Creates a triangle from its corners; the order fixes the winding.</summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Triangle3(Vec3 a, Vec3 b, Vec3 c)
    {
        A = a;
        B = b;
        C = c;
    }

    /// <summary>
    /// Unit surface normal derived from the right-hand winding <c>A to B to C</c>, so
    /// reversing the corners reverses the normal.
    /// </summary>
    public Vec3 Normal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vec3.Normalize(Vec3.Cross(B - A, C - A));
    }

    /// <summary>Creates the tightest box that contains all three corners.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aabb3D GetBounds() => new(Vec3.Min(A, Vec3.Min(B, C)), Vec3.Max(A, Vec3.Max(B, C)));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Triangle3 other) => A == other.A && B == other.B && C == other.C;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Triangle3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(A, B, C);

    /// <summary>Determines whether two triangles have the same corners in the same winding.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Triangle3 left, Triangle3 right) => left.Equals(right);

    /// <summary>Determines whether two triangles differ in corners or winding.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Triangle3 left, Triangle3 right) => !left.Equals(right);
}