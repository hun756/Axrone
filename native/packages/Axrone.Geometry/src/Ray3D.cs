namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

/// <summary>
/// Half-line used by every visibility and picking query: an <see cref="Vec3"/> origin
/// plus a direction that is normalized once at construction, so distance along the ray
/// stays in world units and every consumer can assume a unit vector. The per-component
/// reciprocal is stored alongside the direction so slab-style traversal never divides at
/// query time, and components that sit on an axis are guarded instead of producing an
/// infinity that would silently invert the interval.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 16)]
public readonly struct Ray3D : IEquatable<Ray3D>, ISpanFormattable
{
    /// <summary>
    /// Smallest magnitude a direction component may have before the reciprocal is
    /// faked; below this the component is treated as axis-aligned.
    /// </summary>
    private const float ReciprocalEpsilon = 1e-9f;

    private readonly Vec3 _origin;
    private readonly Vec3 _direction;
    private readonly Vec3 _invDirection;

    /// <summary>Point the ray starts at.</summary>
    public Vec3 Origin
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _origin;
    }

    /// <summary>Unit-length travel direction; parallel tests and intersections assume it.</summary>
    public Vec3 Direction
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _direction;
    }

    /// <summary>
    /// Per-component reciprocal of <see cref="Direction"/>, guarded so an axis-aligned
    /// component yields a large finite value with the correct sign rather than infinity.
    /// </summary>
    public Vec3 InvDirection
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _invDirection;
    }

    /// <summary>
    /// Creates a ray from an origin and a travel direction, normalizing the direction and
    /// caching its guarded reciprocal.
    /// </summary>
    /// <param name="origin">The point the ray starts at.</param>
    /// <param name="direction">The travel direction; any non-zero length is accepted.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="direction"/> is zero or so short that normalizing it would leave
    /// the ray direction undefined.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Ray3D(Vec3 origin, Vec3 direction)
    {
        float lenSq = direction.LengthSquared();
        if (lenSq < 1e-12f)
        {
            ThrowHelper.ThrowInvalidRayDirection();
        }

        Vec3 normDir = direction / MathF.Sqrt(lenSq);

        float invX = MathF.Abs(normDir.X) < ReciprocalEpsilon ? (normDir.X >= 0f ? 1f / ReciprocalEpsilon : -1f / ReciprocalEpsilon) : 1f / normDir.X;
        float invY = MathF.Abs(normDir.Y) < ReciprocalEpsilon ? (normDir.Y >= 0f ? 1f / ReciprocalEpsilon : -1f / ReciprocalEpsilon) : 1f / normDir.Y;
        float invZ = MathF.Abs(normDir.Z) < ReciprocalEpsilon ? (normDir.Z >= 0f ? 1f / ReciprocalEpsilon : -1f / ReciprocalEpsilon) : 1f / normDir.Z;

        _origin = origin;
        _direction = normDir;
        _invDirection = new Vec3(invX, invY, invZ);
    }

    /// <summary>Returns the point reached after travelling <paramref name="distance"/> world units.</summary>
    /// <param name="distance">The distance along the ray; negative values travel backwards.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 At(float distance) => Origin + (Direction * distance);

    /// <summary>
    /// Tests the ray against a triangle with the Möller-Trumbore two-sided test, so a hit
    /// is reported no matter which face points at the ray. A miss is a value, not an
    /// exception, so a traversal loop can test candidates without branching on errors.
    /// </summary>
    /// <param name="triangle">The candidate triangle, wound in either direction.</param>
    /// <param name="hit">
    /// The barycentric hit record on a hit; <see langword="default"/> on a miss.
    /// </param>
    /// <returns><see langword="true"/> if the ray reaches the triangle in front of its origin.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in Triangle3 triangle, out TriangleHit3 hit)
    {
        Vec3 dir = Direction;
        Vec3 edge1 = triangle.B - triangle.A;
        Vec3 edge2 = triangle.C - triangle.A;

        Vec3 h = Vec3.Cross(dir, edge2);
        float a = Vec3.Dot(edge1, h);
        if (MathF.Abs(a) < 1e-7f)
        {
            hit = default;
            return false;
        }

        float f = 1f / a;
        Vec3 s = Origin - triangle.A;
        float u = f * Vec3.Dot(s, h);
        if (u < 0f || u > 1f)
        {
            hit = default;
            return false;
        }

        Vec3 q = Vec3.Cross(s, edge1);
        float v = f * Vec3.Dot(dir, q);
        if (v < 0f || u + v > 1f)
        {
            hit = default;
            return false;
        }

        float t = f * Vec3.Dot(edge2, q);
        if (t < 0f)
        {
            hit = default;
            return false;
        }

        Vec3 normal = Vec3.Normalize(Vec3.Cross(edge1, edge2));
        if (Vec3.Dot(normal, dir) > 0f)
        {
            normal = -normal;
        }

        hit = new TriangleHit3(t, u, v, normal, At(t));
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Ray3D other) => _origin == other._origin && _direction == other._direction && _invDirection == other._invDirection;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Ray3D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_origin, _direction, _invDirection);

    /// <summary>Determines whether two rays share an origin, direction and cached reciprocal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in Ray3D left, in Ray3D right) => left.Equals(right);

    /// <summary>Determines whether two rays differ in origin, direction or reciprocal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in Ray3D left, in Ray3D right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <summary>Formats the ray using the given format and provider.</summary>
    /// <param name="format">The component format, or <see langword="null"/> for the default.</param>
    /// <param name="formatProvider">The culture that formats the components.</param>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"Ray3D(Origin={Origin.ToString(format, formatProvider)}, Direction={Direction.ToString(format, formatProvider)})";

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        destination.TryWrite(provider, $"Ray3D(Origin={Origin}, Direction={Direction})", out charsWritten);
}