namespace Axrone.Input.Android;

/// <summary>
/// Android <c>MotionEvent</c> constants mirrored from AOSP so the mapping
/// core builds without the Android workload.
/// </summary>
public static class AndroidMotionEvents
{
    /// <summary>First pointer down.</summary>
    public const int ActionDown = 0;

    /// <summary>Last pointer up.</summary>
    public const int ActionUp = 1;

    /// <summary>Pointer moved.</summary>
    public const int ActionMove = 2;

    /// <summary>Gesture cancelled.</summary>
    public const int ActionCancel = 3;

    /// <summary>Non-primary pointer down.</summary>
    public const int ActionPointerDown = 5;

    /// <summary>Non-primary pointer up.</summary>
    public const int ActionPointerUp = 6;

    /// <summary>Hover move.</summary>
    public const int ActionHoverMove = 7;

    /// <summary>Wheel/button scroll.</summary>
    public const int ActionScroll = 8;

    /// <summary>Action mask.</summary>
    public const int ActionMask = 0xFF;

    /// <summary>Pointer index mask.</summary>
    public const int PointerIndexMask = 0xFF00;

    /// <summary>Pointer index shift.</summary>
    public const int PointerIndexShift = 8;

    /// <summary>X axis.</summary>
    public const int AxisX = 0;

    /// <summary>Y axis.</summary>
    public const int AxisY = 1;

    /// <summary>Pressure axis.</summary>
    public const int AxisPressure = 2;

    /// <summary>Left stick is X/Y; right stick is Z/RZ on standard layouts.</summary>
    public const int AxisZ = 11;

    /// <summary>Right stick X alternate.</summary>
    public const int AxisRX = 12;

    /// <summary>Right stick Y alternate.</summary>
    public const int AxisRY = 13;

    /// <summary>Right stick Z.</summary>
    public const int AxisRZ = 14;

    /// <summary>DPad hat X.</summary>
    public const int AxisHatX = 15;

    /// <summary>DPad hat Y.</summary>
    public const int AxisHatY = 16;

    /// <summary>Left trigger, 0-1.</summary>
    public const int AxisLTrigger = 17;

    /// <summary>Right trigger, 0-1.</summary>
    public const int AxisRTrigger = 18;

    /// <summary>Unknown tool.</summary>
    public const int ToolUnknown = 0;

    /// <summary>Finger contact.</summary>
    public const int ToolFinger = 1;

    /// <summary>Stylus contact.</summary>
    public const int ToolStylus = 2;

    /// <summary>Mouse tool.</summary>
    public const int ToolMouse = 3;

    /// <summary>Eraser tool.</summary>
    public const int ToolEraser = 4;

    /// <summary>Keyboard source class.</summary>
    public const int SourceKeyboard = 0x101;

    /// <summary>DPad source class.</summary>
    public const int SourceDPad = 0x201;

    /// <summary>Gamepad source class.</summary>
    public const int SourceGamepad = 0x401;

    /// <summary>Touchscreen source class.</summary>
    public const int SourceTouchscreen = 0x1002;

    /// <summary>Mouse source class.</summary>
    public const int SourceMouse = 0x2002;

    /// <summary>Joystick source class.</summary>
    public const int SourceJoystick = 0x1000010;
}

/// <summary>
/// Decodes packed <c>MotionEvent</c> actions and classifies sources.
/// </summary>
public static class AndroidMotionMap
{
    /// <summary>Extracts the masked action.</summary>
    /// <param name="packedAction">Raw <c>getAction()</c> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Action(int packedAction) => packedAction & AndroidMotionEvents.ActionMask;

    /// <summary>Extracts the pointer index for pointer down/up actions.</summary>
    /// <param name="packedAction">Raw <c>getAction()</c> value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PointerIndex(int packedAction) =>
        (packedAction & AndroidMotionEvents.PointerIndexMask) >> AndroidMotionEvents.PointerIndexShift;

    /// <summary>Whether the source carries gamepad buttons.</summary>
    /// <param name="source">Event source bitmask.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGamepadSource(int source) =>
        (source & AndroidMotionEvents.SourceGamepad) != 0 || (source & AndroidMotionEvents.SourceJoystick) != 0;

    /// <summary>Whether the source is a touchscreen.</summary>
    /// <param name="source">Event source bitmask.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTouchSource(int source) =>
        (source & AndroidMotionEvents.SourceTouchscreen) == AndroidMotionEvents.SourceTouchscreen;

    /// <summary>Whether the tool is a touch contact (finger or stylus).</summary>
    /// <param name="toolType">Pointer tool type.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTouchTool(int toolType) =>
        toolType is AndroidMotionEvents.ToolFinger or AndroidMotionEvents.ToolStylus;
}
