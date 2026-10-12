namespace Axrone.Physics2D;

/// <summary>
/// One ray hit against a shape: who was hit, where, facing which way, and
/// how far along the ray. Distances share the ray's world units.
/// </summary>
/// <param name="Shape">Hit shape.</param>
/// <param name="Body">Body owning the shape.</param>
/// <param name="Point">World contact point.</param>
/// <param name="Normal">Unit surface normal at the contact, facing the ray.</param>
/// <param name="Distance">Distance from the ray origin; never negative.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RaycastHit2D(ShapeId Shape, BodyId Body, Vec2 Point, Vec2 Normal, float Distance);

/// <summary>
/// Ray tests against world-space shapes: analytic disc, swept-stadium
/// (caps plus strip) and half-plane-clipped convex polygon. Every test
/// takes a maximum travel distance, reports the nearest contact at or
/// past the origin, and faces the normal back at the ray.
/// </summary>
public static class ShapeRaycaster
{
    private const float ParallelEpsilon = 1e-9f;
    private const float DegenerateLengthSquared = 1e-12f;

    /// <summary>Tests a ray against a disc.</summary>
    /// <param name="ray">The query ray; its direction is unit length by construction.</param>
    /// <param name="center">World center of the disc.</param>
    /// <param name="radius">Radius of the disc.</param>
    /// <param name="maxDistance">Travel limit; contacts past it are ignored.</param>
    /// <param name="distance">Distance from the ray origin to the contact.</param>
    /// <param name="normal">Unit surface normal at the contact, facing the ray.</param>
    /// <param name="point">World contact point.</param>
    /// <returns><see langword="true"/> with the nearest valid contact.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool RaycastCircle(
        in Ray2D ray,
        Vec2 center,
        float radius,
        float maxDistance,
        out float distance,
        out Vec2 normal,
        out Vec2 point)
    {
        Vec2 toCenter = center - ray.Origin;
        float along = Vec2.Dot(toCenter, ray.Direction);
        float offSq = Vec2.Dot(toCenter, toCenter) - (along * along);

        if (offSq > radius * radius)
        {
            distance = 0.0f;
            normal = default;
            point = default;
            return false;
        }

        float halfChord = MathF.Sqrt(Math.Max(radius * radius - offSq, 0.0f));
        float t = along - halfChord;
        if (t < 0.0f)
        {
            t = along + halfChord;
        }

        if (t < 0.0f || t > maxDistance)
        {
            distance = 0.0f;
            normal = default;
            point = default;
            return false;
        }

        point = ray.At(t);
        normal = radius > 1e-9f ? (point - center) * (1.0f / radius) : ray.Direction * -1.0f;
        distance = t;
        return true;
    }

    /// <summary>Tests a ray against a stadium: both caps plus the strip between them.</summary>
    /// <param name="ray">The query ray; its direction is unit length by construction.</param>
    /// <param name="p0">World center of the first cap.</param>
    /// <param name="p1">World center of the second cap.</param>
    /// <param name="radius">Cap radius of the stadium.</param>
    /// <param name="maxDistance">Travel limit; contacts past it are ignored.</param>
    /// <param name="distance">Distance from the ray origin to the contact.</param>
    /// <param name="normal">Unit surface normal at the contact, facing the ray.</param>
    /// <param name="point">World contact point.</param>
    /// <returns><see langword="true"/> with the nearest valid contact.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool RaycastCapsule(
        in Ray2D ray,
        Vec2 p0,
        Vec2 p1,
        float radius,
        float maxDistance,
        out float distance,
        out Vec2 normal,
        out Vec2 point)
    {
        float best = float.PositiveInfinity;
        Vec2 bestNormal = default;
        Vec2 bestPoint = default;

        if (RaycastCircle(in ray, p0, radius, maxDistance, out float d0, out Vec2 n0, out Vec2 q0) && d0 < best)
        {
            best = d0;
            bestNormal = n0;
            bestPoint = q0;
        }

        if (RaycastCircle(in ray, p1, radius, maxDistance, out float d1, out Vec2 n1, out Vec2 q1) && d1 < best)
        {
            best = d1;
            bestNormal = n1;
            bestPoint = q1;
        }

        Vec2 axis = p1 - p0;
        float lengthSq = axis.LengthSquared();
        if (lengthSq >= DegenerateLengthSquared)
        {
            float length = MathF.Sqrt(lengthSq);
            Vec2 side = new(-axis.Y / length, axis.X / length);
            float denom = Vec2.Dot(ray.Direction, side);
            if (MathF.Abs(denom) >= ParallelEpsilon)
            {
                float t = Vec2.Dot(p0 - ray.Origin, side) / denom;
                if (t >= 0.0f && t <= maxDistance && t < best)
                {
                    Vec2 basePoint = ray.At(t);
                    float span = Vec2.Dot(basePoint - p0, axis) / lengthSq;
                    if (span >= 0.0f && span <= 1.0f)
                    {
                        Vec2 facing = denom > 0.0f ? side * -1.0f : side;
                        best = t;
                        bestNormal = facing;
                        bestPoint = basePoint + (facing * radius);
                    }
                }
            }
        }

        if (best == float.PositiveInfinity)
        {
            distance = 0.0f;
            normal = default;
            point = default;
            return false;
        }

        distance = best;
        normal = bestNormal;
        point = bestPoint;
        return true;
    }

    /// <summary>Tests a ray against a convex polygon with Cyrus-Beck half-plane clipping.</summary>
    /// <param name="ray">The query ray; its direction is unit length by construction.</param>
    /// <param name="vertices">World-space vertices of the convex loop.</param>
    /// <param name="normals">Unit outward edge normals, one per edge starting at the same index.</param>
    /// <param name="maxDistance">Travel limit; contacts past it are ignored.</param>
    /// <param name="distance">Distance from the ray origin to the contact.</param>
    /// <param name="normal">Unit surface normal at the contact, facing the ray.</param>
    /// <param name="point">World contact point.</param>
    /// <returns><see langword="true"/> with the entering contact.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool RaycastPolygon(
        in Ray2D ray,
        ReadOnlySpan<Vec2> vertices,
        ReadOnlySpan<Vec2> normals,
        float maxDistance,
        out float distance,
        out Vec2 normal,
        out Vec2 point)
    {
        float enter = float.NegativeInfinity;
        float exit = float.PositiveInfinity;
        Vec2 enterNormal = default;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vec2 outward = normals[i];
            float denom = Vec2.Dot(outward, ray.Direction);
            float numer = Vec2.Dot(outward, vertices[i] - ray.Origin);

            if (MathF.Abs(denom) < ParallelEpsilon)
            {
                if (numer < 0.0f)
                {
                    distance = 0.0f;
                    normal = default;
                    point = default;
                    return false;
                }

                continue;
            }

            float t = numer / denom;
            if (denom < 0.0f)
            {
                if (t > enter)
                {
                    enter = t;
                    enterNormal = outward;
                }
            }
            else if (t < exit)
            {
                exit = t;
            }
        }

        if (enter > exit || exit < 0.0f)
        {
            distance = 0.0f;
            normal = default;
            point = default;
            return false;
        }

        if (enter <= 0.0f)
        {
            distance = 0.0f;
            normal = ray.Direction * -1.0f;
            point = ray.Origin;
            return true;
        }

        if (enter > maxDistance)
        {
            distance = 0.0f;
            normal = default;
            point = default;
            return false;
        }

        distance = enter;
        normal = enterNormal;
        point = ray.At(enter);
        return true;
    }
}
