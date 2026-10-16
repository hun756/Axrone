namespace Axrone.Audio;

/// <summary>
/// Distance attenuation model.
/// </summary>
public enum AudioDistanceModel : byte
{
    /// <summary>No attenuation.</summary>
    None = 0,

    /// <summary>Linear falloff.</summary>
    Linear = 1,

    /// <summary>Inverse falloff.</summary>
    Inverse = 2,

    /// <summary>Exponential falloff.</summary>
    Exponential = 3,
}

/// <summary>
/// Spatialization mode.
/// </summary>
public enum AudioSpatialMode : byte
{
    /// <summary>Flat stereo, no distance.</summary>
    TwoDimensional = 0,

    /// <summary>Positional with distance and cone.</summary>
    ThreeDimensional = 1,
}

/// <summary>
/// Stereo panning model.
/// </summary>
public enum AudioPanningModel : byte
{
    /// <summary>Equal power.</summary>
    EqualPower = 0,

    /// <summary>Linear.</summary>
    Linear = 1,
}

/// <summary>
/// Distance attenuation tuning.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioSpatialAttenuation(
    AudioDistanceModel Model = AudioDistanceModel.Inverse,
    float RefDistance = 1.0f,
    float MaxDistance = 10000.0f,
    float RolloffFactor = 1.0f,
    float MinGain = 0.0f);

/// <summary>
/// Full spatialization descriptor for one voice.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioSpatialization(
    AudioSpatialMode Mode,
    Vector3 Position,
    Vector3 Orientation,
    AudioSpatialAttenuation Attenuation,
    AudioPanningModel PanningModel = AudioPanningModel.EqualPower,
    float ConeInnerAngle = 360.0f,
    float ConeOuterAngle = 360.0f,
    float ConeOuterGain = 0.0f)
{
    /// <summary>Flat stereo default.</summary>
    public static AudioSpatialization Default2D => new(
        AudioSpatialMode.TwoDimensional,
        Vector3.Zero,
        -Vector3.UnitZ,
        new AudioSpatialAttenuation(AudioDistanceModel.None));

    /// <summary>Positional default.</summary>
    public static AudioSpatialization Default3D => new(
        AudioSpatialMode.ThreeDimensional,
        Vector3.Zero,
        -Vector3.UnitZ,
        new AudioSpatialAttenuation(AudioDistanceModel.Inverse));
}

/// <summary>
/// Pure spatial math over <see cref="Vector3"/>.
/// </summary>
public static class SpatialKinematics
{
    /// <summary>Normalizes, mapping near-zero vectors to zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector3 SafeNormalize(in Vector3 value)
    {
        float squared = value.LengthSquared();
        if (squared <= 1e-12f)
        {
            return Vector3.Zero;
        }

        return value / MathF.Sqrt(squared);
    }

    /// <summary>Computes distance gain through the configured model.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float CalculateDistanceGain(in AudioSpatialAttenuation attenuation, float distance)
    {
        return attenuation.Model switch
        {
            AudioDistanceModel.None => NoneAttenuationPolicy.ComputeGain(distance, attenuation.RefDistance, attenuation.MaxDistance, attenuation.RolloffFactor, attenuation.MinGain),
            AudioDistanceModel.Linear => LinearAttenuationPolicy.ComputeGain(distance, attenuation.RefDistance, attenuation.MaxDistance, attenuation.RolloffFactor, attenuation.MinGain),
            AudioDistanceModel.Inverse => InverseAttenuationPolicy.ComputeGain(distance, attenuation.RefDistance, attenuation.MaxDistance, attenuation.RolloffFactor, attenuation.MinGain),
            AudioDistanceModel.Exponential => ExponentialAttenuationPolicy.ComputeGain(distance, attenuation.RefDistance, attenuation.MaxDistance, attenuation.RolloffFactor, attenuation.MinGain),
            _ => 1.0f,
        };
    }

    /// <summary>Computes directional cone gain.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float CalculateConeGain(
        in Vector3 sourcePos,
        in Vector3 sourceForward,
        in Vector3 listenerPos,
        float innerAngle,
        float outerAngle,
        float outerGain)
    {
        if (innerAngle >= 360.0f)
        {
            return 1.0f;
        }

        Vector3 toListener = SafeNormalize(listenerPos - sourcePos);
        Vector3 forward = SafeNormalize(sourceForward);

        float dot = Vector3.Dot(forward, toListener);
        float angleDegrees = MathF.Acos(Math.Clamp(dot, -1.0f, 1.0f)) * (180.0f / MathF.PI) * 2.0f;

        if (angleDegrees <= innerAngle)
        {
            return 1.0f;
        }

        if (angleDegrees >= outerAngle)
        {
            return outerGain;
        }

        float delta = (angleDegrees - innerAngle) / Math.Max(1e-6f, outerAngle - innerAngle);
        return 1.0f - ((1.0f - outerGain) * delta);
    }

    /// <summary>Computes stereo pan in [-1, 1] from listener space.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float CalculateStereoPan(in Vector3 listenerPos, in Vector3 listenerRight, in Vector3 sourcePos)
    {
        Vector3 toSource = SafeNormalize(sourcePos - listenerPos);
        return Math.Clamp(Vector3.Dot(toSource, listenerRight), -1.0f, 1.0f);
    }
}
