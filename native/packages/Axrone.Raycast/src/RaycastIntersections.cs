namespace Axrone.Raycast;

using Axrone.Geometry;
using Axrone.Numeric;

/// <summary>
/// Allocation-free narrow phase for every shape a raycast engine can register. All
/// methods operate on the geometry package's value types and report a miss as a value,
/// so traversal loops never construct exceptions.
/// </summary>
public static class RaycastIntersections
{
    private const float Epsilon = 1e-6f;
    private const float DegenerateLengthSquared = 1e-12f;

    /// <summary>Tests a 2D ray against one shape.</summary>
    /// <param name="ray">Normalized query ray.</param>
    /// <param name="shape">Shape descriptor.</param>
    /// <param name="id">Identity stamped onto the hit.</param>
    /// <param name="maxDistance">Positive distance limit.</param>
    /// <param name="hit">Contact on a hit, otherwise default.</param>
    /// <returns><see langword="true"/> when the ray reaches the shape.</returns>
    public static bool TryIntersect2D(
        in Ray2D ray,
        in RaycastShape2D shape,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit2D hit) =>
        shape.Kind switch
        {
            RaycastShapeKind.Aabb => TryAabb2D(in ray, in shape, id, maxDistance, out hit),
            RaycastShapeKind.Sphere => TryCircle2D(in ray, shape.Center, shape.Radius, id, maxDistance, shape.LayerMask, out hit),
            RaycastShapeKind.Box => TryBox2D(in ray, in shape, id, maxDistance, out hit),
            RaycastShapeKind.Capsule => TryCapsule2D(in ray, in shape, id, maxDistance, out hit),
            RaycastShapeKind.Segment => TrySegment2D(in ray, shape.A, shape.B, id, maxDistance, shape.LayerMask, out hit),
            RaycastShapeKind.Polygon => TryPolygon2D(in ray, in shape, id, maxDistance, out hit),
            _ => Miss2D(out hit),
        };

    /// <summary>Tests a 3D ray against one shape.</summary>
    /// <param name="ray">Normalized query ray.</param>
    /// <param name="shape">Shape descriptor.</param>
    /// <param name="id">Identity stamped onto the hit.</param>
    /// <param name="maxDistance">Positive distance limit.</param>
    /// <param name="hit">Contact on a hit, otherwise default.</param>
    /// <returns><see langword="true"/> when the ray reaches the shape.</returns>
    public static bool TryIntersect3D(
        in Ray3D ray,
        in RaycastShape3D shape,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit3D hit) =>
        shape.Kind switch
        {
            RaycastShapeKind.Aabb => TryAabb3D(in ray, in shape, id, maxDistance, out hit),
            RaycastShapeKind.Sphere => TrySphere3D(in ray, shape.Center, shape.Radius, id, maxDistance, shape.LayerMask, out hit),
            RaycastShapeKind.Plane => TryPlane3D(in ray, in shape, id, maxDistance, out hit),
            RaycastShapeKind.Triangle => TryTriangle3D(in ray, in shape, id, maxDistance, out hit),
            RaycastShapeKind.Box => TryBox3D(in ray, in shape, default, id, maxDistance, out hit),
            RaycastShapeKind.Capsule => TryCapsule3D(in ray, in shape, 0f, id, maxDistance, out hit),
            _ => Miss3D(out hit),
        };

