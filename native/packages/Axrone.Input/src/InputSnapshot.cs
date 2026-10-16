namespace Axrone.Input;

/// <summary>
/// Reads debounced per-frame input state: held, pressed and released edges.
/// </summary>
public interface IInputStateQuery
{
    /// <summary>Whether a key is held.</summary>
    bool IsKeyDown(Key key);

    /// <summary>Whether a key transitioned down this frame.</summary>
    bool IsKeyPressed(Key key);

    /// <summary>Whether a key transitioned up this frame.</summary>
    bool IsKeyReleased(Key key);

    /// <summary>Whether every listed key is held.</summary>
    bool AreAllKeysDown(params ReadOnlySpan<Key> keys);

    /// <summary>Whether any listed key is held.</summary>
    bool IsAnyKeyDown(params ReadOnlySpan<Key> keys);

    /// <summary>Whether a mouse button is held.</summary>
    bool IsMouseButtonDown(MouseButton button);

    /// <summary>Whether a mouse button transitioned down this frame.</summary>
    bool IsMouseButtonPressed(MouseButton button);

    /// <summary>Whether a mouse button transitioned up this frame.</summary>
    bool IsMouseButtonReleased(MouseButton button);

    /// <summary>Pointer position.</summary>
    Vector2 MousePosition { get; }

    /// <summary>Pointer movement this frame.</summary>
    Vector2 MouseDelta { get; }

    /// <summary>Wheel movement this frame.</summary>
    Vector2 ScrollDelta { get; }

    /// <summary>Whether a gamepad button is held.</summary>
    bool IsGamepadButtonDown(DeviceIndex gamepad, ButtonName name);

    /// <summary>Whether a gamepad button transitioned down this frame.</summary>
    bool IsGamepadButtonPressed(DeviceIndex gamepad, ButtonName name);

    /// <summary>Gamepad axis position.</summary>
    float GetGamepadAxis(DeviceIndex gamepad, int axisIndex);
}

/// <summary>
/// 512-bit key state: eight words covering key codes 0-511. Explicit words
/// instead of an inline array: foreach over an inline-array struct would
/// enumerate raw words rather than this pressed-key enumerator.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct KeyStateBitset
{
    private ulong _w0;
    private ulong _w1;
    private ulong _w2;
    private ulong _w3;
    private ulong _w4;
    private ulong _w5;
    private ulong _w6;
    private ulong _w7;

    /// <summary>Reads a word by index.</summary>
    private readonly ulong Word(int index) => index switch
    {
        0 => _w0,
        1 => _w1,
        2 => _w2,
        3 => _w3,
        4 => _w4,
        5 => _w5,
        6 => _w6,
        _ => _w7,
    };

    /// <summary>Writes a word by index.</summary>
    private void SetWord(int index, ulong value)
    {
        switch (index)
        {
            case 0: _w0 = value; break;
            case 1: _w1 = value; break;
            case 2: _w2 = value; break;
            case 3: _w3 = value; break;
            case 4: _w4 = value; break;
            case 5: _w5 = value; break;
            case 6: _w6 = value; break;
            default: _w7 = value; break;
        }
    }

    /// <summary>Whether a key index is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool IsSet(uint keyIndex)
    {
        if (keyIndex >= 512)
        {
            return false;
        }

        int word = (int)(keyIndex >> 6);
        ulong bit = 1UL << (int)(keyIndex & 63);
        return (Word(word) & bit) != 0;
    }

    /// <summary>Sets a key index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Set(uint keyIndex)
    {
        if (keyIndex >= 512)
        {
            return;
        }

        int word = (int)(keyIndex >> 6);
        SetWord(word, Word(word) | (1UL << (int)(keyIndex & 63)));
    }

    /// <summary>Clears a key index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Clear(uint keyIndex)
    {
        if (keyIndex >= 512)
        {
            return;
        }

        int word = (int)(keyIndex >> 6);
        SetWord(word, Word(word) & ~(1UL << (int)(keyIndex & 63)));
    }

    /// <summary>Clears all keys.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ClearAll()
    {
        _w0 = 0UL;
        _w1 = 0UL;
        _w2 = 0UL;
        _w3 = 0UL;
        _w4 = 0UL;
        _w5 = 0UL;
        _w6 = 0UL;
        _w7 = 0UL;
    }

    /// <summary>Enumerates pressed keys.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly PressedKeyEnumerator GetEnumerator() => new(Word(0), Word(1), Word(2), Word(3), Word(4), Word(5), Word(6), Word(7));

    /// <summary>
    /// Allocation-free enumerator over pressed keys.
    /// </summary>
    public ref struct PressedKeyEnumerator
    {
        private ulong _w0;
        private ulong _w1;
        private ulong _w2;
        private ulong _w3;
        private ulong _w4;
        private ulong _w5;
        private ulong _w6;
        private ulong _w7;
        private int _wordIndex;
        private ulong _word;
        private int _currentKey;

        /// <summary>Creates an enumerator over eight words.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PressedKeyEnumerator(ulong w0, ulong w1, ulong w2, ulong w3, ulong w4, ulong w5, ulong w6, ulong w7)
        {
            _w0 = w0;
            _w1 = w1;
            _w2 = w2;
            _w3 = w3;
            _w4 = w4;
            _w5 = w5;
            _w6 = w6;
            _w7 = w7;
            _wordIndex = 0;
            _word = w0;
            _currentKey = -1;
        }

        /// <summary>Advances to the next pressed key.</summary>
        /// <returns><see langword="true"/> while keys remain.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_wordIndex < 8)
            {
                if (_word != 0)
                {
                    int trailing = BitOperations.TrailingZeroCount(_word);
                    _currentKey = (_wordIndex << 6) + trailing;
                    _word &= _word - 1;
                    return true;
                }

                _wordIndex++;
                if (_wordIndex < 8)
                {
                    _word = _wordIndex switch
                    {
                        1 => _w1,
                        2 => _w2,
                        3 => _w3,
                        4 => _w4,
                        5 => _w5,
                        6 => _w6,
                        7 => _w7,
                        _ => _w0,
                    };
                }
            }

            return false;
        }

        /// <summary>Current pressed key.</summary>
        public readonly Key Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Key)_currentKey;
        }
    }
}

