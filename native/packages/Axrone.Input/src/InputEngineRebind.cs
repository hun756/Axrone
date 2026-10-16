namespace Axrone.Input;

/// <summary>
/// Engine rebind wiring: single active session with replace semantics,
/// engine-time timeouts, automatic candidate feeding from the snapshot and
/// binding replacement. Part 3 of the <see cref="InputEngine{TSchema}"/>
/// partial.
/// </summary>
public sealed partial class InputEngine<TSchema> where TSchema : unmanaged, Enum
{
    private InputRebindSession<TSchema>? _rebind;
    private double _timeMs;

    /// <summary>Engine time in milliseconds.</summary>
    public double TimeMs => _timeMs;

    /// <summary>Whether a rebind session listens.</summary>
    public bool HasActiveRebind => _rebind is not null && _rebind.IsActive;

    /// <summary>Starts a rebind session, replacing any active one.</summary>
    /// <param name="request">Rebind request.</param>
    /// <param name="handlers">Session handlers.</param>
    public void BeginRebind(RebindRequest<TSchema> request, RebindHandlers<TSchema> handlers)
    {
        ThrowIfDisposed();
        _rebind?.Cancel(RebindCancelReason.Replaced);
        _rebind = new InputRebindSession<TSchema>(request, handlers, _timeMs);
    }

    /// <summary>Cancels the active session.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CancelRebind()
    {
        ThrowIfDisposed();
        _rebind?.Cancel(RebindCancelReason.Manual);
        _rebind = null;
    }

    /// <summary>Offers one actuated control to the active session.</summary>
    /// <param name="path">Candidate control address.</param>
    /// <param name="magnitude">Actuation magnitude.</param>
    /// <returns><see langword="true"/> when a session completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OfferRebindCandidate(in InputControlPath path, float magnitude)
    {
        ThrowIfDisposed();
        if (_rebind is null || !_rebind.IsActive)
        {
            return false;
        }

        bool completed = _rebind.TryOffer(path, magnitude, _timeMs);
        if (completed)
        {
            _rebind = null;
        }

        return completed;
    }

    /// <summary>Reads current bindings for an action.</summary>
    /// <param name="contextId">Context identity.</param>
    /// <param name="action">Action value.</param>
    public InputBinding[] GetBindings(ContextId contextId, TSchema action)
    {
        ThrowIfDisposed();
        ContextEntry? context = FindContext(contextId);
        if (context is null)
        {
            ThrowHelper.ThrowContextError("input.invalid-context", "No context with the given id is registered.");
        }

        return (InputBinding[])context.GetBindings(ToIndex(action)).Clone();
    }

    /// <summary>Restores default bindings for one action or all actions.</summary>
    /// <param name="contextId">Context identity.</param>
    /// <param name="action">Action value, or every action when null.</param>
    /// <returns><see langword="true"/> when the context exists.</returns>
    public bool ResetBindings(ContextId contextId, TSchema? action = null)
    {
        ThrowIfDisposed();
        ContextEntry? context = FindContext(contextId);
        if (context is null)
        {
            return false;
        }

        if (action.HasValue)
        {
            context.ResetBindings(ToIndex(action.Value));
        }
        else
        {
            context.ResetAllBindings();
        }

        return true;
    }

    /// <summary>Patches one control path inside a binding.</summary>
    /// <param name="contextId">Context identity.</param>
    /// <param name="action">Action value.</param>
    /// <param name="index">Binding index.</param>
    /// <param name="slot">Slot name: control, negative, positive, up, down, left, right, x or y.</param>
    /// <param name="path">New control address.</param>
    /// <returns><see langword="true"/> when patched.</returns>
    public bool TryPatchBinding(ContextId contextId, TSchema action, int index, string slot, in InputControlPath path)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ContextEntry? context = FindContext(contextId);
        if (context is null)
        {
            return false;
        }

        int actionIndex = ToIndex(action);
        InputBinding[] current = context.GetBindings(actionIndex);
        if ((uint)index >= (uint)current.Length)
        {
            return false;
        }

        if (!TryPatch(current[index], slot, path, out InputBinding patched))
        {
            return false;
        }

