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
}
