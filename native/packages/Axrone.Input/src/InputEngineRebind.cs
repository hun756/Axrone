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

    /// <summary>Replaces one binding slot in a context.</summary>
    /// <param name="contextId">Context identity.</param>
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
            ushort buttons = slot switch
            {
                0 => _snapshot.GamepadButtons0,
                1 => _snapshot.GamepadButtons1,
                2 => _snapshot.GamepadButtons2,
                _ => _snapshot.GamepadButtons3,
            };

            for (int button = 0; button < 16; button++)
            {
                if (((buttons >> button) & 1) == 0)
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
