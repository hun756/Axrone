namespace Axrone.Input;

/// <summary>
/// Button action slot: resolves its bindings to one raw scalar and commits
/// the gesture. The largest-magnitude contributor wins; processors run after
/// composition. Near-zero contributors are skipped.
/// </summary>
public sealed class InputButtonSlot
{
    private readonly InputBinding[] _bindings;
    private InputButtonState _state;
    private readonly InputButtonActionDefinition _definition;

    /// <summary>Creates a slot.</summary>
    /// <param name="definition">Button tuning.</param>
    /// <param name="bindings">Bindings in priority order.</param>
    public InputButtonSlot(InputButtonActionDefinition definition, InputBinding[] bindings)
    {
        _definition = definition;
        _bindings = bindings;
    }

    /// <summary>Committed state.</summary>
    public ref InputButtonState State => ref _state;

    /// <summary>Evaluates one frame.</summary>
    /// <param name="snapshot">Debounced frame state.</param>
    /// <param name="consumed">Higher-priority consumption.</param>
    /// <param name="gamepadSlot">Gamepad slot override.</param>
    /// <param name="deltaMs">Frame time in milliseconds.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Evaluate(in InputSnapshot snapshot, ConsumedControls consumed, byte gamepadSlot, float deltaMs)
    {
        float release = InputButtonActionDefinition.EffectiveReleasePoint(_definition);
        float raw = 0.0f;
        bool has = false;
        InputBinding used = default;
        for (int i = 0; i < _bindings.Length; i++)
        {
            if (!InputBindingResolver.TryResolveScalar(snapshot, _bindings[i], consumed, gamepadSlot, out float candidate))
            {
                continue;
            }

            if (MathF.Abs(candidate) <= InputProcessor.Epsilon)
            {
                continue;
            }

            if (!has || MathF.Abs(candidate) > MathF.Abs(raw))
            {
                raw = candidate;
                used = _bindings[i];
            }

            has = true;
        }

        if (!has)
        {
            InputInteraction.Update(ref _state, 0.0f, deltaMs, _definition.Interaction, _definition.PressPoint, release);
            return;
        }

        float processed = InputProcessor.ApplyBindingScalar(
            InputProcessor.ApplyScalar(InputProcessor.ApplyScalar(raw, _definition.Processor0), _definition.Processor1), used);
        InputInteraction.Update(ref _state, processed, deltaMs, _definition.Interaction, _definition.PressPoint, release);
        if (used.Consume || _definition.Consume)
        {
            InputBindingResolver.MarkConsumed(used, consumed);
        }
    }

    /// <summary>Clears gesture state.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => InputInteraction.Reset(ref _state);
}
