namespace Axrone.Input;

/// <summary>
/// Vector action slot: combines contributors (latest or sum), then applies
/// deadzone, normalization and processors.
/// </summary>
public sealed class InputVector2Slot
{
    private readonly InputBinding[] _bindings;
    private InputVector2State _state;
    private readonly InputVector2ActionDefinition _definition;

    /// <summary>Creates a slot.</summary>
    /// <param name="definition">Vector tuning.</param>
    /// <param name="bindings">Bindings in priority order.</param>
    public InputVector2Slot(InputVector2ActionDefinition definition, InputBinding[] bindings)
    {
        _definition = definition;
        _bindings = bindings;
    }

    /// <summary>Committed state.</summary>
    public ref InputVector2State State => ref _state;

    /// <summary>Evaluates one frame.</summary>
    /// <param name="snapshot">Debounced frame state.</param>
    /// <param name="consumed">Higher-priority consumption.</param>
    /// <param name="gamepadSlot">Gamepad slot override.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Evaluate(in InputSnapshot snapshot, ConsumedControls consumed, byte gamepadSlot)
    {
        Vector2 combined = Vector2.Zero;
        bool has = false;
        InputBinding lastUsed = default;
        bool lastHas = false;

        for (int i = 0; i < _bindings.Length; i++)
        {
            if (!InputBindingResolver.TryResolveVector2(snapshot, _bindings[i], consumed, gamepadSlot, out Vector2 candidate))
            {
                continue;
            }

            Vector2 processed = InputProcessor.ApplyBindingVector2(candidate, _bindings[i]);
            combined = _definition.Combine == Vector2CombineMode.Sum && has ? combined + processed : processed;
            has = true;
            lastUsed = _bindings[i];
            lastHas = true;
        }

        if (!has)
        {
            InputInteraction.UpdateVector2(ref _state, Vector2.Zero);
            return;
        }

        Vector2 value = combined;
        if (_definition.Deadzone > 0.0f && value.Length() < _definition.Deadzone)
        {
            value = Vector2.Zero;
        }

        if (_definition.Normalize && value != Vector2.Zero)
        {
            value = Vector2.Normalize(value);
        }

        value = InputProcessor.ApplyVector2(
            InputProcessor.ApplyVector2(value, _definition.Processor0), _definition.Processor1);
        InputInteraction.UpdateVector2(ref _state, value);

        if (lastHas && (lastUsed.Consume || _definition.Consume))
        {
            InputBindingResolver.MarkConsumed(lastUsed, consumed);
        }
    }

    /// <summary>Clears state.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _state = default;
}
