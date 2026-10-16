namespace Axrone.Audio;

/// <summary>
/// Distance attenuation policy: maps distance to gain in [minGain, 1].
/// Monomorphized, no vtable.
/// </summary>
public interface IAttenuationPolicy
{
    /// <summary>Computes gain for a distance.</summary>
    static abstract float ComputeGain(float distance, float refDistance, float maxDistance, float rolloff, float minGain);
}

/// <summary>
/// Inverse-distance attenuation.
/// </summary>
public readonly struct InverseAttenuationPolicy : IAttenuationPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeGain(float distance, float refDistance, float maxDistance, float rolloff, float minGain)
    {
        if (distance <= refDistance)
        {
            return 1.0f;
        }

        float gain = refDistance / (refDistance + (rolloff * (distance - refDistance)));
        return Math.Clamp(gain, minGain, 1.0f);
    }
}

/// <summary>
/// Linear attenuation.
/// </summary>
public readonly struct LinearAttenuationPolicy : IAttenuationPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeGain(float distance, float refDistance, float maxDistance, float rolloff, float minGain)
    {
        if (distance <= refDistance)
        {
            return 1.0f;
        }

        float spread = Math.Max(1e-6f, maxDistance - refDistance);
        float gain = 1.0f - ((rolloff * (distance - refDistance)) / spread);
        return Math.Clamp(gain, minGain, 1.0f);
    }
}

/// <summary>
/// Exponential attenuation.
/// </summary>
public readonly struct ExponentialAttenuationPolicy : IAttenuationPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeGain(float distance, float refDistance, float maxDistance, float rolloff, float minGain)
    {
        if (distance <= refDistance)
        {
            return 1.0f;
        }

        float ratio = Math.Max(1e-6f, distance / refDistance);
        float gain = MathF.Pow(ratio, -rolloff);
        return Math.Clamp(gain, minGain, 1.0f);
    }
}

/// <summary>
/// No attenuation.
/// </summary>
public readonly struct NoneAttenuationPolicy : IAttenuationPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeGain(float distance, float refDistance, float maxDistance, float rolloff, float minGain) => 1.0f;
}

/// <summary>
/// Stereo panning policy: maps normalized pan in [-1, 1] to channel gains.
/// Monomorphized, no vtable.
/// </summary>
public interface IPanningPolicy
{
    /// <summary>Computes left/right gains for a pan.</summary>
    static abstract void ComputeGains(float panNormalized, out float leftGain, out float rightGain);
}

/// <summary>
/// Equal-power panning.
/// </summary>
public readonly struct EqualPowerPanningPolicy : IPanningPolicy
{
    private const float HalfPi = MathF.PI * 0.5f;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ComputeGains(float panNormalized, out float leftGain, out float rightGain)
    {
        float p = Math.Clamp((panNormalized + 1.0f) * 0.5f, 0.0f, 1.0f);
        float angle = p * HalfPi;
        leftGain = MathF.Cos(angle);
        rightGain = MathF.Sin(angle);
    }
}

/// <summary>
/// Linear panning.
/// </summary>
public readonly struct LinearPanningPolicy : IPanningPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ComputeGains(float panNormalized, out float leftGain, out float rightGain)
    {
        float p = Math.Clamp((panNormalized + 1.0f) * 0.5f, 0.0f, 1.0f);
        leftGain = 1.0f - p;
        rightGain = p;
    }
}

/// <summary>
/// Voice eviction policy: selects a victim slot when voices are exhausted.
/// Monomorphized, no vtable.
/// </summary>
/// <typeparam name="TVoice">Voice slot type.</typeparam>
public interface IVoiceEvictionPolicy<TVoice> where TVoice : struct
{
    /// <summary>Selects a victim slot index.</summary>
    static abstract int SelectVictimSlot(ReadOnlySpan<TVoice> voices);
}
