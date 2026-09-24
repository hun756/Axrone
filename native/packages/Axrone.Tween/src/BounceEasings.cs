namespace Axrone.Tween;

/// <summary>Bounce easing family, derived from the ease-out form.</summary>
public readonly struct EaseOutBounceCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        const float Slope = 7.5625f;
        const float Step = 2.75f;

        if (t < 1.0f / Step)
        {
            return Slope * t * t;
        }

        if (t < 2.0f / Step)
        {
            t -= 1.5f / Step;
            return (Slope * t * t) + 0.75f;
        }

        if (t < 2.5f / Step)
        {
            t -= 2.25f / Step;
            return (Slope * t * t) + 0.9375f;
        }

        t -= 2.625f / Step;
        return (Slope * t * t) + 0.984375f;
    }
}

/// <inheritdoc cref="EaseOutBounceCurve"/>
public readonly struct EaseInBounceCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => 1.0f - EaseOutBounceCurve.Evaluate(1.0f - t);
}

/// <inheritdoc cref="EaseOutBounceCurve"/>
public readonly struct EaseInOutBounceCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        if (t < 0.5f)
        {
            return (1.0f - EaseOutBounceCurve.Evaluate(1.0f - (2.0f * t))) * 0.5f;
        }

        return ((1.0f + EaseOutBounceCurve.Evaluate((2.0f * t) - 1.0f)) * 0.5f);
    }
}
