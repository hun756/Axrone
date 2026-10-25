namespace Axrone.Lighting;

/// <summary>Frame scoring policy (monomorphized per resolver).</summary>
public interface ILightScoringPolicy
{
    /// <summary>Policy identity for memoization.</summary>
    static abstract ushort PolicyId { get; }

    /// <summary>Whether the policy reads the camera.</summary>
    static abstract bool RequiresCamera { get; }

    /// <summary>Scores a directional light.</summary>
    static abstract float ScoreDirectional(ref readonly LightData light);

    /// <summary>Scores a point light.</summary>
    static abstract float ScorePoint(ref readonly LightData light, in Vec3 cameraPosition, bool hasCamera);

    /// <summary>Scores a spot light.</summary>
    static abstract float ScoreSpot(ref readonly LightData light, in Vec3 cameraPosition, bool hasCamera);
}

/// <summary>Influence scoring: priority plus distance falloff.</summary>
public readonly struct InfluenceScoringPolicy : ILightScoringPolicy
{
    /// <inheritdoc/>
    public static ushort PolicyId => 1;

    /// <inheritdoc/>
    public static bool RequiresCamera => true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScoreDirectional(ref readonly LightData light)
    {
        return light.Priority.Value * 1024.0f + light.Intensity.Value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScorePoint(ref readonly LightData light, in Vec3 cameraPosition, bool hasCamera)
    {
        if (!hasCamera)
        {
            return light.Intensity.Value;
        }

        float r = light.Range.Value;
        float rangeSq = r * r;
        if (rangeSq <= 0.0f)
        {
            return 0.0f;
        }

        float distSq = Vec3.DistanceSquared(cameraPosition, light.Position);
        return light.Intensity.Value / (1.0f + (light.Attenuation.Value * (distSq / rangeSq)));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScoreSpot(ref readonly LightData light, in Vec3 cameraPosition, bool hasCamera)
    {
        if (!hasCamera)
        {
            return light.Intensity.Value;
        }

        float r = light.Range.Value;
        float rangeSq = r * r;
        if (rangeSq <= 0.0f)
        {
            return 0.0f;
        }

        Vec3 toCam = cameraPosition - light.Position;
        float distSq = toCam.LengthSquared();
        if (distSq <= 1e-16f)
        {
            return light.Intensity.Value;
        }

        float dist = MathF.Sqrt(distSq);
        Vec3 normToCam = toCam / dist;
        float directionDot = Vec3.Dot(normToCam, light.Direction);

        if (directionDot <= light.OuterConeCosine.Value)
        {
            return 0.0f;
        }

        float denom = light.InnerConeCosine.Value - light.OuterConeCosine.Value;
        float coneFactor = denom <= 1e-6f ? 1.0f : Math.Clamp((directionDot - light.OuterConeCosine.Value) / denom, 0.0f, 1.0f);

        return (light.Intensity.Value * coneFactor) / (1.0f + (light.Attenuation.Value * (distSq / rangeSq)));
    }
}

/// <summary>Priority scoring: priority only.</summary>
public readonly struct PriorityScoringPolicy : ILightScoringPolicy
{
    /// <inheritdoc/>
    public static ushort PolicyId => 2;

    /// <inheritdoc/>
    public static bool RequiresCamera => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScoreDirectional(ref readonly LightData light) => (light.Priority.Value * 1024.0f) + light.Intensity.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScorePoint(ref readonly LightData light, in Vec3 cameraPosition, bool hasCamera) => (light.Priority.Value * 1024.0f) + light.Intensity.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScoreSpot(ref readonly LightData light, in Vec3 cameraPosition, bool hasCamera) => (light.Priority.Value * 1024.0f) + light.Intensity.Value;
}

/// <summary>FIFO scoring: insertion order preserved.</summary>
public readonly struct FifoScoringPolicy : ILightScoringPolicy
{
    /// <inheritdoc/>
    public static ushort PolicyId => 3;

    /// <inheritdoc/>
    public static bool RequiresCamera => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScoreDirectional(ref readonly LightData light) => 0.0f;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScorePoint(ref readonly LightData light, in Vec3 cameraPosition, bool hasCamera) => 0.0f;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScoreSpot(ref readonly LightData light, in Vec3 cameraPosition, bool hasCamera) => 0.0f;
}
