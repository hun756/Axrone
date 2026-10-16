namespace Axrone.Input;

/// <summary>
/// Keyboard key codes. Values match Silk.NET input codes, so a Silk.NET
/// backend can forward events without remapping.
/// </summary>
public enum Key : short
{
    /// <summary>Unrecognized key; the invalid sentinel.</summary>
    Unknown = -1,

    /// <summary>Space bar.</summary>
    Space = 32,

    /// <summary>Apostrophe key.</summary>
    Apostrophe = 39,

    /// <summary>Comma key.</summary>
    Comma = 44,

    /// <summary>Minus key.</summary>
    Minus = 45,

    /// <summary>Period key.</summary>
    Period = 46,

    /// <summary>Slash key.</summary>
    Slash = 47,

    /// <summary>Digit row, zero through nine.</summary>
    Number0 = 48,

    /// <summary>Digit row, zero through nine.</summary>
    Number1 = 49,

    /// <summary>Digit row, zero through nine.</summary>
    Number2 = 50,

    /// <summary>Digit row, zero through nine.</summary>
    Number3 = 51,

    /// <summary>Digit row, zero through nine.</summary>
    Number4 = 52,

    /// <summary>Digit row, zero through nine.</summary>
    Number5 = 53,

    /// <summary>Digit row, zero through nine.</summary>
    Number6 = 54,

    /// <summary>Digit row, zero through nine.</summary>
    Number7 = 55,

    /// <summary>Digit row, zero through nine.</summary>
    Number8 = 56,

    /// <summary>Digit row, zero through nine.</summary>
    Number9 = 57,

    /// <summary>Semicolon key.</summary>
    Semicolon = 59,

    /// <summary>Equal key.</summary>
    Equal = 61,

    /// <summary>Letter key.</summary>
    A = 65,

    /// <summary>Letter key.</summary>
    B = 66,

    /// <summary>Letter key.</summary>
    C = 67,

    /// <summary>Letter key.</summary>
    D = 68,

    /// <summary>Letter key.</summary>
    E = 69,

    /// <summary>Letter key.</summary>
    F = 70,

    /// <summary>Letter key.</summary>
    G = 71,

    /// <summary>Letter key.</summary>
    H = 72,

    /// <summary>Letter key.</summary>
    I = 73,

    /// <summary>Letter key.</summary>
    J = 74,

    /// <summary>Letter key.</summary>
    K = 75,

    /// <summary>Letter key.</summary>
    L = 76,

    /// <summary>Letter key.</summary>
    M = 77,

    /// <summary>Letter key.</summary>
    N = 78,

    /// <summary>Letter key.</summary>
    O = 79,

    /// <summary>Letter key.</summary>
    P = 80,

    /// <summary>Letter key.</summary>
    Q = 81,

    /// <summary>Letter key.</summary>
    R = 82,

    /// <summary>Letter key.</summary>
    S = 83,

    /// <summary>Letter key.</summary>
    T = 84,

    /// <summary>Letter key.</summary>
    U = 85,

    /// <summary>Letter key.</summary>
    V = 86,

    /// <summary>Letter key.</summary>
    W = 87,

    /// <summary>Letter key.</summary>
    X = 88,

    /// <summary>Letter key.</summary>
    Y = 89,

    /// <summary>Letter key.</summary>
    Z = 90,

    /// <summary>Left bracket key.</summary>
    LeftBracket = 91,

    /// <summary>Backslash key.</summary>
    BackSlash = 92,

    /// <summary>Right bracket key.</summary>
    RightBracket = 93,

    /// <summary>Grave accent key.</summary>
    GraveAccent = 96,

    /// <summary>Non-US world key.</summary>
    World1 = 161,

    /// <summary>Non-US world key.</summary>
    World2 = 162,

    /// <summary>Escape key.</summary>
    Escape = 256,

    /// <summary>Enter key.</summary>
    Enter = 257,

    /// <summary>Tab key.</summary>
    Tab = 258,

    /// <summary>Backspace key.</summary>
    Backspace = 259,

    /// <summary>Insert key.</summary>
    Insert = 260,

    /// <summary>Delete key.</summary>
    Delete = 261,

    /// <summary>Right arrow.</summary>
    Right = 262,

    /// <summary>Left arrow.</summary>
    Left = 263,

    /// <summary>Down arrow.</summary>
    Down = 264,

    /// <summary>Up arrow.</summary>
    Up = 265,

    /// <summary>Page up.</summary>
    PageUp = 266,

    /// <summary>Page down.</summary>
    PageDown = 267,

    /// <summary>Home key.</summary>
    Home = 268,

    /// <summary>End key.</summary>
    End = 269,

    /// <summary>Caps lock.</summary>
    CapsLock = 280,

    /// <summary>Scroll lock.</summary>
    ScrollLock = 281,

    /// <summary>Num lock.</summary>
    NumLock = 282,

    /// <summary>Print screen.</summary>
    PrintScreen = 283,

