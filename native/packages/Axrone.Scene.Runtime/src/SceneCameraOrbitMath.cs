namespace Axrone.Scene.Runtime;

/// <summary>Shared orbit math for scene camera controllers.</summary>
internal static class SceneCameraOrbitMath
{
    public const float MinimumDistance = 1e-4f;
    public const float ParallelDotThreshold = 0.9999f;
    public const float ElevationLimit = 3.1415927f * 0.49f;

    public static Vec3 NormalizeUp(in Vec3 up)
    {
        float lengthSquared = Vec3.Dot(up, up);
        return lengthSquared <= MinimumDistance * MinimumDistance
            ? Vec3.UnitY
            : Vec3.Normalize(up);
    }

    public static float ClampElevation(float value) =>
        Math.Clamp(value, -ElevationLimit, ElevationLimit);

    public static float ClampDistance(float value, float minDistance, float maxDistance) =>
        Math.Clamp(value, minDistance, maxDistance);

    public static Vec3 ComposePosition(
        in Vec3 target,
        float azimuth,
        float elevation,
        float distance)
    {
        float cosElevation = MathF.Cos(elevation);
        return new Vec3(
            target.X + (MathF.Sin(azimuth) * cosElevation * distance),
            target.Y + (MathF.Sin(elevation) * distance),
            target.Z + (MathF.Cos(azimuth) * cosElevation * distance));
    }

    /// <summary>
    /// Writes the camera position and aims it at <paramref name="target"/>,
    /// falling back to a safe up axis near the poles.
    /// </summary>
    public static void ApplyLook(
        ref SceneTransform3D transform,
        in Vec3 position,
        in Vec3 target,
        in Vec3 up)
    {
        transform.SetPosition(position);

        Vec3 forward = target - position;
        if (Vec3.Dot(forward, forward) <= MinimumDistance * MinimumDistance)
        {
            return;
        }

        forward = Vec3.Normalize(forward);
        Vec3 resolvedUp = up;
        if (MathF.Abs(Vec3.Dot(forward, up)) >= ParallelDotThreshold)
        {
            resolvedUp = MathF.Abs(forward.Y) < ParallelDotThreshold
                ? Vec3.UnitY
                : Vec3.UnitZ;
        }

        Quat rotation = Quat.LookRotation(-forward, resolvedUp);
        transform.SetRotation(rotation);
    }
}