    /// <summary>
    /// Sweeps a cast volume along a ray and reports the first time of impact against a
    /// target. The cast volume is a sphere, axis-aligned box or oriented box; the target
    /// may be any solid shape. Sphere wraps are exact, box wraps against non-axis-aligned
    /// targets are conservative.
    /// </summary>
    /// <param name="ray">Normalized sweep ray.</param>
    /// <param name="target">Shape being swept against.</param>
    /// <param name="castVolume">Sphere, axis-aligned box or oriented box being swept.</param>
    /// <param name="id">Identity stamped onto the hit.</param>
    /// <param name="maxDistance">Positive sweep limit.</param>
    /// <param name="hit">Contact on a hit, otherwise default.</param>
    /// <returns><see langword="true"/> when the sweep reaches the target.</returns>
    public static bool TryShapeCast3D(
        in Ray3D ray,
        in RaycastShape3D target,
        in RaycastShape3D castVolume,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit3D hit)
    {
        if (castVolume.Kind is not (RaycastShapeKind.Sphere or RaycastShapeKind.Aabb or RaycastShapeKind.Box))
        {
            return Miss3D(out hit);
        }

        Vec3 castExtents = castVolume.WorldHalfExtents;
        float castRadius = MaxComponent(castExtents);
        hit = default;

        var found = target.Kind switch
        {
            RaycastShapeKind.Aabb => TryAabb3D(
                in ray,
                RaycastShape3D.CreateAabb(target.Bounds.Expanded(castExtents), target.LayerMask, target.Trigger),
                id,
                maxDistance,
                out hit),
            RaycastShapeKind.Sphere => TrySphere3D(
                in ray,
                target.Center,
                target.Radius + castRadius,
                id,
                maxDistance,
                target.LayerMask,
                out hit),
            RaycastShapeKind.Plane => TryPlaneCast3D(in ray, in target, in castVolume, id, maxDistance, out hit),
            RaycastShapeKind.Box => TryBox3D(in ray, in target, castExtents, id, maxDistance, out hit),
            RaycastShapeKind.Capsule => TryCapsule3D(in ray, in target, castRadius, id, maxDistance, out hit),
            _ => Miss3D(out hit),
        };

        if (!found)
        {
            return false;
        }

        float pointOffset = target.Kind is RaycastShapeKind.Sphere or RaycastShapeKind.Capsule
            ? castRadius
            : SupportExtent(in castVolume, hit.Normal);
        hit = hit with { Point = hit.Point - (hit.Normal * pointOffset) };
        return true;
    }

    private static bool TryAabb2D(
        in Ray2D ray,
        in RaycastShape2D shape,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit2D hit)
    {
        Aabb2D bounds = shape.Bounds;
        if (!TrySlab2D(in ray, in bounds, maxDistance, out float distance))
        {
            return Miss2D(out hit);
        }

        Vec2 point = ray.At(distance);
        Vec2 normal = AabbNormal2D(point, in bounds);
        return Hit2D(id, point, normal, distance, maxDistance, shape.LayerMask, out hit);
    }

    private static bool TryCircle2D(
        in Ray2D ray,
        Vec2 center,
        float radius,
        RaycastShapeId id,
        float maxDistance,
        uint layerMask,
        out RaycastHit2D hit)
    {
        Vec2 offset = ray.Origin - center;
        float b = Vec2.Dot(offset, ray.Direction);
        float c = Vec2.Dot(offset, offset) - (radius * radius);
        float discriminant = (b * b) - c;
        if (discriminant < 0f)
        {
            return Miss2D(out hit);
        }

        float root = MathF.Sqrt(discriminant);
        float distance = -b - root;
        if (distance < 0f)
        {
            distance = -b + root;
        }

        if (distance < 0f || distance > maxDistance)
        {
            return Miss2D(out hit);
        }

        Vec2 point = ray.At(distance);
        Vec2 normal = NormalizeOr(point - center, -ray.Direction);
        return Hit2D(id, point, normal, distance, maxDistance, layerMask, out hit);
    }

