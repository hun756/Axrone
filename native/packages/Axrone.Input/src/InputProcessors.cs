namespace Axrone.Input;

/// <summary>
/// Stateless value processors: pure functions over scalar and vector readings.
/// Scalar core covers identity, gain, negation and range clamping.
/// </summary>
public static class InputProcessor
{
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
}
