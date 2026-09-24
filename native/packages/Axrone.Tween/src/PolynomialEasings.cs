namespace Axrone.Tween;

/// <summary>Polynomial easing family: linear through quintic, in/out/in-out phases.</summary>
public readonly struct LinearCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => t;
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseInQuadCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => t * t;
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseOutQuadCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => t * (2.0f - t);
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseInOutQuadCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => t < 0.5f ? 2.0f * t * t : -1.0f + ((4.0f - 2.0f * t) * t);
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseInCubicCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => t * t * t;
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseOutCubicCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        float f = t - 1.0f;
        return (f * f * f) + 1.0f;
    }
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseInOutCubicCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        if (t < 0.5f)
        {
            return 4.0f * t * t * t;
        }

        float f = (2.0f * t) - 2.0f;
        return (0.5f * f * f * f) + 1.0f;
    }
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseInQuartCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => t * t * t * t;
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseOutQuartCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        float f = t - 1.0f;
        return 1.0f - (f * f * f * f);
    }
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseInOutQuartCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        if (t < 0.5f)
        {
            return 8.0f * t * t * t * t;
        }

        float f = t - 1.0f;
        return 1.0f - (8.0f * f * f * f * f);
    }
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseInQuintCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t) => t * t * t * t * t;
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseOutQuintCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        float f = t - 1.0f;
        return (f * f * f * f * f) + 1.0f;
    }
}

/// <inheritdoc cref="LinearCurve"/>
public readonly struct EaseInOutQuintCurve : IEasingFunction
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(float t)
    {
        if (t < 0.5f)
        {
            return 16.0f * t * t * t * t * t;
        }

        float f = (2.0f * t) - 2.0f;
        return (0.5f * f * f * f * f * f) + 1.0f;
    }
}