    private static bool TryBox2D(
        in Ray2D ray,
        in RaycastShape2D shape,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit2D hit)
    {
        float cos = MathF.Cos(shape.Rotation);
        float sin = MathF.Sin(shape.Rotation);
        Vec2 delta = ray.Origin - shape.Center;
        Vec2 localOrigin = new((delta.X * cos) + (delta.Y * sin), (-delta.X * sin) + (delta.Y * cos));
        Vec2 localDirection = new(
            (ray.Direction.X * cos) + (ray.Direction.Y * sin),
            (-ray.Direction.X * sin) + (ray.Direction.Y * cos));
        var localRay = new Ray2D(localOrigin, localDirection);
        var localBounds = new Aabb2D(-shape.HalfExtents, shape.HalfExtents);
        if (!TrySlab2D(in localRay, in localBounds, maxDistance, out float distance))
        {
            return Miss2D(out hit);
        }

        Vec2 localPoint = localRay.At(distance);
        Vec2 localNormal = AabbNormal2D(localPoint, in localBounds);
        Vec2 point = shape.Center + new Vec2(
            (localPoint.X * cos) - (localPoint.Y * sin),
            (localPoint.X * sin) + (localPoint.Y * cos));
        Vec2 normal = new(
            (localNormal.X * cos) - (localNormal.Y * sin),
            (localNormal.X * sin) + (localNormal.Y * cos));
        return Hit2D(id, point, normal, distance, maxDistance, shape.LayerMask, out hit);
    }

    private static bool TryCapsule2D(
        in Ray2D ray,
        in RaycastShape2D shape,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit2D hit)
    {
        Vec2 edge = shape.B - shape.A;
        float edgeLengthSquared = Vec2.Dot(edge, edge);
        if (edgeLengthSquared < DegenerateLengthSquared)
        {
            return TryCircle2D(in ray, shape.A, shape.Radius, id, maxDistance, shape.LayerMask, out hit);
        }

        Vec2 axis = edge / MathF.Sqrt(edgeLengthSquared);
        Vec2 perpendicular = new(-axis.Y, axis.X);

        var found = false;
        float bestDistance = float.PositiveInfinity;
        Vec2 bestNormal = default;

        if (TryCapsuleSide(in ray, shape.A + (perpendicular * shape.Radius), edge, perpendicular, maxDistance, out float sideDistance, out Vec2 sideNormal) &&
            sideDistance < bestDistance)
        {
            found = true;
            bestDistance = sideDistance;
            bestNormal = sideNormal;
        }

        if (TryCapsuleSide(in ray, shape.A - (perpendicular * shape.Radius), edge, -perpendicular, maxDistance, out sideDistance, out sideNormal) &&
            sideDistance < bestDistance)
        {
            found = true;
            bestDistance = sideDistance;
            bestNormal = sideNormal;
        }

        if (TryCircle2D(in ray, shape.A, shape.Radius, id, maxDistance, shape.LayerMask, out RaycastHit2D capA) &&
            capA.Distance < bestDistance)
        {
            found = true;
            bestDistance = capA.Distance;
            bestNormal = capA.Normal;
        }

        if (TryCircle2D(in ray, shape.B, shape.Radius, id, maxDistance, shape.LayerMask, out RaycastHit2D capB) &&
            capB.Distance < bestDistance)
        {
            found = true;
            bestDistance = capB.Distance;
            bestNormal = capB.Normal;
        }

        if (!found)
        {
            return Miss2D(out hit);
        }

        return Hit2D(id, ray.At(bestDistance), bestNormal, bestDistance, maxDistance, shape.LayerMask, out hit);
    }

    private static bool TryCapsuleSide(
        in Ray2D ray,
        Vec2 linePoint,
        Vec2 lineDirection,
        Vec2 fallbackNormal,
        float maxDistance,
        out float distance,
        out Vec2 normal)
    {
        float denominator = Cross(ray.Direction, lineDirection);
        if (MathF.Abs(denominator) < Epsilon)
        {
            distance = 0f;
            normal = default;
            return false;
        }

        Vec2 delta = linePoint - ray.Origin;
        float t = Cross(delta, lineDirection) / denominator;
        float s = Cross(delta, ray.Direction) / denominator;
        if (t < 0f || t > maxDistance || s < 0f || s > 1f)
        {
            distance = 0f;
            normal = default;
            return false;
        }

        Vec2 point = ray.At(t);
        Vec2 axisPoint = linePoint + (lineDirection * s);
        distance = t;
        normal = NormalizeOr(point - axisPoint, fallbackNormal);
        return true;
    }

