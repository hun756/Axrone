namespace Axrone.Input;

/// <summary>
/// Source device of a control path.
/// </summary>
public enum DeviceKind : byte
{
    /// <summary>No device.</summary>
    None = 0,

    /// <summary>Keyboard.</summary>
    Keyboard = 1,

    /// <summary>Mouse.</summary>
    Mouse = 2,

    /// <summary>Touch contact.</summary>
    Touch = 3,

    /// <summary>Gamepad.</summary>
    Gamepad = 4,
}

/// <summary>
/// Control shape on a device.
/// </summary>
public enum ControlType : byte
{
    /// <summary>No control.</summary>
    None = 0,

    /// <summary>Pressable button.</summary>
    Button = 1,

    /// <summary>Scalar axis.</summary>
    Axis = 2,

    /// <summary>Pointer movement.</summary>
    Move = 3,

    /// <summary>Wheel movement.</summary>
    Wheel = 4,

    /// <summary>Touch contact.</summary>
    Contact = 5,

    /// <summary>Absolute position.</summary>
    Position = 6,

    /// <summary>Pinch distance or delta.</summary>
    Pinch = 7,

    /// <summary>Active contact count.</summary>
    Count = 8,

    /// <summary>Gamepad connection.</summary>
    Connected = 9,
}

/// <summary>
/// Value shape of an action.
/// </summary>
public enum ActionKind : byte
{
    /// <summary>Boolean button.</summary>
    Button = 0,

    /// <summary>Scalar axis.</summary>
    Axis = 1,

    /// <summary>Two-dimensional vector.</summary>
    Vector2 = 2,
}

/// <summary>
/// Lifecycle phase of an action within a frame.
/// </summary>
public enum InputActionPhase : byte
{
    /// <summary>No transition.</summary>
    None = 0,

    /// <summary>Actuation started.</summary>
    Started = 1,

    /// <summary>Actuation confirmed.</summary>
    Performed = 2,

    /// <summary>Ongoing value changed.</summary>
    Changed = 3,

    /// <summary>Actuation released.</summary>
    Canceled = 4,
}

/// <summary>
/// Button interaction gesture.
/// </summary>
public enum InputInteractionType : byte
{
    /// <summary>Simple press.</summary>
    Press = 0,

    /// <summary>Sustained hold.</summary>
    Hold = 1,

    /// <summary>Quick tap.</summary>
    Tap = 2,

    /// <summary>Repeated taps.</summary>
    MultiTap = 3,

    /// <summary>Press with auto-repeat.</summary>
    Repeat = 4,
}

/// <summary>
/// Hold interaction emission mode.
/// </summary>
public enum HoldMode : byte
{
    /// <summary>Emit once per hold.</summary>
    Once = 0,

    /// <summary>Emit every frame while held.</summary>
    Continuous = 1,
}

/// <summary>
/// How multiple axis contributors combine.
/// </summary>
public enum AxisCombineMode : byte
{
    /// <summary>Last writer wins.</summary>
    Latest = 0,

    /// <summary>Sum contributors.</summary>
    Sum = 1,

    /// <summary>Largest magnitude wins.</summary>
    MaxAbs = 2,
}

/// <summary>
/// How multiple vector contributors combine.
/// </summary>
public enum Vector2CombineMode : byte
{
    /// <summary>Last writer wins.</summary>
    Latest = 0,

    /// <summary>Sum contributors.</summary>
    Sum = 1,
}

/// <summary>
/// Binding composition shape.
/// </summary>
public enum BindingKind : byte
{
    /// <summary>Single control.</summary>
    Control = 0,

    /// <summary>Negative/positive control pair.</summary>
    Axis = 1,

    /// <summary>Up/down/left/right directional pad.</summary>
    Vector2Directional = 2,

    /// <summary>Two independent axes.</summary>
    DualAxis = 3,
}

/// <summary>
/// Whether a context consumes the controls it reads.
/// </summary>
public enum ContextCaptureMode : byte
{
    /// <summary>Controls stay visible to lower-priority contexts.</summary>
    None = 0,

    /// <summary>Read controls are consumed.</summary>
    Used = 1,
}

/// <summary>
/// Why a rebind session ended without a binding.
/// </summary>
public enum RebindCancelReason : byte
{
    /// <summary>Cancelled by the caller.</summary>
    Manual = 0,

    /// <summary>Timed out.</summary>
    Timeout = 1,

    /// <summary>Engine disposed.</summary>
    Disposed = 2,

    /// <summary>Replaced by a newer session.</summary>
    Replaced = 3,

