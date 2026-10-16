namespace Axrone.Input.Android;

using Axrone.Input;

/// <summary>
/// Android <c>KeyEvent</c> keycode table. Values are the stable AOSP
/// <c>KEYCODE_*</c> constants, mirrored here so the mapping core builds
/// without the Android workload.
/// </summary>
public static class AndroidKeycodes
{
    /// <summary>Unknown key.</summary>
    public const int Unknown = 0;

    /// <summary>Back button.</summary>
    public const int Back = 4;

    /// <summary>Digit row 0-9.</summary>
    public const int Number0 = 7;

    /// <summary>Digit 1.</summary>
    public const int Number1 = 8;

    /// <summary>Digit 2.</summary>
    public const int Number2 = 9;

    /// <summary>Digit 3.</summary>
    public const int Number3 = 10;

    /// <summary>Digit 4.</summary>
    public const int Number4 = 11;

    /// <summary>Digit 5.</summary>
    public const int Number5 = 12;

    /// <summary>Digit 6.</summary>
    public const int Number6 = 13;

    /// <summary>Digit 7.</summary>
    public const int Number7 = 14;

    /// <summary>Digit 8.</summary>
    public const int Number8 = 15;

    /// <summary>Digit 9.</summary>
    public const int Number9 = 16;

    /// <summary>DPad up.</summary>
    public const int DPadUp = 19;

    /// <summary>DPad down.</summary>
    public const int DPadDown = 20;

    /// <summary>DPad left.</summary>
    public const int DPadLeft = 21;

    /// <summary>DPad right.</summary>
    public const int DPadRight = 22;

    /// <summary>DPad center.</summary>
    public const int DPadCenter = 23;

    /// <summary>Volume up.</summary>
    public const int VolumeUp = 24;

    /// <summary>Volume down.</summary>
    public const int VolumeDown = 25;

    /// <summary>Letters A-Z are contiguous from here.</summary>
    public const int A = 29;

    /// <summary>Comma.</summary>
    public const int Comma = 55;

    /// <summary>Period.</summary>
    public const int Period = 56;

    /// <summary>Left alt.</summary>
    public const int AltLeft = 57;

    /// <summary>Right alt.</summary>
    public const int AltRight = 58;

    /// <summary>Left shift.</summary>
    public const int ShiftLeft = 59;

    /// <summary>Right shift.</summary>
    public const int ShiftRight = 60;

    /// <summary>Tab.</summary>
    public const int Tab = 61;

    /// <summary>Space.</summary>
    public const int Space = 62;

    /// <summary>Enter.</summary>
    public const int Enter = 66;

    /// <summary>Delete (backspace).</summary>
    public const int Del = 67;

    /// <summary>Grave accent.</summary>
    public const int Grave = 68;

    /// <summary>Minus.</summary>
    public const int Minus = 69;

    /// <summary>Equals.</summary>
    public const int EqualsKey = 70;

    /// <summary>Left bracket.</summary>
    public const int LeftBracket = 71;

    /// <summary>Right bracket.</summary>
    public const int RightBracket = 72;

    /// <summary>Backslash.</summary>
    public const int Backslash = 73;

    /// <summary>Semicolon.</summary>
    public const int Semicolon = 74;

    /// <summary>Apostrophe.</summary>
    public const int Apostrophe = 75;

    /// <summary>Slash.</summary>
    public const int Slash = 76;

    /// <summary>At sign.</summary>
    public const int At = 77;

    /// <summary>Escape.</summary>
    public const int Escape = 111;

    /// <summary>Left control.</summary>
    public const int CtrlLeft = 113;

    /// <summary>Right control.</summary>
    public const int CtrlRight = 114;

    /// <summary>Function keys F1-F12 are contiguous from here.</summary>
    public const int F1 = 131;

    /// <summary>Meta state: shift held.</summary>
    public const int MetaShiftOn = 0x1;

