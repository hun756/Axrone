using System.Globalization;

namespace Axrone.Render.OpenGL.Mesh;

/// <summary>
/// Axis-aligned bounding box (AABB) for 3D mesh bounds.
/// </summary>
public readonly record struct Bounds3D(Vector3 Min, Vector3 Max)
{

    /// <summary>
    /// Gets the center of the bounding box.
    /// </summary>
    public Vector3 Center
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Min + Max) * 0.5f;
    }

    /// <summary>
    /// Gets the half-size (extents) of the bounding box.
    /// </summary>
    public Vector3 Extents
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Max - Min) * 0.5f;
    }

    /// <summary>
    /// Gets the full size of the bounding box.
    /// </summary>
    public Vector3 Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Max - Min;
    }

    /// <summary>
    /// Gets an empty bounds suitable for expansion via <see cref="Expand(Vector3)"/>.
    /// Min is <see cref="float.MaxValue"/>, Max is <see cref="float.MinValue"/>.
    /// </summary>
    public static Bounds3D Empty { get; } = new(
        new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
        new Vector3(float.MinValue, float.MinValue, float.MinValue));

    /// <summary>
    /// Creates a bounding box that encloses all the specified points.
    /// </summary>
    /// <param name="points">The points to enclose.</param>
    /// <returns>A bounding box enclosing all points.</returns>
    public static Bounds3D FromPoints(ReadOnlySpan<Vector3> points)
    {
        if (points.Length == 0)
            return Empty;

        var min = points[0];
        var max = points[0];

        for (int i = 1; i < points.Length; i++)
        {
            min = Vector3.Min(min, points[i]);
            max = Vector3.Max(max, points[i]);
        }

        return new Bounds3D(min, max);
    }

    /// <summary>
    /// Returns a new bounds expanded to include the specified point.
    /// </summary>
    /// <param name="point">The point to include.</param>
    /// <returns>A new expanded bounding box.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bounds3D Expand(Vector3 point) =>
        new(Vector3.Min(Min, point), Vector3.Max(Max, point));

    /// <summary>
    /// Returns a new bounds expanded to include all the specified points.
    /// </summary>
    /// <param name="points">The points to include.</param>
    /// <returns>A new expanded bounding box.</returns>
    public Bounds3D Expand(ReadOnlySpan<Vector3> points)
    {
        var min = Min;
        var max = Max;

        for (int i = 0; i < points.Length; i++)
        {
            min = Vector3.Min(min, points[i]);
            max = Vector3.Max(max, points[i]);
        }

        return new Bounds3D(min, max);
    }

    /// <summary>
    /// Determines whether the specified point is inside this bounding box.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c> if the point is inside the bounds; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Vector3 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y &&
        point.Z >= Min.Z && point.Z <= Max.Z;

    /// <summary>
    /// Determines whether this bounding box intersects with another.
    /// </summary>
    /// <param name="other">The other bounding box.</param>
    /// <returns><c>true</c> if the bounds intersect; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(Bounds3D other) =>
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

    /// <summary>
    /// Returns a new bounding box that is the merge of this and another bounding box.
    /// </summary>
    /// <param name="other">The other bounding box.</param>
    /// <returns>A merged bounding box enclosing both.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bounds3D Merge(Bounds3D other) =>
        new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

    /// <inheritdoc/>
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "Bounds3D: Min({0:F3}, {1:F3}, {2:F3}) Max({3:F3}, {4:F3}, {5:F3})",
            Min.X, Min.Y, Min.Z, Max.X, Max.Y, Max.Z);
}
