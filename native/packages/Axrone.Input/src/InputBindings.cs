namespace Axrone.Input;

/// <summary>
/// Canonical control address: device, shape, index and sub-channel.
/// Parses paths like "keyboard/space", "mouse/button/0", "mouse/move/x",
/// "gamepad/left-stick/x" and "touch/contact/any".
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 2, Size = 8)]
public readonly record struct InputControlPath(
    DeviceKind Device,
    ControlType Type,
    ushort Index,
    byte SubIndex,
    byte Flags = 0)
{
    /// <summary>Parses a control path without allocating.</summary>
    /// <returns><see langword="true"/> when the path is well-formed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool TryParse(ReadOnlySpan<char> path, out InputControlPath result)
    {
        result = default;
        if (path.IsEmpty)
        {
            return false;
        }

        if (path.StartsWith("keyboard/", StringComparison.OrdinalIgnoreCase))
        {
            ReadOnlySpan<char> codeSpan = path.Slice(9);
            if (ushort.TryParse(codeSpan, out ushort code))
            {
                result = new InputControlPath(DeviceKind.Keyboard, ControlType.Button, code, 0);
                return true;
            }

            ushort mapped = MapKeyNameToCode(codeSpan);
            if (mapped != 0)
            {
                result = new InputControlPath(DeviceKind.Keyboard, ControlType.Button, mapped, 0);
                return true;
            }

            return false;
        }

        if (path.StartsWith("mouse/", StringComparison.OrdinalIgnoreCase))
        {
            ReadOnlySpan<char> sub = path.Slice(6);
            if (sub.StartsWith("button/", StringComparison.OrdinalIgnoreCase))
            {
                if (ushort.TryParse(sub.Slice(7), out ushort button))
                {
                    result = new InputControlPath(DeviceKind.Mouse, ControlType.Button, button, 0);
                    return true;
                }
            }
            else if (sub.Equals("move/x", StringComparison.OrdinalIgnoreCase))
            {
                result = new InputControlPath(DeviceKind.Mouse, ControlType.Move, 0, 0);
                return true;
            }
            else if (sub.Equals("move/y", StringComparison.OrdinalIgnoreCase))
            {
                result = new InputControlPath(DeviceKind.Mouse, ControlType.Move, 0, 1);
                return true;
            }
            else if (sub.Equals("wheel/x", StringComparison.OrdinalIgnoreCase))
            {
                result = new InputControlPath(DeviceKind.Mouse, ControlType.Wheel, 0, 0);
                return true;
            }
            else if (sub.Equals("wheel/y", StringComparison.OrdinalIgnoreCase))
            {
                result = new InputControlPath(DeviceKind.Mouse, ControlType.Wheel, 0, 1);
                return true;
            }

            return false;
        }

        if (path.StartsWith("touch/", StringComparison.OrdinalIgnoreCase))
        {
            ReadOnlySpan<char> sub = path.Slice(6);
            if (sub.StartsWith("contact/", StringComparison.OrdinalIgnoreCase))
            {
                ReadOnlySpan<char> idSpan = sub.Slice(8);
                if (idSpan.Equals("any", StringComparison.OrdinalIgnoreCase))
                {
                    result = new InputControlPath(DeviceKind.Touch, ControlType.Contact, 255, 0);
                    return true;
                }

                if (ushort.TryParse(idSpan, out ushort contactId))
                {
                    result = new InputControlPath(DeviceKind.Touch, ControlType.Contact, contactId, 0);
                    return true;
                }
            }

            return false;
        }

        if (path.StartsWith("gamepad/", StringComparison.OrdinalIgnoreCase))
        {
            ReadOnlySpan<char> sub = path.Slice(8);
            if (sub.Equals("south", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.South); return true; }
            if (sub.Equals("east", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.East); return true; }
            if (sub.Equals("west", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.West); return true; }
            if (sub.Equals("north", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.North); return true; }
            if (sub.Equals("left-shoulder", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.LeftShoulder); return true; }
            if (sub.Equals("right-shoulder", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.RightShoulder); return true; }
            if (sub.Equals("left-trigger-button", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.LeftTriggerButton); return true; }
            if (sub.Equals("right-trigger-button", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.RightTriggerButton); return true; }
            if (sub.Equals("select", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.Select); return true; }
            if (sub.Equals("start", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.Start); return true; }
            if (sub.Equals("left-stick-press", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.LeftStickPress); return true; }
            if (sub.Equals("right-stick-press", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.RightStickPress); return true; }
            if (sub.Equals("dpad-up", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.DPadUp); return true; }
            if (sub.Equals("dpad-down", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.DPadDown); return true; }
            if (sub.Equals("dpad-left", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.DPadLeft); return true; }
            if (sub.Equals("dpad-right", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.DPadRight); return true; }
            if (sub.Equals("guide", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.Guide); return true; }
            if (sub.Equals("touchpad", StringComparison.OrdinalIgnoreCase)) { result = Button(GamepadButton.Touchpad); return true; }

            if (sub.Equals("left-stick/x", StringComparison.OrdinalIgnoreCase)) { result = Axis(GamepadAxis.LeftStickX, 0); return true; }
            if (sub.Equals("left-stick/y", StringComparison.OrdinalIgnoreCase)) { result = Axis(GamepadAxis.LeftStickY, 1); return true; }
            if (sub.Equals("right-stick/x", StringComparison.OrdinalIgnoreCase)) { result = Axis(GamepadAxis.RightStickX, 0); return true; }
            if (sub.Equals("right-stick/y", StringComparison.OrdinalIgnoreCase)) { result = Axis(GamepadAxis.RightStickY, 1); return true; }
            if (sub.Equals("left-trigger", StringComparison.OrdinalIgnoreCase)) { result = Axis(GamepadAxis.LeftTrigger, 0); return true; }
            if (sub.Equals("right-trigger", StringComparison.OrdinalIgnoreCase)) { result = Axis(GamepadAxis.RightTrigger, 0); return true; }
        }

        return false;

        static InputControlPath Button(GamepadButton name) =>
            new(DeviceKind.Gamepad, ControlType.Button, (ushort)name, 0);

        static InputControlPath Axis(GamepadAxis axis, byte channel) =>
            new(DeviceKind.Gamepad, ControlType.Axis, (ushort)axis, channel);
    }

    /// <summary>Maps a key name to its code; zero when unknown.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort MapKeyNameToCode(ReadOnlySpan<char> span)
    {
        if (span.Length == 1)
        {
            char c = char.ToUpperInvariant(span[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                return (ushort)c;
            }
        }

        if (span.Equals("space", StringComparison.OrdinalIgnoreCase))
        {
            return 32;
        }

        if (span.Equals("enter", StringComparison.OrdinalIgnoreCase) || span.Equals("return", StringComparison.OrdinalIgnoreCase))
        {
            return 13;
        }

        if (span.Equals("escape", StringComparison.OrdinalIgnoreCase))
        {
            return 27;
        }

        if (span.Equals("tab", StringComparison.OrdinalIgnoreCase))
        {
            return 9;
        }

        if (span.Equals("backspace", StringComparison.OrdinalIgnoreCase))
        {
            return 8;
        }

        if (span.Equals("shift", StringComparison.OrdinalIgnoreCase))
        {
            return 16;
        }

        if (span.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || span.Equals("control", StringComparison.OrdinalIgnoreCase))
        {
            return 17;
        }

        if (span.Equals("alt", StringComparison.OrdinalIgnoreCase))
        {
            return 18;
        }

        if (span.Equals("up", StringComparison.OrdinalIgnoreCase))
        {
            return 38;
        }

        if (span.Equals("down", StringComparison.OrdinalIgnoreCase))
        {
            return 40;
        }

        if (span.Equals("left", StringComparison.OrdinalIgnoreCase))
        {
            return 37;
        }

        if (span.Equals("right", StringComparison.OrdinalIgnoreCase))
        {
            return 39;
        }

        return 0;
    }
}

/// <summary>
/// Value processor configuration: one shape plus two parameters.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ProcessorConfiguration(InputProcessorKind Kind, float Param1 = 0f, float Param2 = 0f, bool Flag = false)
{
    /// <summary>Multiplies by a factor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessorConfiguration Scale(float factor) => new(InputProcessorKind.Scale, factor);

    /// <summary>Negates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessorConfiguration Invert() => new(InputProcessorKind.Invert);

    /// <summary>Clamps to a range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessorConfiguration Clamp(float min, float max) => new(InputProcessorKind.Clamp, min, max);

    /// <summary>Zeroes below a threshold.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessorConfiguration Deadzone(float threshold) => new(InputProcessorKind.Deadzone, threshold);

    /// <summary>Applies a power curve.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessorConfiguration Curve(float exponent, bool keepSign = true) => new(InputProcessorKind.Curve, exponent, 0f, keepSign);

    /// <summary>Scales vector components.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessorConfiguration ScaleVector2(float x, float y) => new(InputProcessorKind.ScaleVector2, x, y);

    /// <summary>Negates vector components.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessorConfiguration InvertVector2(bool invertX, bool invertY) => new(InputProcessorKind.InvertVector2, invertX ? 1f : 0f, invertY ? 1f : 0f);

    /// <summary>Normalizes to unit length.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessorConfiguration NormalizeVector2() => new(InputProcessorKind.NormalizeVector2);

    /// <summary>Clamps the vector magnitude.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProcessorConfiguration ClampMagnitude(float min, float max) => new(InputProcessorKind.ClampMagnitude, min, max);
}

/// <summary>
/// Button gesture tuning.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct InteractionSettings(
    InputInteractionType Type = InputInteractionType.Press,
    float DurationMs = 400f,
    HoldMode Mode = HoldMode.Once,
    int TapCount = 2,
    float MaxDelayMs = 300f,
    float IntervalMs = 60f,
    float TapDurationMs = 250f);

