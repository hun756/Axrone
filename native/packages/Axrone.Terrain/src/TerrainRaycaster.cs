namespace Axrone.Terrain;

/// <summary>A ray in terrain-local space, origin-centered with +Y up.</summary>
/// <param name="Origin">Ray origin.</param>
/// <param name="Direction">Ray direction; normalized internally.</param>
public readonly record struct TerrainRay(Vector3 Origin, Vector3 Direction);

/// <summary>A terrain surface hit in terrain-local space.</summary>
/// <param name="Point">Hit position in terrain-local space.</param>
/// <param name="U">Normalized grid coordinate along X.</param>
/// <param name="V">Normalized grid coordinate along Z.</param>
/// <param name="Distance">Ray parameter at the hit.</param>
public readonly record struct TerrainRaycastHit(Vector3 Point, float U, float V, float Distance);

/// <summary>
/// Ray-marches a terrain heightmap in terrain-local space and returns the first
/// surface hit. A ray-AABB pre-cull skips marching outside the footprint, then a
/// grid-sized march is followed by bisection refinement.
/// </summary>
public static class TerrainRaycaster
{
    /// <summary>Ray-marches the heightmap and returns the first surface hit.</summary>
    /// <param name="heightmap">Heightmap to query.</param>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <param name="ray">Ray in terrain-local space.</param>
    /// <param name="maxDistance">Maximum march distance in world units.</param>
    /// <returns>The hit, or <see langword="null"/> when the ray misses.</returns>
    /// <exception cref="TerrainException">The descriptor is invalid.</exception>
    public static TerrainRaycastHit? Raycast(
        TerrainHeightmap heightmap,
        in TerrainDescriptor descriptor,
        in TerrainRay ray,
        float maxDistance = TerrainLimits.RaycastDefaultMaxDistance)
    {
        return TryRaycast(heightmap, descriptor, ray, maxDistance, out TerrainRaycastHit hit)
            ? hit
            : null;
    }

    /// <summary>Ray-marches the heightmap and reports the first surface hit.</summary>
    /// <param name="heightmap">Heightmap to query.</param>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <param name="ray">Ray in terrain-local space.</param>
    /// <param name="maxDistance">Maximum march distance in world units.</param>
    /// <param name="hit">Receives the hit when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the ray hits the surface.</returns>
    /// <exception cref="TerrainException">The descriptor is invalid.</exception>
    public static bool TryRaycast(
        TerrainHeightmap heightmap,
        in TerrainDescriptor descriptor,
        in TerrainRay ray,
        float maxDistance,
        out TerrainRaycastHit hit)
    {
        ArgumentNullException.ThrowIfNull(heightmap);
        descriptor.Validate();
        hit = default;

        float halfWidth = descriptor.Width * 0.5f;
        float halfLength = descriptor.Length * 0.5f;

        float directionLength = ray.Direction.Length();
        if (!float.IsFinite(directionLength) || directionLength <= 0f)
        {
            return false;
        }

        Vector3 direction = ray.Direction / directionLength;
        if (!TryFootprintSlab(ray.Origin, direction, halfWidth, halfLength, maxDistance, out float tMin, out float tMax))
        {
            return false;
        }

        float cellSize = MathF.Min(
            descriptor.Width / (descriptor.Resolution - 1),
            descriptor.Length / (descriptor.Resolution - 1));
        float step = MathF.Max(cellSize * 0.5f, (tMax - tMin) / 4096f);

        float previousT = tMin;
        bool previousAbove = IsAbove(heightmap, descriptor, halfWidth, halfLength, ray.Origin, direction, tMin);

        float t = tMin + step;
        var guard = 0;
        while (t <= tMax && guard++ < 1_000_000)
        {
            bool above = IsAbove(heightmap, descriptor, halfWidth, halfLength, ray.Origin, direction, t);
            if (previousAbove && !above)
            {
                float low = previousT;
                float high = t;
                for (var refine = 0; refine < TerrainLimits.RaycastRefineSteps; refine++)
                {
                    float middle = (low + high) * 0.5f;
                    if (IsAbove(heightmap, descriptor, halfWidth, halfLength, ray.Origin, direction, middle))
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                float hitT = (low + high) * 0.5f;
                float hitX = ray.Origin.X + (direction.X * hitT);
                float hitZ = ray.Origin.Z + (direction.Z * hitT);
                float height = HeightAtWorld(heightmap, descriptor, halfWidth, halfLength, hitX, hitZ);

                hit = new TerrainRaycastHit(
                    new Vector3(hitX, height, hitZ),
                    (hitX + halfWidth) / descriptor.Width,
                    (hitZ + halfLength) / descriptor.Length,
                    hitT);
                return true;
            }

            previousAbove = above;
            previousT = t;
            t += step;
        }

        return false;
    }

    private static bool TryFootprintSlab(
        Vector3 origin,
        Vector3 direction,
        float halfWidth,
        float halfLength,
        float maxDistance,
        out float tMin,
        out float tMax)
    {
        tMin = 0f;
        tMax = maxDistance;

        if (MathF.Abs(direction.X) < 1e-8f)
        {
            if (origin.X < -halfWidth || origin.X > halfWidth)
            {
                return false;
            }
        }
        else
        {
            float t1 = (-halfWidth - origin.X) / direction.X;
            float t2 = (halfWidth - origin.X) / direction.X;
            tMin = MathF.Max(tMin, MathF.Min(t1, t2));
            tMax = MathF.Min(tMax, MathF.Max(t1, t2));
            if (tMin > tMax)
            {
                return false;
            }
        }

        if (MathF.Abs(direction.Z) < 1e-8f)
        {
            if (origin.Z < -halfLength || origin.Z > halfLength)
            {
                return false;
            }
        }
        else
        {
            float t1 = (-halfLength - origin.Z) / direction.Z;
            float t2 = (halfLength - origin.Z) / direction.Z;
            tMin = MathF.Max(tMin, MathF.Min(t1, t2));
            tMax = MathF.Min(tMax, MathF.Max(t1, t2));
            if (tMin > tMax)
            {
                return false;
            }
        }

        tMin = MathF.Max(0f, tMin);
        return true;
    }

    private static float HeightAtWorld(
        TerrainHeightmap heightmap,
        in TerrainDescriptor descriptor,
        float halfWidth,
        float halfLength,
        float x,
        float z) =>
        heightmap.SampleHeight(
            (x + halfWidth) / descriptor.Width,
            (z + halfLength) / descriptor.Length) * descriptor.MaxHeight;

    private static bool IsAbove(
        TerrainHeightmap heightmap,
        in TerrainDescriptor descriptor,
        float halfWidth,
        float halfLength,
        Vector3 origin,
        Vector3 direction,
        float t)
    {
        float x = origin.X + (direction.X * t);
        float z = origin.Z + (direction.Z * t);
        float y = origin.Y + (direction.Y * t);
        return y > HeightAtWorld(heightmap, descriptor, halfWidth, halfLength, x, z);
    }
}
