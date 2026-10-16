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

    /// <summary>Updates a sustained hold; down gives Started, threshold gives Performed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateHold(
        ref InputButtonState state,
        float raw,
        float deltaMs,
        in InteractionSettings settings,
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
        state.TapTriggered = false;
        state.MultiTapTriggered = false;
        state.RepeatTriggered = false;

        if (state.Pressed)
        {
            state.HeldDurationMs = 0.0f;
            state.HoldTriggered = false;
            state.Phase = InputActionPhase.Started;
            return;
        }

        if (state.Released)
        {
            state.HeldDurationMs = 0.0f;
            state.HoldTriggered = false;
            state.Phase = InputActionPhase.Canceled;
            return;
        }

        if (!actuated)
        {
            state.HeldDurationMs = 0.0f;
            state.HoldTriggered = false;
            state.Phase = InputActionPhase.None;
            return;
        }

        state.HeldDurationMs += deltaMs;
        if (state.HeldDurationMs >= settings.DurationMs)
        {
            if (settings.Mode == HoldMode.Continuous)
            {
                state.HoldTriggered = true;
                state.Phase = InputActionPhase.Performed;
            }
            else if (!state.HoldTriggered)
            {
                state.HoldTriggered = true;
                state.Phase = InputActionPhase.Performed;
            }
            else
            {
                state.Phase = InputActionPhase.None;
            }
        }
        else
        {
            state.Phase = InputActionPhase.Started;
        }
    }

    /// <summary>Updates a quick tap; release within budget gives Performed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateTap(
        ref InputButtonState state,
        float raw,
        float deltaMs,
        in InteractionSettings settings,
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
        state.HoldTriggered = false;
        state.MultiTapTriggered = false;
        state.RepeatTriggered = false;

        if (state.Pressed)
        {
            state.HeldDurationMs = 0.0f;
            state.TapTriggered = false;
            state.Phase = InputActionPhase.Started;
            return;
        }

        if (actuated)
        {
            state.HeldDurationMs += deltaMs;
            if (state.HeldDurationMs > settings.DurationMs)
            {
                state.TapTriggered = false;
                state.Phase = InputActionPhase.Canceled;
            }
            else
            {
                state.Phase = InputActionPhase.Started;
            }

            return;
        }

        if (state.Released)
        {
            if (state.HeldDurationMs <= settings.DurationMs)
            {
                state.TapTriggered = true;
                state.TapSequenceCount = 1;
                state.TimeSinceLastTapMs = 0.0f;
                state.Phase = InputActionPhase.Performed;
            }
            else
            {
                state.TapTriggered = false;
                state.Phase = InputActionPhase.Canceled;
            }

            state.HeldDurationMs = 0.0f;
            return;
        }

        state.TapTriggered = false;
        state.Phase = InputActionPhase.None;
    }

    /// <summary>Updates repeated taps; completion gives Performed, gap expiry gives Canceled.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateMultiTap(
        ref InputButtonState state,
        float raw,
        float deltaMs,
        in InteractionSettings settings,
        float pressPoint = 0.5f,
        float releasePoint = 0.5f)
    {
        bool previous = state.Value;
        bool actuated = ResolveActuated(previous, raw, pressPoint, releasePoint);
        int required = settings.TapCount < 2 ? 2 : settings.TapCount;

        state.PreviousValue = previous;
        state.Value = actuated;
        state.RawValue = raw;
        state.Pressed = !previous && actuated;
        state.Released = previous && !actuated;
        state.HoldTriggered = false;
        state.TapTriggered = false;
        state.RepeatTriggered = false;
        state.MultiTapTriggered = false;

        if (state.Pressed)
        {
            state.HeldDurationMs = 0.0f;
            state.Phase = InputActionPhase.Started;
            return;
        }

        if (actuated)
        {
            state.HeldDurationMs += deltaMs;
            state.Phase = state.HeldDurationMs > settings.DurationMs
                ? InputActionPhase.Canceled
                : InputActionPhase.Started;
            if (state.HeldDurationMs > settings.DurationMs)
            {
                state.TapSequenceCount = 0;
            }

            return;
        }

        if (state.Released)
        {
            if (state.HeldDurationMs <= settings.DurationMs)
            {
                state.TapSequenceCount++;
                state.TimeSinceLastTapMs = 0.0f;
                if (state.TapSequenceCount >= required)
                {
                    state.MultiTapTriggered = true;
                    state.TapSequenceCount = 0;
                    state.Phase = InputActionPhase.Performed;
                }
                else
                {
                    state.Phase = InputActionPhase.Started;
                }
            }
            else
            {
                state.TapSequenceCount = 0;
                state.Phase = InputActionPhase.Canceled;
            }

            state.HeldDurationMs = 0.0f;
            return;
        }

        if (state.TapSequenceCount > 0)
        {
            state.TimeSinceLastTapMs += deltaMs;
            if (state.TimeSinceLastTapMs > settings.MaxDelayMs)
            {
                state.TapSequenceCount = 0;
                state.Phase = InputActionPhase.Canceled;
                return;
            }
        }

        state.Phase = state.TapSequenceCount > 0 ? InputActionPhase.Started : InputActionPhase.None;
    }
}
