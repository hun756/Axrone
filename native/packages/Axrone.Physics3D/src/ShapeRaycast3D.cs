namespace Axrone.Physics3D;

/// <summary>Exact ray hit against world shapes: identity, distance, point and face normal.</summary>
/// <param name="Shape">The shape hit.</param>
/// <param name="Body">The owning body.</param>
/// <param name="Point">World hit point.</param>
/// <param name="Normal">World face normal at the hit.</param>
/// <param name="Distance">Ray distance to the hit.</param>
public readonly record struct RaycastHit3D(ShapeId Shape, BodyId Body, Vec3 Point, Vec3 Normal, float Distance);

/// <summary>
/// Exact ray casts against world-space shapes. The broadphase tree admits
/// AABB candidates; these routines pin the exact hit point, normal and
/// distance. Misses report <see langword="false"/> and leave outputs
/// untouched.
/// </summary>
public static class ShapeRaycast3D
{
    /// <summary>Casts a ray against a world ball.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool RaySphere(in Ray3D ray, float maxDistance, in Sphere3 sphere, out float distance, out Vec3 normal)
    {
        Vec3 offset = ray.Origin - sphere.Center;
        float halfB = Vec3.Dot(offset, ray.Direction);
        float c = offset.LengthSquared() - (sphere.Radius * sphere.Radius);

        if (c < 0.0f)
        {
            distance = 0.0f;
            normal = Vec3.Normalize(offset);
            return true;
        }

        float discriminant = (halfB * halfB) - c;
        if (discriminant < 0.0f)
        {
            distance = 0.0f;
            normal = Vec3.Zero;
            return false;
        }

        float hit = -halfB - MathF.Sqrt(discriminant);
        if (hit < 0.0f || hit > maxDistance)
        {
            distance = 0.0f;
            normal = Vec3.Zero;
            return false;
        }

        distance = hit;
        normal = Vec3.Normalize((ray.Origin + (ray.Direction * hit)) - sphere.Center);
        return true;
    }

    /// <summary>Casts a ray against a framed box.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool RayBox(
        in Ray3D ray, float maxDistance, in Box3 box, in RigidTransform3D frame,
        out float distance, out Vec3 normal)
    {
        Vec3 localOrigin = frame.InverseTransformPoint(ray.Origin);
        Vec3 localDirection = frame.Rotation.Conjugate() * ray.Direction;

        float tMin = 0.0f;
        float tMax = maxDistance;
        Vec3 hitNormal = Vec3.Zero;

        if (!Slab(localOrigin.X, localDirection.X, box.HalfExtents.X, new Vec3(1, 0, 0), ref tMin, ref tMax, ref hitNormal) ||
            !Slab(localOrigin.Y, localDirection.Y, box.HalfExtents.Y, new Vec3(0, 1, 0), ref tMin, ref tMax, ref hitNormal) ||
            !Slab(localOrigin.Z, localDirection.Z, box.HalfExtents.Z, new Vec3(0, 0, 1), ref tMin, ref tMax, ref hitNormal))
        {
            distance = 0.0f;
            normal = Vec3.Zero;
            return false;
        }

        distance = tMin;
        normal = frame.Rotation * hitNormal;
        return true;
    }

    /// <summary>Clips the ray interval against one slab; tracks the entry-face normal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool Slab(
        float origin, float direction, float halfExtent, Vec3 axis,
        ref float tMin, ref float tMax, ref Vec3 hitNormal)
    {
        if (MathF.Abs(direction) < 1e-9f)
        {
            return origin >= -halfExtent && origin <= halfExtent;
        }

        float t0 = (-halfExtent - origin) / direction;
        float t1 = (halfExtent - origin) / direction;
        Vec3 faceNormal = axis * (direction > 0.0f ? -1.0f : 1.0f);
        if (t0 > t1)
        {
            (t0, t1) = (t1, t0);
            faceNormal = faceNormal * -1.0f;
        }

        if (t0 > tMin)
        {
            tMin = t0;
            hitNormal = faceNormal;
        }

        if (t1 < tMax)
        {
            tMax = t1;
        }

        return tMin <= tMax;
    }
}
