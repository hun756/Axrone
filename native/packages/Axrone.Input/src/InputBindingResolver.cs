namespace Axrone.Input;

/// <summary>
/// Composes binding shapes into raw values: single controls, negative/positive
/// pairs, four-way pads and dual axes. Modifier gating and consumption are
/// applied here; processors run in the action slots.
/// </summary>
public static class InputBindingResolver
{
    /// <summary>Tries to resolve a scalar binding.</summary>
    /// <param name="snapshot">Debounced frame state.</param>
    /// <param name="binding">Binding to compose.</param>
    /// <param name="consumed">Controls consumed by higher-priority contexts.</param>
    /// <param name="gamepadSlot">Gamepad slot override.</param>
    /// <param name="value">Composed raw value.</param>
    /// <returns><see langword="true"/> when the binding contributes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveScalar(
        in InputSnapshot snapshot,
        in InputBinding binding,
        ConsumedControls consumed,
        byte gamepadSlot,
        out float value)
    {
        ArgumentNullException.ThrowIfNull(consumed);
        value = 0.0f;
        if (!InputControlReader.ModifiersSatisfied(
            InputControlReader.ReadModifiers(snapshot), binding.Modifiers, binding.ExactModifiers))
        {
            return false;
        }

        switch (binding.Kind)
        {
            case BindingKind.Control:
                if (consumed.Contains(binding.Path0))
                {
                    return false;
                }

                value = InputControlReader.ReadScalar(snapshot, binding.Path0, gamepadSlot);
                return true;
            case BindingKind.Axis:
                if (consumed.Contains(binding.Path0) || consumed.Contains(binding.Path1))
                {
                    return false;
                }

                value = InputControlReader.ReadScalar(snapshot, binding.Path1, gamepadSlot)
                    - InputControlReader.ReadScalar(snapshot, binding.Path0, gamepadSlot);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Tries to resolve a vector binding.</summary>
    /// <param name="snapshot">Debounced frame state.</param>
    /// <param name="binding">Binding to compose.</param>
    /// <param name="consumed">Controls consumed by higher-priority contexts.</param>
    /// <param name="gamepadSlot">Gamepad slot override.</param>
    /// <param name="value">Composed raw vector.</param>
    /// <returns><see langword="true"/> when the binding contributes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveVector2(
        in InputSnapshot snapshot,
        in InputBinding binding,
        ConsumedControls consumed,
        byte gamepadSlot,
        out Vector2 value)
    {
        ArgumentNullException.ThrowIfNull(consumed);
        value = Vector2.Zero;
        if (!InputControlReader.ModifiersSatisfied(
            InputControlReader.ReadModifiers(snapshot), binding.Modifiers, binding.ExactModifiers))
        {
            return false;
        }

        switch (binding.Kind)
        {
            case BindingKind.Control:
                if (consumed.Contains(binding.Path0))
                {
                    return false;
                }

                value = new Vector2(InputControlReader.ReadScalar(snapshot, binding.Path0, gamepadSlot), 0.0f);
                return true;
            case BindingKind.Vector2Directional:
                if (consumed.Contains(binding.Path0) || consumed.Contains(binding.Path1)
                    || consumed.Contains(binding.Path2) || consumed.Contains(binding.Path3))
                {
                    return false;
                }

                value = new Vector2(
                    InputControlReader.ReadScalar(snapshot, binding.Path3, gamepadSlot)
                        - InputControlReader.ReadScalar(snapshot, binding.Path2, gamepadSlot),
                    InputControlReader.ReadScalar(snapshot, binding.Path0, gamepadSlot)
                        - InputControlReader.ReadScalar(snapshot, binding.Path1, gamepadSlot));
                return true;
            case BindingKind.DualAxis:
                if (consumed.Contains(binding.Path0) || consumed.Contains(binding.Path1))
                {
                    return false;
                }

                value = new Vector2(
                    InputControlReader.ReadScalar(snapshot, binding.Path0, gamepadSlot),
                    InputControlReader.ReadScalar(snapshot, binding.Path1, gamepadSlot));
                return true;
            default:
                return false;
        }
    }

    /// <summary>Marks a binding's controls consumed.</summary>
    /// <param name="binding">Binding that read the controls.</param>
    /// <param name="consumed">Set to mark into.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MarkConsumed(in InputBinding binding, ConsumedControls consumed)
    {
        ArgumentNullException.ThrowIfNull(consumed);
        switch (binding.Kind)
        {
            case BindingKind.Control:
                consumed.Mark(binding.Path0);
                break;
            case BindingKind.Axis:
            case BindingKind.DualAxis:
                consumed.Mark(binding.Path0);
                consumed.Mark(binding.Path1);
                break;
            case BindingKind.Vector2Directional:
                consumed.Mark(binding.Path0);
                consumed.Mark(binding.Path1);
                consumed.Mark(binding.Path2);
                consumed.Mark(binding.Path3);
                break;
            default:
                break;
        }
    }
}