    private static bool TrySegment2D(
        in Ray2D ray,
        Vec2 a,
        Vec2 b,
        RaycastShapeId id,
        float maxDistance,
        uint layerMask,
        out RaycastHit2D hit)
    {
        if (!TrySegmentEdge(in ray, a, b, maxDistance, out float distance, out Vec2 normal))
        {
            return Miss2D(out hit);
        }

        return Hit2D(id, ray.At(distance), normal, distance, maxDistance, layerMask, out hit);
    }

    private static bool TrySegmentEdge(
        in Ray2D ray,
        Vec2 a,
        Vec2 b,
        float maxDistance,
        out float distance,
        out Vec2 normal)
    {
        Vec2 edge = b - a;
        float denominator = Cross(ray.Direction, edge);
        if (MathF.Abs(denominator) < Epsilon)
        {
            distance = 0f;
            normal = default;
            return false;
        }

        Vec2 delta = a - ray.Origin;
        float t = Cross(delta, edge) / denominator;
        float u = Cross(delta, ray.Direction) / denominator;
        if (t < 0f || t > maxDistance || u < 0f || u > 1f)
        {
            distance = 0f;
            normal = default;
            return false;
        }

        Vec2 candidate = NormalizeOr(new Vec2(-edge.Y, edge.X), -ray.Direction);
        distance = t;
        normal = Vec2.Dot(candidate, ray.Direction) > 0f ? -candidate : candidate;
        return true;
    }

    private static bool TryPolygon2D(
        in Ray2D ray,
        in RaycastShape2D shape,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit2D hit)
    {
        ReadOnlySpan<Vec2> vertices = shape.Polygon.Span;
        if (vertices.Length < 3)
        {
            return Miss2D(out hit);
        }

        var found = false;
        float bestDistance = float.PositiveInfinity;
        Vec2 bestNormal = default;
        for (var index = 0; index < vertices.Length; index++)
        {
            Vec2 a = vertices[index];
            Vec2 b = vertices[(index + 1) % vertices.Length];
            if (TrySegmentEdge(in ray, a, b, maxDistance, out float distance, out Vec2 normal) &&
                distance < bestDistance)
            {
                found = true;
                bestDistance = distance;
                bestNormal = normal;
            }
        }

        if (!found)
        {
            return Miss2D(out hit);
        }

        return Hit2D(id, ray.At(bestDistance), bestNormal, bestDistance, maxDistance, shape.LayerMask, out hit);
    }

    private static bool TryAabb3D(
        in Ray3D ray,
        in RaycastShape3D shape,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit3D hit)
    {
        Aabb3D bounds = shape.Bounds;
        return TryAabb3D(in ray, in bounds, shape.LayerMask, id, maxDistance, out hit);
    }

    private static bool TryAabb3D(
        in Ray3D ray,
        in Aabb3D bounds,
        uint layerMask,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit3D hit)
    {
        if (!TrySlab3D(in ray, in bounds, maxDistance, out float distance))
        {
            return Miss3D(out hit);
        }

        Vec3 point = ray.At(distance);
        Vec3 normal = AabbNormal3D(point, in bounds);
        return Hit3D(id, point, normal, distance, maxDistance, -1, default, layerMask, out hit);
    }

    private static bool TrySphere3D(
        in Ray3D ray,
        Vec3 center,
        float radius,
        RaycastShapeId id,
        float maxDistance,
        uint layerMask,
        out RaycastHit3D hit)
    {
        if (!TrySphereRoot(in ray, center, radius, maxDistance, out float distance, out Vec3 normal))
        {
            return Miss3D(out hit);
        }

        return Hit3D(id, ray.At(distance), normal, distance, maxDistance, -1, default, layerMask, out hit);
    }

