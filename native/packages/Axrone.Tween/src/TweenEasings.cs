namespace Axrone.Tween;

/// <summary>
/// Parametric easing factories: the canonical Penner forms behind the fixed built-in
/// curves, with amplitude/period/overshoot exposed. Use with
/// <c>TweenBuilder.Ease(Func&lt;float, float&gt;)</c>.
/// </summary>
/// <remarks>
/// Built-in <see cref="EasingKind"/> elastic/back curves keep their exact historical
/// values for compatibility. These factories implement the canonical reference forms:
/// back defaults reproduce the built-ins bit-for-bit, elastic defaults follow Penner
/// (period/4 phase) and differ slightly from the built-ins. Endpoints are exact —
/// every factory returns 0 at rest and 1 at completion.
/// </remarks>
public static class TweenEasings
{
    /// <summary>Elastic ease-in with configurable amplitude and period.</summary>
    public static Func<float, float> ElasticIn(float amplitude = 1.0f, float period = 0.3f)
    {
        ValidateElastic(amplitude, period);
        float a = Math.Max(amplitude, 1.0f);
        float s = period / 4.0f;
        return t =>
        {
            if (t <= 0.0f)
            {
                return 0.0f;
            }

            if (t >= 1.0f)
            {
                return 1.0f;
            }

            return -a * MathF.Pow(2.0f, 10.0f * (t - 1.0f)) * MathF.Sin(((t - 1.0f - s) * 2.0f * MathF.PI) / period);
        };
    }

    /// <summary>Elastic ease-out with configurable amplitude and period.</summary>
    public static Func<float, float> ElasticOut(float amplitude = 1.0f, float period = 0.3f)
    {
        ValidateElastic(amplitude, period);
        float a = Math.Max(amplitude, 1.0f);
        float s = period / 4.0f;
        return t =>
        {
            if (t <= 0.0f)
            {
                return 0.0f;
            }

            if (t >= 1.0f)
            {
                return 1.0f;
            }

            return (a * MathF.Pow(2.0f, -10.0f * t) * MathF.Sin(((t - s) * 2.0f * MathF.PI) / period)) + 1.0f;
        };
    }

    /// <summary>Elastic ease-in-out with configurable amplitude and period.</summary>
    public static Func<float, float> ElasticInOut(float amplitude = 1.0f, float period = 0.45f)
    {
        ValidateElastic(amplitude, period);
        float a = Math.Max(amplitude, 1.0f);
        float s = period / 4.0f;
        return t =>
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
                return -0.5f * a * MathF.Pow(2.0f, 10.0f * (t - 1.0f)) * MathF.Sin(((t - 1.0f - s) * 2.0f * MathF.PI) / period);
            }

            return (0.5f * a * MathF.Pow(2.0f, -10.0f * (t - 1.0f)) * MathF.Sin(((t - 1.0f - s) * 2.0f * MathF.PI) / period)) + 1.0f;
        };
    }

    /// <summary>Back ease-in with configurable overshoot. Matches the built-in at default.</summary>
    public static Func<float, float> BackIn(float overshoot = 1.70158f)
    {
        ValidateOvershoot(overshoot);
        return t => (((overshoot + 1.0f) * t * t * t) - (overshoot * t * t));
    }

    /// <summary>Back ease-out with configurable overshoot. Matches the built-in at default.</summary>
    public static Func<float, float> BackOut(float overshoot = 1.70158f)
    {
        ValidateOvershoot(overshoot);
        return t =>
        {
            float d = t - 1.0f;
            return 1.0f + (((overshoot + 1.0f) * d * d * d) + (overshoot * d * d));
        };
    }

    /// <summary>Back ease-in-out with configurable overshoot. Matches the built-in at default.</summary>
    public static Func<float, float> BackInOut(float overshoot = 1.70158f)
    {
        ValidateOvershoot(overshoot);
        float s = overshoot * 1.525f;
        return t =>
        {
            if (t < 0.5f)
            {
                return 0.5f * ((4.0f * t * t * (((s + 1.0f) * 2.0f * t) - s)));
            }

            float d = (2.0f * t) - 2.0f;
            return 0.5f * (((d * d) * (((s + 1.0f) * d) + s)) + 2.0f);
        };
    }

    private static void ValidateElastic(float amplitude, float period)
    {
        if (amplitude <= 0.0f || !float.IsFinite(amplitude))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(amplitude));
        }

        if (period <= 0.0f || !float.IsFinite(period))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(period));
        }
    }

    private static void ValidateOvershoot(float overshoot)
    {
        if (overshoot < 0.0f || !float.IsFinite(overshoot))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(overshoot));
        }
    }
}