    /// <summary>Meta state: alt held.</summary>
    public const int MetaAltOn = 0x2;

    /// <summary>Meta state: control held.</summary>
    public const int MetaCtrlOn = 0x1000;

    /// <summary>Meta state: meta held.</summary>
    public const int MetaMetaOn = 0x10000;
}

/// <summary>
/// Maps Android keycodes to engine <see cref="Key"/> codes.
/// </summary>
public static class AndroidKeyMap
{
    /// <summary>Maps a keycode, or <see cref="Key.Unknown"/> when unmapped.</summary>
    /// <param name="keyCode">Android <c>KeyEvent</c> keycode.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Key ToKey(int keyCode)
    {
        if (keyCode >= AndroidKeycodes.A && keyCode <= AndroidKeycodes.A + 25)
        {
            return (Key)(65 + (keyCode - AndroidKeycodes.A));
        }

        if (keyCode >= AndroidKeycodes.Number0 && keyCode <= AndroidKeycodes.Number9)
        {
            return (Key)(48 + (keyCode - AndroidKeycodes.Number0));
        }

        if (keyCode >= AndroidKeycodes.F1 && keyCode <= AndroidKeycodes.F1 + 11)
        {
            return (Key)(290 + (keyCode - AndroidKeycodes.F1));
        }

        return keyCode switch
        {
            AndroidKeycodes.Back => Key.Escape,
            AndroidKeycodes.DPadUp => Key.Up,
            AndroidKeycodes.DPadDown => Key.Down,
            AndroidKeycodes.DPadLeft => Key.Left,
            AndroidKeycodes.DPadRight => Key.Right,
            AndroidKeycodes.Comma => Key.Comma,
            AndroidKeycodes.Period => Key.Period,
            AndroidKeycodes.AltLeft => Key.AltLeft,
            AndroidKeycodes.AltRight => Key.AltRight,
            AndroidKeycodes.ShiftLeft => Key.ShiftLeft,
            AndroidKeycodes.ShiftRight => Key.ShiftRight,
            AndroidKeycodes.Tab => Key.Tab,
            AndroidKeycodes.Space => Key.Space,
            AndroidKeycodes.Enter => Key.Enter,
            AndroidKeycodes.Del => Key.Backspace,
            AndroidKeycodes.Grave => Key.GraveAccent,
            AndroidKeycodes.Minus => Key.Minus,
            AndroidKeycodes.EqualsKey => Key.Equal,
            AndroidKeycodes.LeftBracket => Key.LeftBracket,
            AndroidKeycodes.RightBracket => Key.RightBracket,
            AndroidKeycodes.Backslash => Key.BackSlash,
            AndroidKeycodes.Semicolon => Key.Semicolon,
            AndroidKeycodes.Apostrophe => Key.Apostrophe,
            AndroidKeycodes.Slash => Key.Slash,
            AndroidKeycodes.Escape => Key.Escape,
            AndroidKeycodes.CtrlLeft => Key.ControlLeft,
            AndroidKeycodes.CtrlRight => Key.ControlRight,
            _ => Key.Unknown,
        };
    }

    /// <summary>Reads held modifiers from a meta state bitmask.</summary>
    /// <param name="metaState">Android <c>KeyEvent</c> meta state.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InputModifiers ToModifiers(int metaState)
    {
        InputModifiers modifiers = InputModifiers.None;
        if ((metaState & AndroidKeycodes.MetaShiftOn) != 0)
        {
            modifiers |= InputModifiers.Shift;
        }

        if ((metaState & AndroidKeycodes.MetaAltOn) != 0)
        {
            modifiers |= InputModifiers.Alt;
        }

        if ((metaState & AndroidKeycodes.MetaCtrlOn) != 0)
        {
            modifiers |= InputModifiers.Control;
        }

        if ((metaState & AndroidKeycodes.MetaMetaOn) != 0)
        {
            modifiers |= InputModifiers.Meta;
        }

        return modifiers;
    }
}
