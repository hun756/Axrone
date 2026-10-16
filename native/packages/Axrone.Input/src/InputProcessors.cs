namespace Axrone.Input;

/// <summary>
/// Stateless value processors: pure functions over scalar and vector readings.
/// Scalar core covers identity, gain, negation and range clamping.
/// </summary>
public static class InputProcessor
{
    /// <summary>Zero threshold for contributor contributions.</summary>
    internal const float Epsilon = 0.00001f;

    /// <summary>Applies one processor to a scalar reading.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ApplyScalar(float value, in ProcessorConfiguration processor)
    {
        return processor.Kind switch
        {
            InputProcessorKind.None => value,
            InputProcessorKind.Scale => value * processor.Param1,
            InputProcessorKind.Invert => -value,
            InputProcessorKind.Clamp => ClampScalar(value, processor.Param1, processor.Param2),
            InputProcessorKind.Deadzone => DeadzoneScalar(value, processor.Param1),
            InputProcessorKind.Curve => CurveScalar(value, processor.Param1, processor.Flag),
            _ => value,
        };
    }

    /// <summary>Applies two processors in order.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ApplyScalar(float value, in ProcessorConfiguration first, in ProcessorConfiguration second)
    {
        return ApplyScalar(ApplyScalar(value, first), second);
    }

    /// <summary>Applies one processor to a vector reading.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ApplyVector2(Vector2 value, in ProcessorConfiguration processor)
    {
        return processor.Kind switch
        {
            InputProcessorKind.None => value,
            InputProcessorKind.Scale => value * processor.Param1,
            InputProcessorKind.Invert => -value,
            InputProcessorKind.Clamp => new Vector2(
                ClampScalar(value.X, processor.Param1, processor.Param2),
                ClampScalar(value.Y, processor.Param1, processor.Param2)),
            InputProcessorKind.Deadzone => DeadzoneVector2(value, processor.Param1),
            InputProcessorKind.Curve => new Vector2(
                CurveScalar(value.X, processor.Param1, processor.Flag),
                CurveScalar(value.Y, processor.Param1, processor.Flag)),
            InputProcessorKind.ScaleVector2 => new Vector2(value.X * processor.Param1, value.Y * processor.Param2),
            InputProcessorKind.InvertVector2 => new Vector2(
                processor.Param1 != 0.0f ? -value.X : value.X,
                processor.Param2 != 0.0f ? -value.Y : value.Y),
            InputProcessorKind.NormalizeVector2 => NormalizeVector2(value),
            InputProcessorKind.ClampMagnitude => ClampMagnitude(value, processor.Param1, processor.Param2),
            _ => value,
        };
    }

    /// <summary>Applies two processors in order.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ApplyVector2(Vector2 value, in ProcessorConfiguration first, in ProcessorConfiguration second)
    {
        return ApplyVector2(ApplyVector2(value, first), second);
    }

    /// <summary>Clamps with swapped bounds tolerated.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ClampScalar(float value, float min, float max)
    {
        float lo = min <= max ? min : max;
        float hi = min <= max ? max : min;
        return value < lo ? lo : (value > hi ? hi : value);
    }

    /// <summary>Zeroes below threshold, rescales the remainder to [0, 1].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DeadzoneScalar(float value, float threshold)
    {
        float abs = MathF.Abs(value);
        if (abs < threshold || threshold >= 1.0f)
        {
            return 0.0f;
        }

        float scaled = (abs - threshold) / (1.0f - threshold);
        return MathF.CopySign(scaled, value);
    }

    /// <summary>Power curve; keeps the sign when requested.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CurveScalar(float value, float exponent, bool keepSign)
    {
        if (exponent <= 0.0f)
        {
            return value;
        }

        if (keepSign)
        {
            return MathF.CopySign(MathF.Pow(MathF.Abs(value), exponent), value);
        }

        return value <= 0.0f ? 0.0f : MathF.Pow(value, exponent);
    }

    /// <summary>Radial deadzone for sticks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 DeadzoneVector2(Vector2 value, float threshold)
    {
        float length = value.Length();
        if (length < threshold || threshold >= 1.0f || length <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        float scaled = (length - threshold) / (1.0f - threshold);
        return value / length * MathF.Min(scaled, 1.0f);
    }

    /// <summary>Normalizes; zero stays zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 NormalizeVector2(Vector2 value)
    {
        float length = value.Length();
        return length <= float.Epsilon ? Vector2.Zero : value / length;
    }

    /// <summary>Clamps the magnitude into [min, max].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 ClampMagnitude(Vector2 value, float min, float max)
    {
        float length = value.Length();
        if (length <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        float lo = min <= max ? min : max;
        float hi = min <= max ? max : min;
        float clamped = length < lo ? lo : (length > hi ? hi : length);
        return value / length * clamped;
    }

    /// <summary>Applies binding gain, negation, deadzone and processors to a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ApplyBindingScalar(float value, in InputBinding binding)
    {
        float scaled = value * binding.Scale;
        if (binding.Invert)
        {
            scaled = -scaled;
        }

        if (binding.Deadzone > 0.0f && MathF.Abs(scaled) < binding.Deadzone)
        {
            scaled = 0.0f;
        }

        return ApplyScalar(ApplyScalar(scaled, binding.Processor0), binding.Processor1);
    }

    /// <summary>Applies binding gain, negation, deadzone, normalization and processors to a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ApplyBindingVector2(Vector2 value, in InputBinding binding)
    {
        Vector2 scaled = value * binding.Scale;
        if (binding.Invert)
        {
            scaled = -scaled;
        }

        if (binding.Deadzone > 0.0f)
        {
            scaled = DeadzoneVector2(scaled, binding.Deadzone);
        }

        if (binding.Normalize)
        {
            scaled = NormalizeVector2(scaled);
        }

        return ApplyVector2(ApplyVector2(scaled, binding.Processor0), binding.Processor1);
    }
}
