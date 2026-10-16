namespace Axrone.Input;

/// <summary>
/// Typed action engine update: evaluates contexts in priority order, routes
/// gamepad slots per user, applies capture and raises callbacks. Part 2 of
/// the <see cref="InputEngine{TSchema}"/> partial.
/// </summary>
public sealed partial class InputEngine<TSchema> where TSchema : unmanaged, Enum
{
    private readonly TSchema[] _actions = Enum.GetValues<TSchema>();

    /// <summary>Evaluates one frame over the current snapshot.</summary>
    /// <param name="deltaMs">Frame time in milliseconds; negatives clamp to zero.</param>
    public void Update(float deltaMs)
    {
        ThrowIfDisposed();
        if (!_focused)
        {
            return;
        }

        float dt = deltaMs < 0.0f ? 0.0f : deltaMs;
        _consumed.Clear();
        AdvanceRebind(dt);
        FeedRebind();

        for (int c = 0; c < _contexts.Count; c++)
        {
            ContextEntry context = _contexts[c];
            if (!context.Enabled)
            {
                continue;
            }

            if (!ResolveUser(context.UserId, out InputUserDefinition? user) || user is null || !user.Enabled)
            {
                continue;
            }

            if (UsesOnlyUnownedDevices(context, user))
            {
                continue;
            }

            byte slot = GamepadSlot(user);
            for (int a = 0; a < _actionCount; a++)
            {
                EvaluateAction(context, a, slot, dt);
            }

            if (context.Capture == ContextCaptureMode.Used)
            {
                CaptureAll(context);
            }
        }
    }

    /// <summary>Reads a committed button state.</summary>
    /// <param name="contextId">Context identity.</param>
    /// <param name="action">Action value.</param>
    /// <param name="state">Committed state.</param>
    /// <returns><see langword="true"/> when the context and slot exist.</returns>
    public bool TryGetButton(ContextId contextId, TSchema action, out InputButtonState state)
    {
        state = default;
        ContextEntry? context = FindContext(contextId);
        if (context is null)
        {
            return false;
        }

        InputButtonSlot? slot = context.ButtonSlot(ToIndex(action));
        if (slot is null)
        {
            return false;
        }

        state = slot.State;
        return true;
    }

    /// <summary>Reads a committed axis state.</summary>
    /// <param name="contextId">Context identity.</param>
    /// <param name="action">Action value.</param>
    /// <param name="state">Committed state.</param>
    /// <returns><see langword="true"/> when the context and slot exist.</returns>
    public bool TryGetAxis(ContextId contextId, TSchema action, out InputAxisState state)
    {
        state = default;
        ContextEntry? context = FindContext(contextId);
        if (context is null)
        {
            return false;
        }

        InputAxisSlot? slot = context.AxisSlot(ToIndex(action));
        if (slot is null)
        {
            return false;
        }

        state = slot.State;
        return true;
    }

    /// <summary>Reads a committed vector state.</summary>
    /// <param name="contextId">Context identity.</param>
    /// <param name="action">Action value.</param>
    /// <param name="state">Committed state.</param>
    /// <returns><see langword="true"/> when the context and slot exist.</returns>
    public bool TryGetVector2(ContextId contextId, TSchema action, out InputVector2State state)
    {
        state = default;
        ContextEntry? context = FindContext(contextId);
        if (context is null)
        {
            return false;
        }

        InputVector2Slot? slot = context.VectorSlot(ToIndex(action));
        if (slot is null)
        {
            return false;
        }

        state = slot.State;
        return true;
    }

    /// <summary>Evaluates one action slot and raises callbacks on transitions.</summary>
    /// <param name="context">Owning context.</param>
    /// <param name="index">Action index.</param>
    /// <param name="gamepadSlot">Gamepad slot override.</param>
    /// <param name="deltaMs">Frame time in milliseconds.</param>
    private void EvaluateAction(ContextEntry context, int index, byte gamepadSlot, float deltaMs)
    {
        TSchema action = _actions[index];
        switch (context.Kind(index))
        {
            case ActionKind.Axis:
                {
                    InputAxisSlot? slot = context.AxisSlot(index);
                    if (slot is null)
                    {
                        return;
                    }

                    slot.Evaluate(_snapshot, _consumed, gamepadSlot);
                    if (slot.State.Phase != InputActionPhase.None)
                    {
                        Raise(action, slot.State.Phase, ActionKind.Axis, default, slot.State, default);
                    }

                    break;
                }

            case ActionKind.Vector2:
                {
                    InputVector2Slot? slot = context.VectorSlot(index);
                    if (slot is null)
                    {
                        return;
                    }

                    slot.Evaluate(_snapshot, _consumed, gamepadSlot);
                    if (slot.State.Phase != InputActionPhase.None)
                    {
                        Raise(action, slot.State.Phase, ActionKind.Vector2, default, default, slot.State);
                    }

                    break;
                }

            default:
                {
                    InputButtonSlot? slot = context.ButtonSlot(index);
                    if (slot is null)
                    {
                        return;
                    }

                    slot.Evaluate(_snapshot, _consumed, gamepadSlot, deltaMs);
                    if (slot.State.Phase != InputActionPhase.None)
                    {
                        Raise(action, slot.State.Phase, ActionKind.Button, slot.State, default, default);
                    }

                    break;
                }
        }
    }

