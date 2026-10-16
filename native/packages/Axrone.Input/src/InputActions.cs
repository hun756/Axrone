namespace Axrone.Input;

/// <summary>
/// Engine dispatch event: keyboard, pointer, touch, gamepad, focus, text or composition.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct InputSourceEvent
{
    /// <summary>Event discriminant.</summary>
    public readonly InputSourceEventType Type;

    /// <summary>Key code.</summary>
    public readonly ushort KeyCode;

    /// <summary>Device slot.</summary>
    public readonly byte DeviceIndex;

    /// <summary>Mouse button.</summary>
    public readonly byte Button;

    /// <summary>Pressed (or focused).</summary>
    public readonly bool Pressed;

    /// <summary>Key auto-repeat.</summary>
    public readonly bool Repeat;

    /// <summary>Touch phase.</summary>
    public readonly TouchPhase TouchPhase;

    /// <summary>Composition phase.</summary>
    public readonly CompositionPhase CompositionPhase;

    /// <summary>Float payload zero.</summary>
    public readonly float Float0;

    /// <summary>Float payload one.</summary>
    public readonly float Float1;

    /// <summary>Float payload two.</summary>
    public readonly float Float2;

    /// <summary>Text character.</summary>
    public readonly char Character;

    /// <summary>Creates an event.</summary>
    public InputSourceEvent(
        InputSourceEventType type,
        ushort keyCode = 0,
        byte deviceIndex = 0,
        byte button = 0,
        bool pressed = false,
        bool repeat = false,
        TouchPhase touchPhase = TouchPhase.Start,
        CompositionPhase compositionPhase = CompositionPhase.Start,
        float f0 = 0f,
        float f1 = 0f,
        float f2 = 0f,
        char character = '\0')
    {
        Type = type;
        KeyCode = keyCode;
        DeviceIndex = deviceIndex;
        Button = button;
        Pressed = pressed;
        Repeat = repeat;
        TouchPhase = touchPhase;
        CompositionPhase = compositionPhase;
        Float0 = f0;
        Float1 = f1;
        Float2 = f2;
        Character = character;
    }

    /// <summary>Creates a keyboard event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputSourceEvent Keyboard(ushort keyCode, bool pressed, bool repeat = false) =>
        new(InputSourceEventType.Keyboard, keyCode: keyCode, pressed: pressed, repeat: repeat);

    /// <summary>Creates a mouse-button event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputSourceEvent MouseButton(byte button, bool pressed, float x = 0f, float y = 0f) =>
        new(InputSourceEventType.MouseButton, button: button, pressed: pressed, f0: x, f1: y);

    /// <summary>Creates a pointer-move event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputSourceEvent MouseMove(float x, float y) =>
        new(InputSourceEventType.MouseMove, f0: x, f1: y);

    /// <summary>Creates a wheel event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputSourceEvent MouseWheel(float deltaX, float deltaY) =>
        new(InputSourceEventType.MouseWheel, f0: deltaX, f1: deltaY);

    /// <summary>Creates a touch event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputSourceEvent Touch(TouchPhase phase, byte contactIndex, float x, float y) =>
        new(InputSourceEventType.Touch, deviceIndex: contactIndex, touchPhase: phase, f0: x, f1: y);

    /// <summary>Creates a focus event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputSourceEvent Focus(bool focused) =>
        new(InputSourceEventType.Focus, pressed: focused);

    /// <summary>Creates a text event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputSourceEvent Text(char character) =>
        new(InputSourceEventType.Text, character: character);

    /// <summary>Creates a composition event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputSourceEvent Composition(CompositionPhase phase, int cursor, int selectionLength) =>
        new(InputSourceEventType.Composition, compositionPhase: phase, f0: cursor, f1: selectionLength);
}

/// <summary>
/// Button action tuning.
/// </summary>
public sealed class InputButtonActionDefinition
{
    /// <summary>Actuation threshold in [0, 1].</summary>
    public float PressPoint
    {
        get => field;
        init => field = value is < 0f or > 1f ? ThrowHelper.ThrowArgumentOutOfRange<float>(nameof(value)) : value;
    } = 0.5f;

    /// <summary>Release threshold in [0, 1]. NaN follows half the press point.</summary>
    public float ReleasePoint
    {
        get => field;
        init => field = float.IsNaN(value) ? value
            : value is < 0f or > 1f ? ThrowHelper.ThrowArgumentOutOfRange<float>(nameof(value)) : value;
    } = float.NaN;

    /// <summary>Resolves the effective release threshold.</summary>
    /// <param name="definition">Button tuning.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float EffectiveReleasePoint(InputButtonActionDefinition definition) =>
        float.IsNaN(definition.ReleasePoint) ? definition.PressPoint * 0.5f : definition.ReleasePoint;

    /// <summary>Gesture tuning.</summary>
    public InteractionSettings Interaction { get; init; } = new(InputInteractionType.Press);

    /// <summary>First processor.</summary>
    public ProcessorConfiguration Processor0 { get; init; }

    /// <summary>Second processor.</summary>
    public ProcessorConfiguration Processor1 { get; init; }

    /// <summary>Whether read controls are consumed.</summary>
    public bool Consume { get; init; }
}

