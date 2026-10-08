namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

/// <summary>
/// Axis-aligned 2D bounding box expressed as inclusive minimum and maximum corners.
/// The corners are the interop contract — callers hand them to native APIs and SIMD
/// loads directly — so they are public fields rather than properties.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct Aabb2D : IEquatable<Aabb2D>, ISpanFormattable
{
    /// <summary>Inclusive lower corner; the smallest coordinates the box spans.</summary>
    public readonly Vec2 Min;

    /// <summary>Inclusive upper corner; the largest coordinates the box spans.</summary>
    public readonly Vec2 Max;

    /// <summary>
    /// An inverted box that any merge or expansion absorbs: every corner is infinite in
    /// the opposing direction, so it contributes nothing to the result.
    /// </summary>
    public static Aabb2D Empty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(new Vec2(float.PositiveInfinity), new Vec2(float.NegativeInfinity));
    }

    /// <summary>A degenerate box at the origin; valid, with zero size and zero area.</summary>
    public static Aabb2D Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Vec2.Zero, Vec2.Zero);
    }

    /// <summary>Creates a box from its inclusive corners.</summary>
    /// <param name="min">The inclusive lower corner.</param>
    /// <param name="max">The inclusive upper corner.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aabb2D(Vec2 min, Vec2 max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>Creates a box from its inclusive corner components.</summary>
    /// <param name="minX">The smallest X coordinate the box spans.</param>
    /// <param name="minY">The smallest Y coordinate the box spans.</param>
    /// <param name="maxX">The largest X coordinate the box spans.</param>
    /// <param name="maxY">The largest Y coordinate the box spans.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aabb2D(float minX, float minY, float maxX, float maxY)
    {
        Min = new Vec2(minX, minY);
        Max = new Vec2(maxX, maxY);
    }

    /// <summary>Midpoint of the box.</summary>
    public Vec2 Center
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Min + Max) * 0.5F;
    }

    /// <summary>Half the box size per axis; negative for any inverted axis.</summary>
    public Vec2 Extents
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Max - Min) * 0.5F;
    }

    /// <summary>Full size per axis; negative for any inverted axis.</summary>
    public Vec2 Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Max - Min;
    }

    /// <summary>
    /// Enclosed area, clamped at zero per axis so an inverted or degenerate box reports
    /// zero instead of a negative or nonsensical size.
    /// </summary>
    public float Area
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Vec2 d = Size;
            return MathF.Max(0, d.X) * MathF.Max(0, d.Y);
        }
    }

    /// <summary>Whether the corners are ordered, that is <see cref="Min"/> does not exceed <see cref="Max"/> on any axis.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Min.X <= Max.X && Min.Y <= Max.Y;
    }

    /// <summary>Determines whether the point lies inside the box; boundary points count as inside.</summary>
    /// <param name="point">The point to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Vec2 point) =>
        point.X >= Min.X &&
        point.X <= Max.X &&
        point.Y >= Min.Y &&
        point.Y <= Max.Y;

    /// <summary>Determines whether the other box lies wholly inside this one; shared boundaries count as inside.</summary>
    /// <param name="other">The candidate inner box.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in Aabb2D other) =>
        other.Min.X >= Min.X &&
        other.Max.X <= Max.X &&
        other.Min.Y >= Min.Y &&
        other.Max.Y <= Max.Y;

    /// <summary>Determines whether the two boxes share at least one point; touching edges count as overlapping.</summary>
    /// <param name="other">The candidate overlapping box.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Overlaps(in Aabb2D other) =>
        other.Min.X <= Max.X &&
        other.Max.X >= Min.X &&
        other.Min.Y <= Max.Y &&
        other.Max.Y >= Min.Y;

    /// <summary>Returns the smallest box containing both boxes.</summary>
    /// <param name="other">The box to union with this one.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aabb2D MergedWith(in Aabb2D other) => new(Vec2.Min(Min, other.Min), Vec2.Max(Max, other.Max));

    /// <summary>Returns the box grown by the given amount on every side.</summary>
    /// <param name="amount">The per-side growth, in world units.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aabb2D Expanded(float amount) => new(Min - new Vec2(amount), Max + new Vec2(amount));

    /// <summary>Returns the point inside the box nearest to the input; points inside are returned unchanged.</summary>
    /// <param name="point">The point to project.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2 ClosestPoint(Vec2 point) => Vec2.Clamp(point, Min, Max);

    /// <summary>Returns the squared distance from the point to the box; zero for points inside.</summary>
    /// <param name="point">The point to measure from.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceSquared(Vec2 point) => Vec2.DistanceSquared(point, ClosestPoint(point));

    /// <summary>
    /// Sweeps the box along an axis into 3D, keeping the planar corners on the two remaining
    /// axes and placing the depth interval on the extrusion axis.
    /// </summary>
    /// <param name="minDepth">The lower depth along <paramref name="extrusionAxis"/>.</param>
    /// <param name="maxDepth">The upper depth along <paramref name="extrusionAxis"/>.</param>
    /// <param name="extrusionAxis">The axis the depth interval is mapped onto.</param>
    /// <exception cref="ArgumentOutOfRangeException">The axis is not one of the three coordinate axes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aabb3D Extrude(float minDepth, float maxDepth, Axis extrusionAxis) =>
        extrusionAxis switch
        {
            Axis.X => new Aabb3D(new Vec3(minDepth, Min.X, Min.Y), new Vec3(maxDepth, Max.X, Max.Y)),
            Axis.Y => new Aabb3D(new Vec3(Min.X, minDepth, Min.Y), new Vec3(Max.X, maxDepth, Max.Y)),
            Axis.Z => new Aabb3D(new Vec3(Min.X, Min.Y, minDepth), new Vec3(Max.X, Max.Y, maxDepth)),
            _ => ThrowHelper.ThrowInvalidAxis<Aabb3D>(),
        };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Aabb2D other) => Min == other.Min && Max == other.Max;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Aabb2D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>Determines whether two boxes span the same corners.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Aabb2D left, Aabb2D right) => left.Equals(right);

    /// <summary>Determines whether two boxes span different corners.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Aabb2D left, Aabb2D right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <summary>Formats the corners using the given format and provider.</summary>
    /// <param name="format">The component format, or <see langword="null"/> for the default.</param>
    /// <param name="formatProvider">The culture that formats the components.</param>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"Aabb2D(Min={Min.ToString(format, formatProvider)}, Max={Max.ToString(format, formatProvider)})";

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        destination.TryWrite(provider, $"Aabb2D(Min={Min}, Max={Max})", out charsWritten);
}