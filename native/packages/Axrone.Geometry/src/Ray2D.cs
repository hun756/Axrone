namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

/// <summary>
/// Half-line used by every 2D visibility and picking query: a <see cref="Vec2"/> origin
/// plus a direction that is normalized once at construction, so distance along the ray
/// stays in world units and every consumer can assume a unit vector. The per-component
/// reciprocal is stored alongside the direction so slab-style traversal never divides at
/// query time, and components that sit on an axis are guarded instead of producing an
/// infinity that would silently invert the interval.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Ray2D : IEquatable<Ray2D>, ISpanFormattable
{
    /// <summary>
    /// Smallest magnitude a direction component may have before the reciprocal is
    /// faked; below this the component is treated as axis-aligned.
    /// </summary>
    private const float ReciprocalEpsilon = 1e-9f;

    private readonly Vec2 _origin;
    private readonly Vec2 _direction;
    private readonly Vec2 _invDirection;

    /// <summary>Point the ray starts at.</summary>
    public Vec2 Origin
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _origin;
    }

    /// <summary>Unit-length travel direction; parallel tests and intersections assume it.</summary>
    public Vec2 Direction
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _direction;
    }

    /// <summary>
    /// Per-component reciprocal of <see cref="Direction"/>, guarded so an axis-aligned
    /// component yields a large finite value with the correct sign rather than infinity.
    /// </summary>
    public Vec2 InvDirection
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
    public Ray2D(Vec2 origin, Vec2 direction)
    {
        float lenSq = direction.LengthSquared();
        if (lenSq < 1e-12f)
        {
            ThrowHelper.ThrowInvalidRayDirection();
        }

        Vec2 normDir = direction / MathF.Sqrt(lenSq);

        float invX = MathF.Abs(normDir.X) < ReciprocalEpsilon ? (normDir.X >= 0f ? 1f / ReciprocalEpsilon : -1f / ReciprocalEpsilon) : 1f / normDir.X;
        float invY = MathF.Abs(normDir.Y) < ReciprocalEpsilon ? (normDir.Y >= 0f ? 1f / ReciprocalEpsilon : -1f / ReciprocalEpsilon) : 1f / normDir.Y;

        _origin = origin;
        _direction = normDir;
        _invDirection = new Vec2(invX, invY);
    }

    /// <summary>Returns the point reached after travelling <paramref name="distance"/> world units.</summary>
    /// <param name="distance">The distance along the ray; negative values travel backwards.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2 At(float distance) => Origin + (Direction * distance);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Ray2D other) => _origin == other._origin && _direction == other._direction && _invDirection == other._invDirection;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Ray2D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_origin, _direction, _invDirection);

    /// <summary>Determines whether two rays share an origin, direction and cached reciprocal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in Ray2D left, in Ray2D right) => left.Equals(right);

    /// <summary>Determines whether two rays differ in origin, direction or reciprocal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in Ray2D left, in Ray2D right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <summary>Formats the ray using the given format and provider.</summary>
    /// <param name="format">The component format, or <see langword="null"/> for the default.</param>
    /// <param name="formatProvider">The culture that formats the components.</param>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"Ray2D(Origin={Origin.ToString(format, formatProvider)}, Direction={Direction.ToString(format, formatProvider)})";

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        destination.TryWrite(provider, $"Ray2D(Origin={Origin}, Direction={Direction})", out charsWritten);
}
