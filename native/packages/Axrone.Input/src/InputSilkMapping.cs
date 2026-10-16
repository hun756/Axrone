namespace Axrone.Input;

/// <summary>
/// Silk.NET code mappings: numeric casts between Silk.NET.Input enums and the
/// engine enums, guarded against unknown and out-of-range codes. The engine
/// enums carry Silk codes by design; these helpers centralize the boundary.
/// </summary>
public static class SilkInputMapping
{
    /// <summary>Maps a Silk key, or Unknown when out of range.</summary>
    /// <param name="key">Silk key code.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Key ToKey(Silk.NET.Input.Key key)
    {
        int code = (int)key;
        return code is < -1 or > 348 ? Key.Unknown : (Key)code;
    }

    /// <summary>Maps a Silk mouse button.</summary>
    /// <param name="button">Silk mouse button.</param>
    /// <param name="mapped">Mapped button.</param>
    /// <returns><see langword="true"/> when the button is known.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryToMouseButton(Silk.NET.Input.MouseButton button, out MouseButton mapped)
    {
        int code = (int)button;
        if (code is < 0 or > 12)
        {
            mapped = default;
            return false;
        }

        mapped = (MouseButton)code;
        return true;
    }

    /// <summary>Maps a Silk gamepad button to engine numbering.</summary>
    /// <param name="name">Silk button name.</param>
    /// <param name="engine">Engine button index.</param>
    /// <returns><see langword="true"/> when the button is known.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryToGamepadButton(Silk.NET.Input.ButtonName name, out ushort engine)
    {
        int code = (int)name;
        if (code is < 0 or > 14)
        {
            engine = 0;
            return false;
        }

        engine = InputControlReader.MapSilkButton((ushort)code);
        return true;
    }
}
