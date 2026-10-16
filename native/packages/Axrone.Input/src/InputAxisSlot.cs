namespace Axrone.Input;

/// <summary>
/// Axis action slot: combines contributors (latest, sum or max-abs), then
/// applies deadzone, clamping and processors.
/// </summary>
public sealed class InputAxisSlot
{
    private readonly InputBinding[] _bindings;
    private InputAxisState _state;
    private readonly InputAxisActionDefinition _definition;

    /// <summary>Creates a slot.</summary>
    /// <param name="definition">Axis tuning.</param>
    /// <param name="bindings">Bindings in priority order.</param>
    public InputAxisSlot(InputAxisActionDefinition definition, InputBinding[] bindings)
    {
        _definition = definition;
        _bindings = bindings;
    }

    /// <summary>Committed state.</summary>
    public ref InputAxisState State => ref _state;

    /// <summary>Evaluates one frame.</summary>
    /// <param name="snapshot">Debounced frame state.</param>
    /// <param name="consumed">Higher-priority consumption.</param>
    /// <param name="gamepadSlot">Gamepad slot override.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Evaluate(in InputSnapshot snapshot, ConsumedControls consumed, byte gamepadSlot)
    {
        float combined = 0.0f;
        bool has = false;
        InputBinding lastUsed = default;
        bool lastHas = false;

        for (int i = 0; i < _bindings.Length; i++)
        {
            if (!InputBindingResolver.TryResolveScalar(snapshot, _bindings[i], consumed, gamepadSlot, out float candidate))
            {
                continue;
            }

            float processed = InputProcessor.ApplyBindingScalar(candidate, _bindings[i]);
            switch (_definition.Combine)
            {
                case AxisCombineMode.Sum:
                    combined += processed;
                    break;
                case AxisCombineMode.MaxAbs:
                    if (!has || MathF.Abs(processed) > MathF.Abs(combined))
                    {
                        combined = processed;
                    }

                    break;
                default:
                    combined = processed;
                    break;
            }

            has = true;
            lastUsed = _bindings[i];
            lastHas = true;
        }

        if (!has)
        {
            InputInteraction.UpdateAxis(ref _state, 0.0f);
            return;
        }

        float value = combined;
        if (_definition.Deadzone > 0.0f && MathF.Abs(value) < _definition.Deadzone)
        {
            value = 0.0f;
        }

        float lo = _definition.ClampMin <= _definition.ClampMax ? _definition.ClampMin : _definition.ClampMax;
        float hi = _definition.ClampMin <= _definition.ClampMax ? _definition.ClampMax : _definition.ClampMin;
        value = value < lo ? lo : (value > hi ? hi : value);
        value = InputProcessor.ApplyScalar(InputProcessor.ApplyScalar(value, _definition.Processor0), _definition.Processor1);
        InputInteraction.UpdateAxis(ref _state, value);

        if (lastHas && (lastUsed.Consume || _definition.Consume))
        {
            InputBindingResolver.MarkConsumed(lastUsed, consumed);
        }
    }

    /// <summary>Clears state.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _state = default;
}