    /// <summary>Raises callbacks for one committed action.</summary>
    /// <param name="action">Action value.</param>
    /// <param name="phase">Committed phase.</param>
    /// <param name="kind">Value shape.</param>
    /// <param name="button">Button state.</param>
    /// <param name="axis">Axis state.</param>
    /// <param name="vector">Vector state.</param>
    private void Raise(
        TSchema action,
        InputActionPhase phase,
        ActionKind kind,
        in InputButtonState button,
        in InputAxisState axis,
        in InputVector2State vector)
    {
        if (_callbacks.Count == 0)
        {
            return;
        }

        var payload = new InputActionCallbackContext<TSchema>(action, phase, kind, button, axis, vector);
        for (int i = 0; i < _callbacks.Count; i++)
        {
            _callbacks[i](payload);
        }
    }

    /// <summary>Marks every binding control of a capturing context consumed.</summary>
    /// <param name="context">Capturing context.</param>
    private void CaptureAll(ContextEntry context)
    {
        for (int a = 0; a < _actionCount; a++)
        {
            InputBinding[] bindings = context.GetBindings(a);
            for (int i = 0; i < bindings.Length; i++)
            {
                InputBindingResolver.MarkConsumed(bindings[i], _consumed);
            }
        }
    }

    /// <summary>Resolves the effective user for a context.</summary>
    /// <param name="userId">Context owner, or any user.</param>
    /// <param name="user">Effective user.</param>
    /// <returns><see langword="true"/> when a user applies.</returns>
    private bool ResolveUser(UserId userId, out InputUserDefinition? user)
    {
        if (!userId.Equals(UserId.Any))
        {
            return _users.TryGetValue(userId, out user);
        }

        foreach (InputUserDefinition candidate in _users.Values)
        {
            if (candidate.Enabled)
            {
                user = candidate;
                return true;
            }
        }

        user = null;
        return _users.Count == 0;
    }

    /// <summary>Selects the first assigned gamepad slot.</summary>
    /// <param name="user">Effective user.</param>
    private static byte GamepadSlot(InputUserDefinition? user)
    {
        if (user is null)
        {
            return 0;
        }

        uint mask = user.AssignedGamepadMask;
        for (byte slot = 0; slot < 4; slot++)
        {
            if (((mask >> slot) & 1u) != 0)
            {
                return slot;
            }
        }

        return 0;
    }

    /// <summary>Whether a context only touches devices its user does not own.</summary>
    /// <param name="context">Context to test.</param>
    /// <param name="user">Effective user.</param>
    private bool UsesOnlyUnownedDevices(ContextEntry context, InputUserDefinition? user)
    {
        if (user is null)
        {
            return false;
        }

        bool any = false;
        bool owned = false;
        for (int a = 0; a < _actionCount; a++)
        {
            InputBinding[] bindings = context.GetBindings(a);
            for (int i = 0; i < bindings.Length; i++)
            {
                any = true;
                if (OwnsDevice(user, bindings[i]))
                {
                    owned = true;
                    return false;
                }
            }
        }

        return any && !owned;
    }

    /// <summary>Whether a user owns at least one device a binding touches.</summary>
    /// <param name="user">Effective user.</param>
    /// <param name="binding">Binding to test.</param>
    private static bool OwnsDevice(InputUserDefinition user, in InputBinding binding)
    {
        return OwnsPath(user, binding.Path0)
            || (binding.Kind != BindingKind.Control && OwnsPath(user, binding.Path1))
            || ((binding.Kind == BindingKind.Vector2Directional || binding.Kind == BindingKind.DualAxis)
                && (OwnsPath(user, binding.Path2) || OwnsPath(user, binding.Path3)));
    }

    /// <summary>Whether a user owns one control's device.</summary>
    /// <param name="user">Effective user.</param>
    /// <param name="path">Control address.</param>
    private static bool OwnsPath(InputUserDefinition user, in InputControlPath path)
    {
        return path.Device switch
        {
            DeviceKind.Keyboard => user.OwnsKeyboard,
            DeviceKind.Mouse => user.OwnsMouse,
            DeviceKind.Touch => user.OwnsTouch,
            DeviceKind.Gamepad => user.AssignedGamepadMask != 0,
            _ => false,
        };
    }
}
