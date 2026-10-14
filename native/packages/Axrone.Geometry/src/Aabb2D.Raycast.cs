namespace Axrone.Geometry;

/// <summary>
/// Ray query surface of <see cref="Aabb2D"/>: slab tests against the
/// professional 2D ray, mirroring the 3D box.
/// </summary>
public readonly partial struct Aabb2D
{
    /// <summary>
    /// Tests the ray against the box with no distance limit, accepting the first contact in
    /// front of the origin.
    /// </summary>
    /// <param name="ray">The ray to test; its reciprocal direction must be the cached one.</param>
    /// <param name="distance">
    /// The distance to the first contact, or zero when the ray starts inside the box.
    /// </param>
    /// <returns><see langword="true"/> if the ray reaches the box.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects(in Ray2D ray, out float distance) => Intersects(in ray, float.PositiveInfinity, out distance);

    /// <summary>
    /// Tests the ray against the box with the scalar slab method: each axis contributes an
    /// interval, and the ray hits when those intervals overlap within
    /// <paramref name="maxDistance"/>. The reciprocal direction is read from
    /// <see cref="Ray2D.InvDirection"/>, which the ray guards against a zero or
    /// near-parallel component, so no division happens here.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="maxDistance">The furthest distance along the ray that still counts as a hit.</param>
    /// <param name="distance">
    /// The distance to the first contact, or zero when the ray starts inside the box.
    /// </param>
    /// <returns><see langword="true"/> if the ray reaches the box within the limit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(in Ray2D ray, float maxDistance, out float distance)
    {
        float t1x = (Min.X - ray.Origin.X) * ray.InvDirection.X;
        float t2x = (Max.X - ray.Origin.X) * ray.InvDirection.X;
        float t1y = (Min.Y - ray.Origin.Y) * ray.InvDirection.Y;
        float t2y = (Max.Y - ray.Origin.Y) * ray.InvDirection.Y;

        float tminx = MathF.Min(t1x, t2x);
        float tmaxx = MathF.Max(t1x, t2x);
        float tminy = MathF.Min(t1y, t2y);
        float tmaxy = MathF.Max(t1y, t2y);

        float tNear = MathF.Max(tminx, tminy);
        float tFar = MathF.Min(tmaxx, tmaxy);

        if (tFar >= MathF.Max(tNear, 0.0F) && tNear <= maxDistance)
        {
            distance = tNear < 0.0F ? 0.0F : tNear;
            return true;
        }

        distance = 0.0F;
        return false;
    }
}
