namespace Axrone.Input;

/// <summary>
/// Reads scalar control values from a debounced snapshot.
/// Buttons read as 0/1; pointer move and wheel read as frame deltas;
/// gamepad axes read from the slot row. Indices at or above 16 on gamepad
/// buttons are outside the snapshot bit range and read as zero.
/// </summary>
public static class InputControlReader
{
    /// <summary>Reads a control as a scalar.</summary>
    /// <param name="snapshot">Debounced frame state.</param>
    /// <param name="path">Canonical control address.</param>
    /// <param name="gamepadSlot">Gamepad slot override for gamepad paths.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadScalar(in InputSnapshot snapshot, in InputControlPath path, byte gamepadSlot = 0)
    {
        switch (path.Device)
        {
            case DeviceKind.Keyboard:
                return path.Type == ControlType.Button && snapshot.Keys.IsSet(path.Index) ? 1.0f : 0.0f;
            case DeviceKind.Mouse:
                return ReadMouse(snapshot, path);
            case DeviceKind.Gamepad:
                return ReadGamepad(in snapshot, path, gamepadSlot);
            case DeviceKind.Touch:
                return ReadTouch(in snapshot, path);
            case DeviceKind.None:
            default:
                return 0.0f;
        }
    }

    /// <summary>Reads touch contacts as 0/1.</summary>
    /// <param name="snapshot">Debounced frame state.</param>
    /// <param name="path">Canonical control address.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReadTouch(in InputSnapshot snapshot, in InputControlPath path)
    {
        switch (path.Type)
        {
            case ControlType.Contact:
                {
                    int slot = ResolveTouch(in snapshot, path.Index);
                    return slot >= 0 ? 1.0f : 0.0f;
                }

            case ControlType.Position:
                {
                    int slot = ResolveTouch(in snapshot, path.Index);
                    if (slot < 0)
                    {
                        return 0.0f;
                    }

                    TouchContact contact = snapshot.TouchContacts[slot];
                    return path.SubIndex == 0 ? contact.X : contact.Y;
                }

            case ControlType.Count:
                {
                    int count = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        if (snapshot.TouchContacts[i].Active)
                        {
                            count++;
                        }
                    }

                    return count;
                }

            case ControlType.Pinch:
                return path.SubIndex == 0 ? snapshot.TouchPinch : snapshot.TouchPinchDelta;

            default:
                return 0.0f;
        }
    }

    /// <summary>Resolves a touch selector to a slot, or -1.</summary>
    /// <param name="snapshot">Debounced frame state.</param>
    /// <param name="selector">255 any, 254 primary, else slot.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ResolveTouch(in InputSnapshot snapshot, ushort selector)
    {
        if (selector < 8)
        {
            return snapshot.TouchContacts[selector].Active ? selector : -1;
        }

        for (int i = 0; i < 8; i++)
        {
            if (snapshot.TouchContacts[i].Active)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Reads mouse controls.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReadMouse(in InputSnapshot snapshot, in InputControlPath path)
    {
        if (path.Type == ControlType.Button)
        {
            return path.Index < 16 && ((snapshot.MouseButtons >> path.Index) & 1) != 0 ? 1.0f : 0.0f;
        }

        if (path.Type == ControlType.Move)
        {
            return path.SubIndex == 0 ? snapshot.MouseDeltaX : snapshot.MouseDeltaY;
        }

        if (path.Type == ControlType.Wheel)
        {
            return path.SubIndex == 0 ? snapshot.ScrollDeltaX : snapshot.ScrollDeltaY;
        }

        if (path.Type == ControlType.Position)
        {
            return path.SubIndex == 0 ? snapshot.MouseX : snapshot.MouseY;
        }

        return 0.0f;
    }

    /// <summary>Reads gamepad controls in engine numbering.</summary>
    /// <param name="snapshot">Debounced frame state.</param>
    /// <param name="path">Canonical control address.</param>
    /// <param name="slot">Gamepad slot override for default paths.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReadGamepad(in InputSnapshot snapshot, in InputControlPath path, byte slot)
    {
        bool any = (path.Flags & 0xC0) == 0xC0;
        if ((path.Flags & 0x80) != 0 && !any)
        {
            slot = (byte)(path.Flags & 0x03);
        }

        if (path.Type == ControlType.Connected)
        {
            if (any)
            {
                return snapshot.GamepadConnectedMask != 0 ? 1.0f : 0.0f;
            }

            return slot < 4 && ((snapshot.GamepadConnectedMask >> slot) & 1u) != 0 ? 1.0f : 0.0f;
        }

        if (path.Type == ControlType.Button)
        {
            if (path.Index >= 32)
            {
                return 0.0f;
            }

            if (any)
            {
                return ((snapshot.GamepadButtons0 | snapshot.GamepadButtons1
                    | snapshot.GamepadButtons2 | snapshot.GamepadButtons3) >> path.Index & 1u) != 0 ? 1.0f : 0.0f;
            }

            if (slot >= 4)
            {
                return 0.0f;
            }

            uint buttons = slot switch
            {
                0 => snapshot.GamepadButtons0,
                1 => snapshot.GamepadButtons1,
                2 => snapshot.GamepadButtons2,
                _ => snapshot.GamepadButtons3,
            };
            return ((buttons >> path.Index) & 1u) != 0 ? 1.0f : 0.0f;
        }

        if (path.Type == ControlType.Axis)
        {
            if (path.Index >= 6)
            {
                return 0.0f;
            }

            if (any)
            {
                float best = 0.0f;
                for (byte s = 0; s < 4; s++)
                {
                    float value = snapshot.GamepadAxes[(s * 6) + path.Index];
                    if (MathF.Abs(value) > MathF.Abs(best))
                    {
                        best = value;
                    }
                }

                return best;
            }

            if (slot >= 4)
            {
                return 0.0f;
            }

            return snapshot.GamepadAxes[(slot * 6) + path.Index];
        }

        return 0.0f;
    }

    /// <summary>Maps Silk button codes to engine numbering.</summary>
    /// <param name="silk">Silk button code.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ushort MapSilkButton(ushort silk) => silk switch
    {
        6 => 8,
        7 => 9,
        8 => 16,
        9 => 10,
        10 => 11,
        11 => 12,
        12 => 15,
        13 => 13,
        14 => 14,
        _ => silk,
    };

    /// <summary>Reads held modifier keys as flags.</summary>    /// <param name="snapshot">Debounced frame state.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputModifiers ReadModifiers(in InputSnapshot snapshot)
    {
        InputModifiers modifiers = InputModifiers.None;
        if (snapshot.Keys.IsSet(340) || snapshot.Keys.IsSet(344))
        {
            modifiers |= InputModifiers.Shift;
        }

        if (snapshot.Keys.IsSet(341) || snapshot.Keys.IsSet(345))
        {
            modifiers |= InputModifiers.Control;
        }

        if (snapshot.Keys.IsSet(342) || snapshot.Keys.IsSet(346))
        {
            modifiers |= InputModifiers.Alt;
        }

        if (snapshot.Keys.IsSet(343) || snapshot.Keys.IsSet(347))
        {
            modifiers |= InputModifiers.Meta;
        }

        return modifiers;
    }

    /// <summary>Whether required modifiers satisfy the binding gate.</summary>
    /// <param name="held">Currently held modifiers.</param>
    /// <param name="required">Modifiers the binding demands.</param>
    /// <param name="exact">Whether held modifiers must match exactly.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ModifiersSatisfied(InputModifiers held, InputModifiers required, bool exact)
    {
        return exact ? held == required : (held & required) == required;
    }
}