/// <summary>
/// Gamepad axis storage: four gamepads times six axes.
/// </summary>
[InlineArray(24)]
public struct GamepadAxesStorage
{
    private float _element0;
}

/// <summary>
/// One debounced frame of input: keyboard bits, mouse buttons and pointer,
/// gamepad buttons and axes. The coordinator owns two copies (current and
/// previous) and derives pressed/released edges from their difference.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct InputSnapshot
{
    /// <summary>Keyboard state.</summary>
    public KeyStateBitset Keys;

    /// <summary>Mouse button bits.</summary>
    public ushort MouseButtons;

    /// <summary>Gamepad button bits in engine numbering, slot zero.</summary>
    public uint GamepadButtons0;

    /// <summary>Gamepad button bits in engine numbering, slot one.</summary>
    public uint GamepadButtons1;

    /// <summary>Gamepad button bits in engine numbering, slot two.</summary>
    public uint GamepadButtons2;

    /// <summary>Gamepad button bits in engine numbering, slot three.</summary>
    public uint GamepadButtons3;

    /// <summary>Pointer X.</summary>
    public float MouseX;

    /// <summary>Pointer Y.</summary>
    public float MouseY;

    /// <summary>Pointer X movement this frame.</summary>
    public float MouseDeltaX;

    /// <summary>Pointer Y movement this frame.</summary>
    public float MouseDeltaY;

    /// <summary>Wheel X movement this frame.</summary>
    public float ScrollDeltaX;

    /// <summary>Wheel Y movement this frame.</summary>
    public float ScrollDeltaY;

    /// <summary>Gamepad axes, row-major per slot.</summary>
    public GamepadAxesStorage GamepadAxes;

    /// <summary>Touch contacts, up to eight concurrent.</summary>
    public TouchContactStorage TouchContacts;

    /// <summary>Gamepad connection bitmask over four slots.</summary>
    public uint GamepadConnectedMask;

    /// <summary>Pinch distance between the first two contacts.</summary>
    public float TouchPinch;

    /// <summary>Pinch distance change this frame.</summary>
    public float TouchPinchDelta;
}

/// <summary>
/// One touch contact: activity plus position.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct TouchContact
{
    /// <summary>Whether the contact is down.</summary>
    public bool Active;

    /// <summary>Contact X.</summary>
    public float X;

    /// <summary>Contact Y.</summary>
    public float Y;
}

/// <summary>
/// Touch-contact storage: eight slots.
/// </summary>
[InlineArray(8)]
public struct TouchContactStorage
{
    private TouchContact _element0;
}
