namespace Axrone.Tween;

/// <summary>Exponential and circular easing families.</summary>
public readonly struct EaseInExpoCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => t == 0.0f ? 0.0f : MathF.Pow(2.0f, 10.0f * (t - 1.0f));
}

/// <inheritdoc cref="EaseInExpoCurve"/>
public readonly struct EaseOutExpoCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => t >= 1.0f ? 1.0f : 1.0f - MathF.Pow(2.0f, -10.0f * t);
}

/// <inheritdoc cref="EaseInExpoCurve"/>
public readonly struct EaseInOutExpoCurve : IEasingFunction
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

        if (t < 0.5f)
        {
            return 0.5f * MathF.Pow(2.0f, (20.0f * t) - 10.0f);
        }

        return 1.0f - (0.5f * MathF.Pow(2.0f, (-20.0f * t) + 10.0f));
    }
}

/// <inheritdoc cref="EaseInExpoCurve"/>
public readonly struct EaseInCircCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => 1.0f - MathF.Sqrt(1.0f - (t * t));
}

/// <inheritdoc cref="EaseInExpoCurve"/>
public readonly struct EaseOutCircCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        float f = t - 1.0f;
        return MathF.Sqrt(1.0f - (f * f));
    }
}

/// <inheritdoc cref="EaseInExpoCurve"/>
public readonly struct EaseInOutCircCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        if (t < 0.5f)
        {
            return 0.5f * (1.0f - MathF.Sqrt(1.0f - (4.0f * t * t)));
        }

        float f = (2.0f * t) - 2.0f;
        return 0.5f * (MathF.Sqrt(1.0f - (f * f)) + 1.0f);
    }
}