    private static bool TrySphereRoot(
        in Ray3D ray,
        Vec3 center,
        float radius,
        float maxDistance,
        out float distance,
        out Vec3 normal)
    {
        Vec3 offset = ray.Origin - center;
        float b = Vec3.Dot(offset, ray.Direction);
        float c = Vec3.Dot(offset, offset) - (radius * radius);
        float discriminant = (b * b) - c;
        if (discriminant < 0f)
        {
            distance = 0f;
            normal = default;
            return false;
        }

        float root = MathF.Sqrt(discriminant);
        float t = -b - root;
        if (t < 0f)
        {
            t = -b + root;
        }

        if (t < 0f || t > maxDistance)
        {
            distance = 0f;
            normal = default;
            return false;
        }

        distance = t;
        normal = NormalizeOr(ray.At(t) - center, -ray.Direction);
        return true;
    }

    private static bool TryPlane3D(
        in Ray3D ray,
        in RaycastShape3D shape,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit3D hit)
    {
        Vec3 normal = shape.Normal;
        float denominator = Vec3.Dot(normal, ray.Direction);
        if (MathF.Abs(denominator) < Epsilon)
        {
            return Miss3D(out hit);
        }

        float distance = Vec3.Dot(shape.Center - ray.Origin, normal) / denominator;
        if (distance < 0f || distance > maxDistance)
        {
            return Miss3D(out hit);
        }

        return Hit3D(id, ray.At(distance), normal, distance, maxDistance, -1, default, shape.LayerMask, out hit);
    }

    private static bool TryPlaneCast3D(
        in Ray3D ray,
        in RaycastShape3D target,
        in RaycastShape3D castVolume,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit3D hit)
    {
        Vec3 normal = target.Normal;
        float denominator = Vec3.Dot(normal, ray.Direction);
        if (MathF.Abs(denominator) < Epsilon)
        {
            return Miss3D(out hit);
        }

        float support = SupportExtent(in castVolume, normal);
        float distance = (support - Vec3.Dot(ray.Origin - target.Center, normal)) / denominator;
        if (distance < 0f || distance > maxDistance)
        {
            return Miss3D(out hit);
        }

        Vec3 point = ray.At(distance);
        return Hit3D(id, point, normal, distance, maxDistance, -1, default, target.LayerMask, out hit);
    }

    private static bool TryTriangle3D(
        in Ray3D ray,
        in RaycastShape3D shape,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit3D hit)
    {
        Vec3 edge1 = shape.B - shape.A;
        Vec3 edge2 = shape.C - shape.A;
        Vec3 rawNormal = Vec3.Cross(edge1, edge2);
        float lengthSquared = rawNormal.LengthSquared();
        if (lengthSquared < DegenerateLengthSquared)
        {
            return Miss3D(out hit);
        }

        if (!ray.Intersects(new Triangle3(shape.A, shape.B, shape.C), out TriangleHit3 triangle) ||
            triangle.Distance > maxDistance)
        {
            return Miss3D(out hit);
        }

        Vec3 normal = rawNormal / MathF.Sqrt(lengthSquared);
        return Hit3D(
            id,
            triangle.Point,
            normal,
            triangle.Distance,
            maxDistance,
            0,
            new Vec2(triangle.U, triangle.V),
            shape.LayerMask,
            out hit);
    }