/// <summary>
/// Axis action tuning.
/// </summary>
public sealed class InputAxisActionDefinition
{
    /// <summary>Deadzone; assigned values must not be negative.</summary>
    public float Deadzone
    {
        get => field;
        init => field = value < 0f ? ThrowHelper.ThrowArgumentOutOfRange<float>(nameof(value)) : value;
    }

    /// <summary>Clamp minimum.</summary>
    public float ClampMin { get; init; } = float.NegativeInfinity;

    /// <summary>Clamp maximum.</summary>
    public float ClampMax { get; init; } = float.PositiveInfinity;

    /// <summary>Contributor combine mode.</summary>
    public AxisCombineMode Combine { get; init; } = AxisCombineMode.Latest;

    /// <summary>First processor.</summary>
    public ProcessorConfiguration Processor0 { get; init; }

    /// <summary>Second processor.</summary>
    public ProcessorConfiguration Processor1 { get; init; }

    /// <summary>Whether read controls are consumed.</summary>
    public bool Consume { get; init; }
}

/// <summary>
/// Vector action tuning.
/// </summary>
public sealed class InputVector2ActionDefinition
{
    /// <summary>Deadzone; assigned values must not be negative.</summary>
    public float Deadzone
    {
        get => field;
        init => field = value < 0f ? ThrowHelper.ThrowArgumentOutOfRange<float>(nameof(value)) : value;
    }

    /// <summary>Whether outputs normalize.</summary>
    public bool Normalize { get; init; }

    /// <summary>Contributor combine mode.</summary>
    public Vector2CombineMode Combine { get; init; } = Vector2CombineMode.Latest;

    /// <summary>First processor.</summary>
    public ProcessorConfiguration Processor0 { get; init; }

    /// <summary>Second processor.</summary>
    public ProcessorConfiguration Processor1 { get; init; }

    /// <summary>Whether read controls are consumed.</summary>
    public bool Consume { get; init; }
}

/// <summary>
/// Action-map registration.
/// </summary>
/// <typeparam name="TSchema">Action enum.</typeparam>
public sealed class InputContextDefinition<TSchema> where TSchema : unmanaged, Enum
{
    /// <summary>Context identity.</summary>
    public required ContextId Id { get; init; }

    /// <summary>Conflict priority.</summary>
    public PriorityLevel Priority { get; init; } = PriorityLevel.Default;

    /// <summary>Whether the context evaluates.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Capture mode.</summary>
    public ContextCaptureMode Capture { get; init; } = ContextCaptureMode.None;

    /// <summary>Owning user.</summary>
    public UserId UserId { get; init; } = UserId.Any;

    /// <summary>Bindings per action index.</summary>
    public InputBinding[][] BindingsByAction { get; init; } = [];
}

/// <summary>
/// Input user registration.
/// </summary>
public sealed class InputUserDefinition
{
    /// <summary>User identity.</summary>
    public required UserId Id { get; init; }

    /// <summary>Whether the user evaluates.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Whether the user reads the keyboard.</summary>
    public bool OwnsKeyboard { get; init; } = true;

    /// <summary>Whether the user reads the mouse.</summary>
    public bool OwnsMouse { get; init; } = true;

    /// <summary>Whether the user reads touch.</summary>
    public bool OwnsTouch { get; init; } = true;

    /// <summary>Assigned gamepad bitmask.</summary>
    public uint AssignedGamepadMask { get; init; } = 0xFFFFFFFFu;
}

/// <summary>
/// Action callback payload.
/// </summary>
/// <typeparam name="TSchema">Action enum.</typeparam>
public readonly record struct InputActionCallbackContext<TSchema>(
    TSchema Action,
    InputActionPhase Phase,
    ActionKind Kind,
    in InputButtonState ButtonState,
    in InputAxisState AxisState,
    in InputVector2State Vector2State)
    where TSchema : unmanaged, Enum;

/// <summary>
/// Rebind candidate control.
/// </summary>
public readonly record struct InputRebindingCandidate(
    InputControlPath Path,
    DeviceKind Device,
    double TimestampMs);

/// <summary>
/// Rebind request.
/// </summary>
/// <typeparam name="TSchema">Action enum.</typeparam>
public readonly record struct RebindRequest<TSchema>(
    ContextId ContextId,
    TSchema Action,
    SlotIndex Slot = default,
    float TimeoutMs = 5000f,
    DeviceKind DeviceFilter = DeviceKind.None,
    float Threshold = 0.5f)
    where TSchema : unmanaged, Enum;

/// <summary>
/// Rebind session handlers.
/// </summary>
/// <typeparam name="TSchema">Action enum.</typeparam>
public readonly struct RebindHandlers<TSchema> where TSchema : unmanaged, Enum
{
    /// <summary>Accepts or rejects a candidate.</summary>
    public Func<InputRebindingCandidate, bool>? Accept { get; init; }

    /// <summary>Completes with the new binding.</summary>
    public Action<InputBinding>? Complete { get; init; }

    /// <summary>Cancels with a reason.</summary>
    public Action<RebindCancelReason>? Cancel { get; init; }
}

/// <summary>
/// Typed text entry.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct InputTextEntry(char Character, uint Codepoint);

/// <summary>
/// IME composition state.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct InputCompositionState(CompositionPhase Phase, int CursorPosition, int SelectionLength);
