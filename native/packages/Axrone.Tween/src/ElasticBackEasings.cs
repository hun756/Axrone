namespace Axrone.Tween;

/// <summary>Elastic and back easing families. Both overshoot the unit interval by design.</summary>
public readonly struct EaseInElasticCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        if (t <= 0.0f)
        {
            return 0.0f;
        }

        if (t >= 1.0f)
        {
            return 1.0f;
        }

        return -MathF.Pow(2.0f, 10.0f * (t - 1.0f)) * MathF.Sin(((t - 1.1f) * 2.0f * MathF.PI) / 0.4f);
    }
}

/// <inheritdoc cref="EaseInElasticCurve"/>
public readonly struct EaseOutElasticCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        if (t <= 0.0f)
        {
            return 0.0f;
        }

        if (t >= 1.0f)
        {
            return 1.0f;
        }

        return (MathF.Pow(2.0f, -10.0f * t) * MathF.Sin(((t - 0.1f) * 2.0f * MathF.PI) / 0.4f)) + 1.0f;
    }
}

/// <inheritdoc cref="EaseInElasticCurve"/>
public readonly struct EaseInOutElasticCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        if (t <= 0.0f)
        {
            return 0.0f;
        }

        if (t >= 1.0f)
        {
            return 1.0f;
        }

        t *= 2.0f;
        if (t < 1.0f)
        {
            return -0.5f * MathF.Pow(2.0f, 10.0f * (t - 1.0f)) * MathF.Sin(((t - 1.15f) * 2.0f * MathF.PI) / 0.45f);
        }

        return (0.5f * MathF.Pow(2.0f, -10.0f * (t - 1.0f)) * MathF.Sin(((t - 1.15f) * 2.0f * MathF.PI) / 0.45f)) + 1.0f;
    }
}

/// <inheritdoc cref="EaseInElasticCurve"/>
public readonly struct EaseInBackCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        const float Overshoot = 1.70158f;
        return ((Overshoot + 1.0f) * t * t * t) - (Overshoot * t * t);
    }
}

/// <inheritdoc cref="EaseInElasticCurve"/>
public readonly struct EaseOutBackCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        const float Overshoot = 1.70158f;
        float f = t - 1.0f;
        return 1.0f + (((Overshoot + 1.0f) * f * f * f) + (Overshoot * f * f));
    }
}

/// <inheritdoc cref="EaseInElasticCurve"/>
public readonly struct EaseInOutBackCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        const float Overshoot = 1.70158f * 1.525f;
        if (t < 0.5f)
        {
            return 0.5f * ((4.0f * t * t * (((Overshoot + 1.0f) * 2.0f * t) - Overshoot)));
        }

        float f = (2.0f * t) - 2.0f;
        return 0.5f * (((f * f) * (((Overshoot + 1.0f) * f) + Overshoot)) + 2.0f);
    }
}
