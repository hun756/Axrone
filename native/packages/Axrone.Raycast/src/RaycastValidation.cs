namespace Axrone.Raycast;

using Axrone.Geometry;

/// <summary>
/// Cold-path guards shared by both raycast engines. Every rejection funnels into a
/// non-inlined throw helper so the hot traversal keeps its inline budget.
/// </summary>
internal static class RaycastValidation
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(in RaycastQuery2D query)
    {
        Ray2D ray = query.Ray;
        ValidateRay(in ray, query.MaxDistance);
        ValidateHits(query.Flags, query.MaxHits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(in RaycastQuery2D query, int resultLength)
    {
        if (resultLength <= 0)
        {
            ThrowHelper.ThrowArgumentEmpty(nameof(resultLength));
        }

        Validate(in query);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(in RaycastQuery3D query)
    {
        Ray3D ray = query.Ray;
        ValidateRay(in ray, query.MaxDistance);
        ValidateHits(query.Flags, query.MaxHits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(in RaycastQuery3D query, int resultLength)
    {
        if (resultLength <= 0)
        {
            ThrowHelper.ThrowArgumentEmpty(nameof(resultLength));
        }

        Validate(in query);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateRay(in Ray2D ray, float maxDistance)
    {
        if (!float.IsFinite(ray.Origin.X) || !float.IsFinite(ray.Origin.Y) ||
            !float.IsFinite(ray.Direction.X) || !float.IsFinite(ray.Direction.Y) ||
            ray.Direction.LengthSquared() < 1e-12f)
        {
            ThrowHelper.ThrowInvalidQuery("Ray origin or direction is not finite.");
        }

        if (!float.IsFinite(maxDistance) || maxDistance <= 0f)
        {
            ThrowHelper.ThrowInvalidQuery("Ray max distance must be positive and finite.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateRay(in Ray3D ray, float maxDistance)
    {
        if (!float.IsFinite(ray.Origin.X) || !float.IsFinite(ray.Origin.Y) || !float.IsFinite(ray.Origin.Z) ||
            !float.IsFinite(ray.Direction.X) || !float.IsFinite(ray.Direction.Y) || !float.IsFinite(ray.Direction.Z) ||
            ray.Direction.LengthSquared() < 1e-12f)
        {
            ThrowHelper.ThrowInvalidQuery("Ray origin or direction is not finite.");
        }

        if (!float.IsFinite(maxDistance) || maxDistance <= 0f)
        {
            ThrowHelper.ThrowInvalidQuery("Ray max distance must be positive and finite.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateCastVolume(in RaycastShape3D castVolume)
    {
        if (castVolume.Kind is not (RaycastShapeKind.Sphere or RaycastShapeKind.Aabb or RaycastShapeKind.Box))
        {
            ThrowHelper.ThrowInvalidQuery("A shape cast volume must be a sphere, axis-aligned box or oriented box.");
        }

        if (!IsFinite(castVolume.WorldHalfExtents))
        {
            ThrowHelper.ThrowInvalidQuery("A shape cast volume must have finite extents.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsClosest(RaycastFlags flags) => (flags & RaycastFlags.ClosestOnly) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateHits(RaycastFlags flags, int maxHits)
    {
        if (!IsClosest(flags) && maxHits < 1)
        {
            ThrowHelper.ThrowInvalidQuery("All-hits queries require a positive hit limit.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFinite(Axrone.Numeric.Vec3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
