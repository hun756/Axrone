namespace Axrone.Input.Android;

using Axrone.Input;

/// <summary>
/// Android gamepad button keycodes (<c>KEYCODE_BUTTON_*</c>) mirrored from AOSP.
/// </summary>
public static class AndroidGamepadButtons
{
    /// <summary>Bottom face.</summary>
    public const int A = 96;

    /// <summary>Right face.</summary>
    public const int B = 97;

    /// <summary>Left face.</summary>
    public const int X = 99;

    /// <summary>Top face.</summary>
    public const int Y = 100;

    /// <summary>Left shoulder.</summary>
    public const int L1 = 102;

    /// <summary>Right shoulder.</summary>
    public const int R1 = 103;

    /// <summary>Left trigger button.</summary>
    public const int L2 = 104;

    /// <summary>Right trigger button.</summary>
    public const int R2 = 105;

    /// <summary>Left stick press.</summary>
    public const int ThumbL = 106;

    /// <summary>Right stick press.</summary>
    public const int ThumbR = 107;

    /// <summary>Start.</summary>
    public const int Start = 108;

    /// <summary>Select.</summary>
    public const int Select = 109;

    /// <summary>Guide/mode.</summary>
    public const int Mode = 110;
}

/// <summary>
/// Maps Android gamepad buttons and axes to engine numbering.
/// </summary>
public static class AndroidGamepadMap
{
    /// <summary>Maps a gamepad button keycode to an engine button.</summary>
    /// <param name="keyCode">Android <c>KEYCODE_BUTTON_*</c> or DPad keycode.</param>
    /// <param name="button">Engine button index.</param>
    /// <returns><see langword="true"/> when mapped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryToButton(int keyCode, out ushort button)
    {
        button = keyCode switch
        {
            AndroidGamepadButtons.A => 0,
            AndroidGamepadButtons.B => 1,
            AndroidGamepadButtons.X => 2,
            AndroidGamepadButtons.Y => 3,
            AndroidGamepadButtons.L1 => 4,
            AndroidGamepadButtons.R1 => 5,
            AndroidGamepadButtons.L2 => 6,
            AndroidGamepadButtons.R2 => 7,
            AndroidGamepadButtons.Select => 8,
            AndroidGamepadButtons.Start => 9,
            AndroidGamepadButtons.ThumbL => 10,
            AndroidGamepadButtons.ThumbR => 11,
            AndroidKeycodes.DPadUp => 12,
            AndroidKeycodes.DPadDown => 13,
            AndroidKeycodes.DPadLeft => 14,
            AndroidKeycodes.DPadRight => 15,
            AndroidGamepadButtons.Mode => 16,
            _ => ushort.MaxValue,
        };
        return button != ushort.MaxValue;
    }

    /// <summary>Maps a motion axis to an engine axis.</summary>
    /// <param name="axis">Android <c>AXIS_*</c> constant.</param>
    /// <param name="engineAxis">Engine axis index.</param>
    /// <returns><see langword="true"/> when mapped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryToAxis(int axis, out ushort engineAxis)
    {
        engineAxis = axis switch
        {
            AndroidMotionEvents.AxisX => 0,
            AndroidMotionEvents.AxisY => 1,
            AndroidMotionEvents.AxisZ => 2,
            AndroidMotionEvents.AxisRX => 2,
            AndroidMotionEvents.AxisRY => 3,
            AndroidMotionEvents.AxisRZ => 3,
            AndroidMotionEvents.AxisLTrigger => 4,
            AndroidMotionEvents.AxisRTrigger => 5,
            _ => ushort.MaxValue,
        };
        return engineAxis != ushort.MaxValue;
    }
}
