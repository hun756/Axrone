namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

/// <summary>
/// Bounding sphere in 3D: a center point plus a radius, clamped to a non-negative value
/// so a caller-supplied negative radius can never produce a test that always passes.
/// The center and the radius are the interop contract — callers hand them to native
/// APIs directly — so they are public fields rather than properties.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct BoundingSphere3 : IEquatable<BoundingSphere3>, ISpanFormattable
{
    /// <summary>Center of the sphere.</summary>
    public readonly Vec3 Center;

    /// <summary>Radius of the sphere, never negative.</summary>
    public readonly float Radius;

    /// <summary>Creates a sphere, clamping a negative radius to zero.</summary>
    /// <param name="center">The center of the sphere.</param>
    /// <param name="radius">The requested radius; negative values become zero.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoundingSphere3(Vec3 center, float radius)
    {
        Center = center;
        Radius = MathF.Max(0.0F, radius);
    }

    /// <summary>Determines whether the point lies inside the sphere; surface points count as inside.</summary>
    /// <param name="point">The point to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Vec3 point) => Vec3.DistanceSquared(Center, point) <= (Radius * Radius);

    /// <summary>Determines whether the two spheres overlap or touch.</summary>
    /// <param name="other">The candidate overlapping sphere.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(in BoundingSphere3 other)
    {
        float r = Radius + other.Radius;
        return Vec3.DistanceSquared(Center, other.Center) <= (r * r);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(BoundingSphere3 other) => Center == other.Center && Radius == other.Radius;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is BoundingSphere3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius);

    /// <summary>Determines whether two spheres share a center and a radius.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BoundingSphere3 left, BoundingSphere3 right) => left.Equals(right);

    /// <summary>Determines whether two spheres differ in center or radius.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BoundingSphere3 left, BoundingSphere3 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <summary>Formats the center and the radius using the given format and provider.</summary>
    /// <param name="format">The component format, or <see langword="null"/> for the default.</param>
    /// <param name="formatProvider">The culture that formats the components.</param>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"Sphere3(Center={Center.ToString(format, formatProvider)}, Radius={Radius:F4})";

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        destination.TryWrite(provider, $"Sphere3(Center={Center}, Radius={Radius:F4})", out charsWritten);
}