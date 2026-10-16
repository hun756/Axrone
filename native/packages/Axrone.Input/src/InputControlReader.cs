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
            case DeviceKind.None:
            default:
                return 0.0f;
        }
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

        return 0.0f;
    }

    /// <summary>Reads gamepad controls in engine numbering.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReadGamepad(in InputSnapshot snapshot, in InputControlPath path, byte slot)
    {
        if (slot >= 4)
        {
            return 0.0f;
        }

        if (path.Type == ControlType.Button)
        {
            if (path.Index >= 16)
            {
                return 0.0f;
            }

            ushort buttons = slot switch
            {
                0 => snapshot.GamepadButtons0,
                1 => snapshot.GamepadButtons1,
                2 => snapshot.GamepadButtons2,
                _ => snapshot.GamepadButtons3,
            };
            return ((buttons >> path.Index) & 1) != 0 ? 1.0f : 0.0f;
        }

        if (path.Type == ControlType.Axis)
        {
            if (path.Index >= 6)
            {
                return 0.0f;
            }

            return snapshot.GamepadAxes[(slot * 6) + path.Index];
        }

        return 0.0f;
    }
}