    private static bool TryBox3D(
        in Ray3D ray,
        in RaycastShape3D shape,
        Vec3 inflation,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit3D hit)
    {
        Vec3 halfExtents = shape.HalfExtents + inflation;
        Quat inverse = Quat.Conjugate(shape.Rotation);
        Vec3 delta = ray.Origin - shape.Center;
        Vec3 localOrigin = RaycastShape3D.Rotate(inverse, delta);
        Vec3 localDirection = RaycastShape3D.Rotate(inverse, ray.Direction);
        var localRay = new Ray3D(localOrigin, localDirection);
        var localBounds = new Aabb3D(-halfExtents, halfExtents);
        if (!TrySlab3D(in localRay, in localBounds, maxDistance, out float distance))
        {
            return Miss3D(out hit);
        }

        Vec3 localPoint = localRay.At(distance);
        Vec3 localNormal = AabbNormal3D(localPoint, in localBounds);
        Vec3 point = shape.Center + RaycastShape3D.Rotate(shape.Rotation, localPoint);
        Vec3 normal = RaycastShape3D.Rotate(shape.Rotation, localNormal);
        return Hit3D(id, point, normal, distance, maxDistance, -1, default, shape.LayerMask, out hit);
    }

    private static bool TryCapsule3D(
        in Ray3D ray,
        in RaycastShape3D shape,
        float inflation,
        RaycastShapeId id,
        float maxDistance,
        out RaycastHit3D hit)
    {
        Vec3 axis = shape.B - shape.A;
        float length = axis.Length();
        if (length < Epsilon)
        {
            return TrySphere3D(in ray, shape.A, shape.Radius + inflation, id, maxDistance, shape.LayerMask, out hit);
        }

        Vec3 u = axis / length;
        Vec3 helper = MathF.Abs(u.Y) < 0.99f ? Vec3.UnitY : Vec3.UnitX;
        Vec3 v = Vec3.Normalize(Vec3.Cross(helper, u));
        Vec3 w = Vec3.Cross(u, v);
        Vec3 center = (shape.A + shape.B) * 0.5f;
        Vec3 delta = ray.Origin - center;
        var localRay = new Ray3D(
            new Vec3(Vec3.Dot(delta, v), Vec3.Dot(delta, u), Vec3.Dot(delta, w)),
            new Vec3(Vec3.Dot(ray.Direction, v), Vec3.Dot(ray.Direction, u), Vec3.Dot(ray.Direction, w)));

        if (!TryVerticalCapsule(in localRay, length * 0.5f, shape.Radius + inflation, maxDistance, out float distance, out Vec3 localNormal))
        {
            return Miss3D(out hit);
        }

        Vec3 localPoint = localRay.At(distance);
        Vec3 point = center + (v * localPoint.X) + (u * localPoint.Y) + (w * localPoint.Z);
        Vec3 normal = Vec3.Normalize((v * localNormal.X) + (u * localNormal.Y) + (w * localNormal.Z));
        return Hit3D(id, point, normal, distance, maxDistance, -1, default, shape.LayerMask, out hit);
    }

    private static bool TryVerticalCapsule(
        in Ray3D ray,
        float halfLength,
        float radius,
        float maxDistance,
        out float distance,
        out Vec3 normal)
    {
        var found = false;
        float bestDistance = float.PositiveInfinity;
        Vec3 bestNormal = default;

        Vec3 origin = ray.Origin;
        float a = (ray.Direction.X * ray.Direction.X) + (ray.Direction.Z * ray.Direction.Z);
        float b = 2f * ((origin.X * ray.Direction.X) + (origin.Z * ray.Direction.Z));
        float c = (origin.X * origin.X) + (origin.Z * origin.Z) - (radius * radius);
        float discriminant = (b * b) - (4f * a * c);
        if (a > Epsilon && discriminant >= 0f)
        {
            float root = MathF.Sqrt(discriminant);
            float inverse = 1f / (2f * a);
            float t0 = (-b - root) * inverse;
            float t1 = (-b + root) * inverse;
            for (var index = 0; index < 2; index++)
            {
                float t = index == 0 ? t0 : t1;
                if (t < 0f || t > maxDistance || t >= bestDistance)
                {
                    continue;
                }

                Vec3 point = ray.At(t);
                if (point.Y < -halfLength || point.Y > halfLength)
                {
                    continue;
                }

                found = true;
                bestDistance = t;
                bestNormal = NormalizeOr(new Vec3(point.X, 0f, point.Z), -ray.Direction);
            }
        }

        if (TrySphereRoot(in ray, new Vec3(0f, halfLength, 0f), radius, maxDistance, out float capDistance, out Vec3 capNormal) &&
            capDistance < bestDistance)
        {
            found = true;
            bestDistance = capDistance;
            bestNormal = capNormal;
        }

        if (TrySphereRoot(in ray, new Vec3(0f, -halfLength, 0f), radius, maxDistance, out capDistance, out capNormal) &&
            capDistance < bestDistance)
        {
            found = true;
            bestDistance = capDistance;
            bestNormal = capNormal;
        }

        distance = bestDistance;
        normal = bestNormal;
        return found;
    }