/// <summary>
/// One action binding: up to four controls composed into a value, with
/// modifiers, consumption, scaling and processors.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct InputBinding
{
    /// <summary>Composition shape.</summary>
    public readonly BindingKind Kind;

    /// <summary>First control.</summary>
    public readonly InputControlPath Path0;

    /// <summary>Second control.</summary>
    public readonly InputControlPath Path1;

    /// <summary>Third control.</summary>
    public readonly InputControlPath Path2;

    /// <summary>Fourth control.</summary>
    public readonly InputControlPath Path3;

    /// <summary>Required modifiers.</summary>
    public readonly InputModifiers Modifiers;

    /// <summary>Whether modifiers must match exactly.</summary>
    public readonly bool ExactModifiers;

    /// <summary>Whether read controls are consumed.</summary>
    public readonly bool Consume;

    /// <summary>Value scale.</summary>
    public readonly float Scale;

    /// <summary>Whether the value is negated.</summary>
    public readonly bool Invert;

    /// <summary>Deadzone threshold.</summary>
    public readonly float Deadzone;

    /// <summary>Whether vectors normalize.</summary>
    public readonly bool Normalize;

    /// <summary>First processor.</summary>
    public readonly ProcessorConfiguration Processor0;

    /// <summary>Second processor.</summary>
    public readonly ProcessorConfiguration Processor1;

    /// <summary>Creates a binding.</summary>
    public InputBinding(
        BindingKind kind,
        in InputControlPath p0,
        in InputControlPath p1 = default,
        in InputControlPath p2 = default,
        in InputControlPath p3 = default,
        InputModifiers modifiers = InputModifiers.None,
        bool exactModifiers = false,
        bool consume = false,
        float scale = 1.0f,
        bool invert = false,
        float deadzone = 0.0f,
        bool normalize = false,
        in ProcessorConfiguration proc0 = default,
        in ProcessorConfiguration proc1 = default)
    {
        Kind = kind;
        Path0 = p0;
        Path1 = p1;
        Path2 = p2;
        Path3 = p3;
        Modifiers = modifiers;
        ExactModifiers = exactModifiers;
        Consume = consume;
        Scale = scale;
        Invert = invert;
        Deadzone = deadzone;
        Normalize = normalize;
        Processor0 = proc0;
        Processor1 = proc1;
    }

    /// <summary>Binds one control.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputBinding Control(in InputControlPath path, InputModifiers modifiers = InputModifiers.None, bool consume = false) =>
        new(BindingKind.Control, path, modifiers: modifiers, consume: consume);

    /// <summary>Binds a negative/positive pair.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputBinding Axis(in InputControlPath negative, in InputControlPath positive, InputModifiers modifiers = InputModifiers.None, bool consume = false) =>
        new(BindingKind.Axis, negative, positive, modifiers: modifiers, consume: consume);

    /// <summary>Binds four directional controls.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputBinding Vector2Directional(in InputControlPath up, in InputControlPath down, in InputControlPath left, in InputControlPath right, InputModifiers modifiers = InputModifiers.None, bool normalize = false, bool consume = false) =>
        new(BindingKind.Vector2Directional, up, down, left, right, modifiers: modifiers, normalize: normalize, consume: consume);

    /// <summary>Binds two independent axes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputBinding DualAxis(in InputControlPath x, in InputControlPath y, InputModifiers modifiers = InputModifiers.None, bool normalize = false, bool consume = false) =>
        new(BindingKind.DualAxis, x, y, modifiers: modifiers, normalize: normalize, consume: consume);
}