    /// <summary>Completed with a binding.</summary>
    Completed = 4,
}

/// <summary>
/// Source event discriminant for engine dispatch.
/// </summary>
public enum InputSourceEventType : byte
{
    /// <summary>Keyboard key.</summary>
    Keyboard = 0,

    /// <summary>Mouse button.</summary>
    MouseButton = 1,

    /// <summary>Pointer movement.</summary>
    MouseMove = 2,

    /// <summary>Wheel movement.</summary>
    MouseWheel = 3,

    /// <summary>Touch contact.</summary>
    Touch = 4,

    /// <summary>Gamepad full-state frame.</summary>
    Gamepad = 5,

    /// <summary>Window focus.</summary>
    Focus = 6,

    /// <summary>Typed text.</summary>
    Text = 7,

    /// <summary>IME composition.</summary>
    Composition = 8,
}

/// <summary>
/// Touch contact phase.
/// </summary>
public enum TouchPhase : byte
{
    /// <summary>Contact started.</summary>
    Start = 0,

    /// <summary>Contact moved.</summary>
    Move = 1,

    /// <summary>Contact ended.</summary>
    End = 2,

    /// <summary>Contact cancelled.</summary>
    Cancel = 3,
}

/// <summary>
/// IME composition phase.
/// </summary>
public enum CompositionPhase : byte
{
    /// <summary>Composition started.</summary>
    Start = 0,

    /// <summary>Composition updated.</summary>
    Update = 1,

    /// <summary>Composition ended.</summary>
    End = 2,
}

/// <summary>
/// Value processor shape.
/// </summary>
public enum InputProcessorKind : byte
{
    /// <summary>No processing.</summary>
    None = 0,

    /// <summary>Multiply by a factor.</summary>
    Scale = 1,

    /// <summary>Negate.</summary>
    Invert = 2,

    /// <summary>Clamp to a range.</summary>
    Clamp = 3,

    /// <summary>Zero below a threshold.</summary>
    Deadzone = 4,

    /// <summary>Power curve.</summary>
    Curve = 5,

    /// <summary>Per-component scale.</summary>
    ScaleVector2 = 6,

    /// <summary>Per-component negate.</summary>
    InvertVector2 = 7,

    /// <summary>Normalize to unit length.</summary>
    NormalizeVector2 = 8,

    /// <summary>Clamp the magnitude.</summary>
    ClampMagnitude = 9,
}

/// <summary>
/// Gamepad button index, engine numbering.
/// </summary>
public enum GamepadButton : byte
{
    /// <summary>Bottom face.</summary>
    South = 0,

    /// <summary>Right face.</summary>
    East = 1,

    /// <summary>Left face.</summary>
    West = 2,

    /// <summary>Top face.</summary>
    North = 3,

    /// <summary>Left shoulder.</summary>
    LeftShoulder = 4,

    /// <summary>Right shoulder.</summary>
    RightShoulder = 5,

    /// <summary>Left trigger button.</summary>
    LeftTriggerButton = 6,

    /// <summary>Right trigger button.</summary>
    RightTriggerButton = 7,

    /// <summary>Select.</summary>
    Select = 8,

    /// <summary>Start.</summary>
    Start = 9,

    /// <summary>Left stick press.</summary>
    LeftStickPress = 10,

    /// <summary>Right stick press.</summary>
    RightStickPress = 11,

    /// <summary>DPad up.</summary>
    DPadUp = 12,

    /// <summary>DPad down.</summary>
    DPadDown = 13,

    /// <summary>DPad left.</summary>
    DPadLeft = 14,

    /// <summary>DPad right.</summary>
    DPadRight = 15,

    /// <summary>Guide.</summary>
    Guide = 16,

    /// <summary>Touchpad press.</summary>
    Touchpad = 17,

    /// <summary>Button count.</summary>
    Count = 18,
}

/// <summary>
/// Gamepad axis index, engine numbering.
/// </summary>
public enum GamepadAxis : byte
{
    /// <summary>Left stick X.</summary>
    LeftStickX = 0,

    /// <summary>Left stick Y.</summary>
    LeftStickY = 1,

    /// <summary>Right stick X.</summary>
    RightStickX = 2,

    /// <summary>Right stick Y.</summary>
    RightStickY = 3,

    /// <summary>Left trigger.</summary>
    LeftTrigger = 4,

    /// <summary>Right trigger.</summary>
    RightTrigger = 5,

    /// <summary>Axis count.</summary>
    Count = 6,
}