    private static bool TrySlab2D(in Ray2D ray, in Aabb2D bounds, float maxDistance, out float distance)
    {
        float t1x = (bounds.Min.X - ray.Origin.X) * ray.InvDirection.X;
        float t2x = (bounds.Max.X - ray.Origin.X) * ray.InvDirection.X;
        float t1y = (bounds.Min.Y - ray.Origin.Y) * ray.InvDirection.Y;
        float t2y = (bounds.Max.Y - ray.Origin.Y) * ray.InvDirection.Y;

        float tNear = MathF.Max(MathF.Min(t1x, t2x), MathF.Min(t1y, t2y));
        float tFar = MathF.Min(MathF.Max(t1x, t2x), MathF.Max(t1y, t2y));
        if (tFar < MathF.Max(tNear, 0f) || tNear > maxDistance)
        {
            distance = 0f;
            return false;
        }

        distance = tNear >= 0f ? tNear : tFar;
        return distance >= 0f && distance <= maxDistance;
    }

    private static bool TrySlab3D(in Ray3D ray, in Aabb3D bounds, float maxDistance, out float distance)
    {
        float t1x = (bounds.Min.X - ray.Origin.X) * ray.InvDirection.X;
        float t2x = (bounds.Max.X - ray.Origin.X) * ray.InvDirection.X;
        float t1y = (bounds.Min.Y - ray.Origin.Y) * ray.InvDirection.Y;
        float t2y = (bounds.Max.Y - ray.Origin.Y) * ray.InvDirection.Y;
        float t1z = (bounds.Min.Z - ray.Origin.Z) * ray.InvDirection.Z;
        float t2z = (bounds.Max.Z - ray.Origin.Z) * ray.InvDirection.Z;

        float tNear = MathF.Max(MathF.Min(t1x, t2x), MathF.Max(MathF.Min(t1y, t2y), MathF.Min(t1z, t2z)));
        float tFar = MathF.Min(MathF.Max(t1x, t2x), MathF.Min(MathF.Max(t1y, t2y), MathF.Max(t1z, t2z)));
        if (tFar < MathF.Max(tNear, 0f) || tNear > maxDistance)
        {
            distance = 0f;
            return false;
        }

        distance = tNear >= 0f ? tNear : tFar;
        return distance >= 0f && distance <= maxDistance;
    }

    private static float SupportExtent(in RaycastShape3D castVolume, Vec3 normal)
    {
        switch (castVolume.Kind)
        {
            case RaycastShapeKind.Sphere:
                return castVolume.Radius;
            case RaycastShapeKind.Aabb:
                Vec3 extents = castVolume.Bounds.Extents;
                return (MathF.Abs(normal.X) * extents.X) +
                       (MathF.Abs(normal.Y) * extents.Y) +
                       (MathF.Abs(normal.Z) * extents.Z);
            case RaycastShapeKind.Box:
                Vec3 x = RaycastShape3D.Rotate(castVolume.Rotation, new Vec3(castVolume.HalfExtents.X, 0f, 0f));
                Vec3 y = RaycastShape3D.Rotate(castVolume.Rotation, new Vec3(0f, castVolume.HalfExtents.Y, 0f));
                Vec3 z = RaycastShape3D.Rotate(castVolume.Rotation, new Vec3(0f, 0f, castVolume.HalfExtents.Z));
                return MathF.Abs(Vec3.Dot(normal, x)) +
                       MathF.Abs(Vec3.Dot(normal, y)) +
                       MathF.Abs(Vec3.Dot(normal, z));
            default:
                return 0f;
        }
    }