/// <summary>
/// Read action value: boolean, scalar or vector.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 16)]
public readonly struct InputActionValue
{
    /// <summary>Value shape.</summary>
    [FieldOffset(0)]
    public readonly ActionKind Kind;

    /// <summary>Boolean payload.</summary>
    [FieldOffset(4)]
    public readonly bool BoolValue;

    /// <summary>Scalar payload.</summary>
    [FieldOffset(4)]
    public readonly float FloatValue;

    /// <summary>Vector payload.</summary>
    [FieldOffset(8)]
    public readonly Vector2 Vector2Value;

    /// <summary>Wraps a boolean.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InputActionValue(bool value)
    {
        FloatValue = 0f;
        Vector2Value = default;
        Kind = ActionKind.Button;
        BoolValue = value;
    }

    /// <summary>Wraps a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InputActionValue(float value)
    {
        BoolValue = false;
        Vector2Value = default;
        Kind = ActionKind.Axis;
        FloatValue = value;
    }

    /// <summary>Wraps a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InputActionValue(Vector2 value)
    {
        BoolValue = false;
        FloatValue = 0f;
        Kind = ActionKind.Vector2;
        Vector2Value = value;
    }
}

/// <summary>
/// Committed button state with gesture flags.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct InputButtonState
{
    /// <summary>Actuated.</summary>
    public bool Value;

    /// <summary>Previous actuation.</summary>
    public bool PreviousValue;

    /// <summary>Raw pre-threshold value.</summary>
    public float RawValue;

    /// <summary>Transitioned down this frame.</summary>
    public bool Pressed;

    /// <summary>Transitioned up this frame.</summary>
    public bool Released;

    /// <summary>Held time in milliseconds.</summary>
    public float HeldDurationMs;

    /// <summary>Consecutive tap count.</summary>
    public int TapSequenceCount;

    /// <summary>Auto-repeat count.</summary>
    public int RepeatCount;

    /// <summary>Hold gesture fired.</summary>
    public bool HoldTriggered;

    /// <summary>Tap gesture fired.</summary>
    public bool TapTriggered;

    /// <summary>Multi-tap gesture fired.</summary>
    public bool MultiTapTriggered;

    /// <summary>Repeat tick fired.</summary>
    public bool RepeatTriggered;

    /// <summary>Current phase.</summary>
    public InputActionPhase Phase;

    /// <summary>Time since the last tap.</summary>
    public float TimeSinceLastTapMs;
}

/// <summary>
/// Committed axis state.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct InputAxisState
{
    /// <summary>Current value.</summary>
    public float Value;

    /// <summary>Previous value.</summary>
    public float PreviousValue;

    /// <summary>Frame delta.</summary>
    public float Delta;

    /// <summary>Current phase.</summary>
    public InputActionPhase Phase;
}

/// <summary>
/// Committed vector state.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct InputVector2State
{
    /// <summary>Current value.</summary>
    public Vector2 Value;

    /// <summary>Previous value.</summary>
    public Vector2 PreviousValue;

    /// <summary>Frame delta.</summary>
    public Vector2 Delta;

    /// <summary>Current magnitude.</summary>
    public float Magnitude;

    /// <summary>Previous magnitude.</summary>
    public float PreviousMagnitude;

    /// <summary>Current phase.</summary>
    public InputActionPhase Phase;
}
