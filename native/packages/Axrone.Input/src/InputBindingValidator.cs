namespace Axrone.Input;

/// <summary>
/// Binding validation: composition kind must fit the action shape and
/// processors must match the scalar/vector pipeline.
/// </summary>
public static class InputBindingValidator
{
    /// <summary>Validates one binding against an action kind.</summary>
    /// <param name="kind">Action value shape.</param>
    /// <param name="binding">Binding to validate.</param>
    /// <param name="error">Failure reason.</param>
    /// <returns><see langword="true"/> when the binding fits.</returns>
    public static bool TryValidate(ActionKind kind, in InputBinding binding, out string? error)
    {
        bool compositionFits = kind == ActionKind.Vector2
            ? binding.Kind is BindingKind.Vector2Directional or BindingKind.DualAxis or BindingKind.Control
            : binding.Kind is BindingKind.Control or BindingKind.Axis;
        if (!compositionFits)
        {
            error = $"Binding kind {binding.Kind} does not fit {kind} actions.";
            return false;
        }

        if (kind != ActionKind.Vector2
            && (!IsScalarProcessor(binding.Processor0) || !IsScalarProcessor(binding.Processor1)))
        {
            error = "Vector processors do not fit scalar bindings.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Whether a processor runs in the scalar pipeline.</summary>
    /// <param name="processor">Processor configuration.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsScalarProcessor(in ProcessorConfiguration processor) =>
        processor.Kind is InputProcessorKind.None
            or InputProcessorKind.Scale
            or InputProcessorKind.Invert
            or InputProcessorKind.Clamp
            or InputProcessorKind.Deadzone
            or InputProcessorKind.Curve;
}