    /// <summary>Pause key.</summary>
    Pause = 284,

    /// <summary>Function key.</summary>
    F1 = 290,

    /// <summary>Function key.</summary>
    F2 = 291,

    /// <summary>Function key.</summary>
    F3 = 292,

    /// <summary>Function key.</summary>
    F4 = 293,

    /// <summary>Function key.</summary>
    F5 = 294,

    /// <summary>Function key.</summary>
    F6 = 295,

    /// <summary>Function key.</summary>
    F7 = 296,

    /// <summary>Function key.</summary>
    F8 = 297,

    /// <summary>Function key.</summary>
    F9 = 298,

    /// <summary>Function key.</summary>
    F10 = 299,

    /// <summary>Function key.</summary>
    F11 = 300,

    /// <summary>Function key.</summary>
    F12 = 301,

    /// <summary>Numpad digit.</summary>
    Keypad0 = 320,

    /// <summary>Numpad digit.</summary>
    Keypad1 = 321,

    /// <summary>Numpad digit.</summary>
    Keypad2 = 322,

    /// <summary>Numpad digit.</summary>
    Keypad3 = 323,

    /// <summary>Numpad digit.</summary>
    Keypad4 = 324,

    /// <summary>Numpad digit.</summary>
    Keypad5 = 325,

    /// <summary>Numpad digit.</summary>
    Keypad6 = 326,

    /// <summary>Numpad digit.</summary>
    Keypad7 = 327,

    /// <summary>Numpad digit.</summary>
    Keypad8 = 328,

    /// <summary>Numpad digit.</summary>
    Keypad9 = 329,

    /// <summary>Left shift modifier.</summary>
    ShiftLeft = 340,

    /// <summary>Left control modifier.</summary>
    ControlLeft = 341,

    /// <summary>Left alt modifier.</summary>
    AltLeft = 342,

    /// <summary>Left super modifier.</summary>
    SuperLeft = 343,

    /// <summary>Right shift modifier.</summary>
    ShiftRight = 344,

    /// <summary>Right control modifier.</summary>
    ControlRight = 345,

    /// <summary>Right alt modifier.</summary>
    AltRight = 346,

    /// <summary>Right super modifier.</summary>
    SuperRight = 347,

    /// <summary>Menu key.</summary>
    Menu = 348,
}

/// <summary>
/// Mouse button codes. Values match Silk.NET input codes.
/// </summary>
public enum MouseButton : byte
{
    /// <summary>Primary button.</summary>
    Left = 0,

    /// <summary>Secondary button.</summary>
    Right = 1,

    /// <summary>Middle button.</summary>
    Middle = 2,

    /// <summary>Extended button.</summary>
    Button4 = 3,

    /// <summary>Extended button.</summary>
    Button5 = 4,

    /// <summary>Extended button.</summary>
    Button6 = 5,

    /// <summary>Extended button.</summary>
    Button7 = 6,

    /// <summary>Extended button.</summary>
    Button8 = 7,

    /// <summary>Extended button.</summary>
    Button9 = 8,

    /// <summary>Extended button.</summary>
    Button10 = 9,

    /// <summary>Extended button.</summary>
    Button11 = 10,

    /// <summary>Extended button.</summary>
    Button12 = 11,
}

/// <summary>
/// Gamepad button names. Values match Silk.NET input codes.
/// </summary>
public enum ButtonName : byte
{
    /// <summary>Bottom face button.</summary>
    A = 0,

    /// <summary>Right face button.</summary>
    B = 1,

    /// <summary>Left face button.</summary>
    X = 2,

    /// <summary>Top face button.</summary>
    Y = 3,

    /// <summary>Left shoulder.</summary>
    LeftBumper = 4,

    /// <summary>Right shoulder.</summary>
    RightBumper = 5,

    /// <summary>Select/back.</summary>
    Back = 6,

    /// <summary>Start.</summary>
    Start = 7,

    /// <summary>Guide/home.</summary>
    Home = 8,

    /// <summary>Left stick press.</summary>
    LeftStick = 9,

    /// <summary>Right stick press.</summary>
    RightStick = 10,

    /// <summary>DPad up.</summary>
    DPadUp = 11,

    /// <summary>DPad right.</summary>
    DPadRight = 12,

    /// <summary>DPad down.</summary>
    DPadDown = 13,

    /// <summary>DPad left.</summary>
    DPadLeft = 14,
}

/// <summary>
/// Modifier keys held alongside an input.
/// </summary>
[Flags]
public enum InputModifiers : byte
{
    /// <summary>No modifier held.</summary>
    None = 0,

    /// <summary>Shift held.</summary>
    Shift = 1 << 0,

    /// <summary>Control held.</summary>
    Control = 1 << 1,

    /// <summary>Alt held.</summary>
    Alt = 1 << 2,

    /// <summary>Super/meta held.</summary>
    Meta = 1 << 3,
}