        current[index] = patched;
        context.SetBindings(actionIndex, current);
        return true;
    }

    /// <summary>Rebuilds a binding with one replaced path.</summary>
    /// <param name="binding">Binding to patch.</param>
    /// <param name="slot">Slot name.</param>
    /// <param name="path">New control address.</param>
    /// <param name="patched">Patched binding.</param>
    /// <returns><see langword="true"/> when the slot fits the binding kind.</returns>
    private static bool TryPatch(in InputBinding binding, string slot, in InputControlPath path, out InputBinding patched)
    {
        patched = binding;
        switch (binding.Kind)
        {
            case BindingKind.Control when slot.Equals("control", StringComparison.OrdinalIgnoreCase):
                patched = WithPaths(binding, path, binding.Path1, binding.Path2, binding.Path3);
                return true;
            case BindingKind.Axis when slot.Equals("negative", StringComparison.OrdinalIgnoreCase):
                patched = WithPaths(binding, path, binding.Path1, binding.Path2, binding.Path3);
                return true;
            case BindingKind.Axis when slot.Equals("positive", StringComparison.OrdinalIgnoreCase):
                patched = WithPaths(binding, binding.Path0, path, binding.Path2, binding.Path3);
                return true;
            case BindingKind.Vector2Directional when slot.Equals("up", StringComparison.OrdinalIgnoreCase):
                patched = WithPaths(binding, path, binding.Path1, binding.Path2, binding.Path3);
                return true;
            case BindingKind.Vector2Directional when slot.Equals("down", StringComparison.OrdinalIgnoreCase):
                patched = WithPaths(binding, binding.Path0, path, binding.Path2, binding.Path3);
                return true;
            case BindingKind.Vector2Directional when slot.Equals("left", StringComparison.OrdinalIgnoreCase):
                patched = WithPaths(binding, binding.Path0, binding.Path1, path, binding.Path3);
                return true;
            case BindingKind.Vector2Directional when slot.Equals("right", StringComparison.OrdinalIgnoreCase):
                patched = WithPaths(binding, binding.Path0, binding.Path1, binding.Path2, path);
                return true;
            case BindingKind.DualAxis when slot.Equals("x", StringComparison.OrdinalIgnoreCase):
                patched = WithPaths(binding, path, binding.Path1, binding.Path2, binding.Path3);
                return true;
            case BindingKind.DualAxis when slot.Equals("y", StringComparison.OrdinalIgnoreCase):
                patched = WithPaths(binding, binding.Path0, path, binding.Path2, binding.Path3);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Rebuilds a binding with replaced paths.</summary>
    /// <param name="binding">Source binding.</param>
    /// <param name="p0">First control.</param>
    /// <param name="p1">Second control.</param>
    /// <param name="p2">Third control.</param>
    /// <param name="p3">Fourth control.</param>
    private static InputBinding WithPaths(
        in InputBinding binding,
        in InputControlPath p0,
        in InputControlPath p1,
        in InputControlPath p2,
        in InputControlPath p3) =>
        new(binding.Kind, p0, p1, p2, p3,
            binding.Modifiers, binding.ExactModifiers, binding.Consume,
            binding.Scale, binding.Invert, binding.Deadzone, binding.Normalize,
            binding.Processor0, binding.Processor1);

    /// <summary>Replaces one binding slot in a context.</summary>    /// <param name="contextId">Context identity.</param>
    /// <param name="action">Action value.</param>
    /// <param name="slot">Binding slot index.</param>
    /// <param name="binding">New binding.</param>
    /// <returns><see langword="true"/> when the context and action exist.</returns>
    public bool TrySetBinding(ContextId contextId, TSchema action, SlotIndex slot, in InputBinding binding)
    {
        ThrowIfDisposed();
        ContextEntry? context = FindContext(contextId);
        if (context is null)
        {
            return false;
        }

        int index = ToIndex(action);
        InputBinding[] current = context.GetBindings(index);
        if (slot.Value < 0)
        {
            return false;
        }

        if (slot.Value < current.Length)
        {
            current[slot.Value] = binding;
            context.SetBindings(index, current);
            return true;
        }

        if (slot.Value == current.Length)
        {
            var grown = new InputBinding[current.Length + 1];
            Array.Copy(current, grown, current.Length);
            grown[current.Length] = binding;
            context.SetBindings(index, grown);
            return true;
        }

        return false;
    }

    /// <summary>Advances engine time and polls the rebind timeout.</summary>
    /// <param name="deltaMs">Frame time in milliseconds.</param>
    private void AdvanceRebind(float deltaMs)
    {
        _timeMs += deltaMs;
        if (_rebind is not null)
        {
            if (!_rebind.IsActive)
            {
                _rebind = null;
            }
            else if (_rebind.Update(_timeMs))
            {
                _rebind = null;
            }
        }
    }

    /// <summary>Feeds actuated snapshot controls to the active session.</summary>
    private void FeedRebind()
    {
        if (_rebind is null || !_rebind.IsActive)
        {
            return;
        }

        foreach (Key key in _snapshot.Keys)
        {
            var path = new InputControlPath(DeviceKind.Keyboard, ControlType.Button, (ushort)key, 0);
            if (OfferRebindCandidate(path, 1.0f))
            {
                return;
            }
        }

        for (int button = 0; button < 16; button++)
        {
            if (((_snapshot.MouseButtons >> button) & 1) == 0)
            {
                continue;
            }

            var path = new InputControlPath(DeviceKind.Mouse, ControlType.Button, (ushort)button, 0);
            if (OfferRebindCandidate(path, 1.0f))
            {
                return;
            }
        }

        if (MathF.Abs(_snapshot.MouseDeltaX) >= 0.5f)
        {
            var path = new InputControlPath(DeviceKind.Mouse, ControlType.Move, 0, 0);
            if (OfferRebindCandidate(path, _snapshot.MouseDeltaX))
            {
                return;
            }
        }

        if (MathF.Abs(_snapshot.MouseDeltaY) >= 0.5f)
        {
            var path = new InputControlPath(DeviceKind.Mouse, ControlType.Move, 0, 1);
            if (OfferRebindCandidate(path, _snapshot.MouseDeltaY))
            {
                return;
            }
        }

        FeedRebindGamepads();
        FeedRebindTouch();
    }

    /// <summary>Feeds touch contacts to the active session.</summary>
    private void FeedRebindTouch()
    {
        if (_rebind is null || !_rebind.IsActive)
        {
            return;
        }

        bool any = false;
        for (int contact = 0; contact < 8; contact++)
        {
            if (!_snapshot.TouchContacts[contact].Active)
            {
                continue;
            }

            any = true;
            var path = new InputControlPath(DeviceKind.Touch, ControlType.Contact, (ushort)contact, 0);
            if (OfferRebindCandidate(path, 1.0f))
            {
                return;
            }
        }

        if (any)
        {
            var anyPath = new InputControlPath(DeviceKind.Touch, ControlType.Contact, 255, 0);
            OfferRebindCandidate(anyPath, 1.0f);
        }
    }

    /// <summary>Feeds gamepad controls to the active session.</summary>
    private void FeedRebindGamepads()
    {
        if (_rebind is null || !_rebind.IsActive)
        {
            return;
        }

        for (byte slot = 0; slot < 4; slot++)
        {
            uint buttons = slot switch
            {
                0 => _snapshot.GamepadButtons0,
                1 => _snapshot.GamepadButtons1,
                2 => _snapshot.GamepadButtons2,
                _ => _snapshot.GamepadButtons3,
            };

            for (int button = 0; button < 18; button++)
            {
                if (((buttons >> button) & 1u) == 0)
                {
                    continue;
                }

                var path = new InputControlPath(DeviceKind.Gamepad, ControlType.Button, (ushort)button, 0);
                if (OfferRebindCandidate(path, 1.0f))
                {
                    return;
                }
            }

            for (int axis = 0; axis < 6; axis++)
            {
                float value = _snapshot.GamepadAxes[(slot * 6) + axis];
                if (MathF.Abs(value) < 0.5f)
                {
                    continue;
                }

                var path = new InputControlPath(DeviceKind.Gamepad, ControlType.Axis, (ushort)axis, 0);
                if (OfferRebindCandidate(path, value))
                {
                    return;
                }
            }
        }
    }
}
