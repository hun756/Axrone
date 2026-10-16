namespace Axrone.Input;

/// <summary>
/// Stateful button gestures: hysteresis plus press/hold/tap/multitap/repeat.
/// Press fires Performed on the down edge and Canceled on release.
/// </summary>
public static class InputInteraction
{
    /// <summary>Clears gesture state, e.g. on focus loss.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reset(ref InputButtonState state)
    {
        state = default;
    }

    /// <summary>Resolves raw analog input through hysteresis.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ResolveActuated(bool wasActuated, float raw, float pressPoint, float releasePoint)
    {
        return wasActuated ? raw >= releasePoint : raw >= pressPoint;
    }

    /// <summary>Updates a simple press.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdatePress(
        ref InputButtonState state,
        float raw,
        float deltaMs,
        float pressPoint = 0.5f,
        float releasePoint = 0.5f)
    {
        bool previous = state.Value;
        bool actuated = ResolveActuated(previous, raw, pressPoint, releasePoint);

        state.PreviousValue = previous;
        state.Value = actuated;
        state.RawValue = raw;
        state.Pressed = !previous && actuated;
        state.Released = previous && !actuated;
        state.HeldDurationMs = actuated ? (previous ? state.HeldDurationMs + deltaMs : 0.0f) : 0.0f;
        state.HoldTriggered = false;
        state.TapTriggered = false;
        state.MultiTapTriggered = false;
        state.RepeatTriggered = false;
        state.Phase = state.Pressed ? InputActionPhase.Performed
            : state.Released ? InputActionPhase.Canceled
            : InputActionPhase.None;
    }
}