    private static float MaxComponent(Vec3 value) =>
        MathF.Max(value.X, MathF.Max(value.Y, value.Z));

    private static Vec2 AabbNormal2D(Vec2 point, in Aabb2D bounds)
    {
        float left = MathF.Abs(point.X - bounds.Min.X);
        float right = MathF.Abs(bounds.Max.X - point.X);
        float bottom = MathF.Abs(point.Y - bounds.Min.Y);
        float top = MathF.Abs(bounds.Max.Y - point.Y);
        float best = MathF.Min(MathF.Min(left, right), MathF.Min(bottom, top));
        if (best == left)
        {
            return new Vec2(-1f, 0f);
        }

        if (best == right)
        {
            return new Vec2(1f, 0f);
        }

        return best == bottom ? new Vec2(0f, -1f) : new Vec2(0f, 1f);
    }

    private static Vec3 AabbNormal3D(Vec3 point, in Aabb3D bounds)
    {
        float left = MathF.Abs(point.X - bounds.Min.X);
        float right = MathF.Abs(bounds.Max.X - point.X);
        float bottom = MathF.Abs(point.Y - bounds.Min.Y);
        float top = MathF.Abs(bounds.Max.Y - point.Y);
        float back = MathF.Abs(point.Z - bounds.Min.Z);
        float front = MathF.Abs(bounds.Max.Z - point.Z);

        float best = MathF.Min(MathF.Min(MathF.Min(left, right), MathF.Min(bottom, top)), MathF.Min(back, front));
        if (best == left)
        {
            return new Vec3(-1f, 0f, 0f);
        }

        if (best == right)
        {
            return new Vec3(1f, 0f, 0f);
        }

        if (best == bottom)
        {
            return new Vec3(0f, -1f, 0f);
        }

        if (best == top)
        {
            return new Vec3(0f, 1f, 0f);
        }

        return best == back ? new Vec3(0f, 0f, -1f) : new Vec3(0f, 0f, 1f);
    }

    private static Vec2 NormalizeOr(Vec2 value, Vec2 fallback)
    {
        float lengthSquared = Vec2.Dot(value, value);
        return lengthSquared < DegenerateLengthSquared ? fallback : value / MathF.Sqrt(lengthSquared);
    }

    private static Vec3 NormalizeOr(Vec3 value, Vec3 fallback)
    {
        float lengthSquared = Vec3.Dot(value, value);
        return lengthSquared < DegenerateLengthSquared ? fallback : value / MathF.Sqrt(lengthSquared);
    }

    private static float Cross(Vec2 left, Vec2 right) => (left.X * right.Y) - (left.Y * right.X);

    private static bool Hit2D(
        RaycastShapeId id,
        Vec2 point,
        Vec2 normal,
        float distance,
        float maxDistance,
        uint layerMask,
        out RaycastHit2D hit)
    {
        hit = new RaycastHit2D(id, point, normal, distance, distance / maxDistance, layerMask);
        return true;
    }

    private static bool Hit3D(
        RaycastShapeId id,
        Vec3 point,
        Vec3 normal,
        float distance,
        float maxDistance,
        int triangleIndex,
        Vec2 barycentric,
        uint layerMask,
        out RaycastHit3D hit)
    {
        hit = new RaycastHit3D(id, point, normal, distance, distance / maxDistance, triangleIndex, barycentric, layerMask);
        return true;
    }

    private static bool Miss2D(out RaycastHit2D hit)
    {
        hit = default;
        return false;
    }

    private static bool Miss3D(out RaycastHit3D hit)
    {
        hit = default;
        return false;
    }
}
