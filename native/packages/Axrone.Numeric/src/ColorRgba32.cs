namespace Axrone.Numeric;

/// <summary>
/// Packed 32-bit sRGB color (RGBA, 8 bits per channel). Strictly 4 bytes,
/// promoted directly to CPU integer registers.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public readonly struct Rgba32 :
    IEquatable<Rgba32>,
    IEqualityOperators<Rgba32, Rgba32, bool>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    ISpanParsable<Rgba32>,
    IUtf8SpanParsable<Rgba32>
{
    /// <summary>Red byte.</summary>
    public readonly byte R;

    /// <summary>Green byte.</summary>
    public readonly byte G;

    /// <summary>Blue byte.</summary>
    public readonly byte B;

    /// <summary>Alpha byte.</summary>
    public readonly byte A;

    /// <summary>Creates a packed color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rgba32(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Reinterprets packed bits.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rgba32(uint packedRgba) => this = Unsafe.BitCast<uint, Rgba32>(packedRgba);

    /// <summary>Reinterprets as packed bits.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public uint ToPackedUInt32() => Unsafe.BitCast<Rgba32, uint>(this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Rgba32 other) => ToPackedUInt32() == other.ToPackedUInt32();

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Rgba32 other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode() => (int)ToPackedUInt32();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Rgba32 left, Rgba32 right) => left.ToPackedUInt32() == right.ToPackedUInt32();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Rgba32 left, Rgba32 right) => left.ToPackedUInt32() != right.ToPackedUInt32();

    /// <inheritdoc/>
    public override string ToString() => ToString(null, null);

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[9];
        _ = TryFormat(buffer, out int written, format.AsSpan(), formatProvider);
        return new string(buffer.Slice(0, written));
    }

    /// <inheritdoc/>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = default)
    {
        _ = provider;
        bool lower = format.Length > 0 && format[0] == 'x';
        bool noAlpha = (format.Length > 1 && (format[0] == 'X' || format[0] == 'x') && format[1] == '6') ||
            (format.Length == 0 && A == 255);

        int requiredLength = noAlpha ? 7 : 9;
        if (destination.Length < requiredLength)
        {
            charsWritten = 0;
            return false;
        }

        destination[0] = '#';
        WriteHexByteChar(R, destination.Slice(1, 2), lower);
        WriteHexByteChar(G, destination.Slice(3, 2), lower);
        WriteHexByteChar(B, destination.Slice(5, 2), lower);

        if (!noAlpha)
        {
            WriteHexByteChar(A, destination.Slice(7, 2), lower);
            charsWritten = 9;
            return true;
        }

        charsWritten = 7;
        return true;
    }

    /// <inheritdoc/>
    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = default)
    {
        _ = provider;
        bool lower = format.Length > 0 && format[0] == 'x';
        bool noAlpha = (format.Length > 1 && (format[0] == 'X' || format[0] == 'x') && format[1] == '6') ||
            (format.Length == 0 && A == 255);

        int requiredLength = noAlpha ? 7 : 9;
        if (utf8Destination.Length < requiredLength)
        {
            bytesWritten = 0;
            return false;
        }

        utf8Destination[0] = (byte)'#';
        WriteHexByteUtf8(R, utf8Destination.Slice(1, 2), lower);
        WriteHexByteUtf8(G, utf8Destination.Slice(3, 2), lower);
        WriteHexByteUtf8(B, utf8Destination.Slice(5, 2), lower);

        if (!noAlpha)
        {
            WriteHexByteUtf8(A, utf8Destination.Slice(7, 2), lower);
            bytesWritten = 9;
            return true;
        }

        bytesWritten = 7;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void WriteHexByteChar(byte value, Span<char> destination, bool lower)
    {
        const string Hex = "0123456789ABCDEF";
        const string HexLower = "0123456789abcdef";
        string hex = lower ? HexLower : Hex;
        destination[0] = hex[value >> 4];
        destination[1] = hex[value & 0xF];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void WriteHexByteUtf8(byte value, Span<byte> destination, bool lower)
    {
        ReadOnlySpan<byte> hex = lower ? "0123456789abcdef"u8 : "0123456789ABCDEF"u8;
        destination[0] = hex[value >> 4];
        destination[1] = hex[value & 0xF];
    }

    /// <inheritdoc/>
    public static Rgba32 Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
    {
        _ = provider;
        if (!TryParse(s, provider, out Rgba32 result))
        {
            ThrowHelper.ThrowFormatException("Invalid hexadecimal color string.");
        }

        return result;
    }

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Rgba32 result)
    {
        _ = provider;
        Unsafe.SkipInit(out result);
        if (s.IsEmpty)
        {
            return false;
        }

        if (s[0] == '#')
        {
            s = s.Slice(1);
        }

        switch (s.Length)
        {
            case 3:
                if (TryDecodeNibble(s[0], out uint r3) &&
                    TryDecodeNibble(s[1], out uint g3) &&
                    TryDecodeNibble(s[2], out uint b3))
                {
                    result = new Rgba32((byte)((r3 << 4) | r3), (byte)((g3 << 4) | g3), (byte)((b3 << 4) | b3), 255);
                    return true;
                }

                break;
            case 4:
                if (TryDecodeNibble(s[0], out uint r4) &&
                    TryDecodeNibble(s[1], out uint g4) &&
                    TryDecodeNibble(s[2], out uint b4) &&
                    TryDecodeNibble(s[3], out uint a4))
                {
                    result = new Rgba32((byte)((r4 << 4) | r4), (byte)((g4 << 4) | g4), (byte)((b4 << 4) | b4), (byte)((a4 << 4) | a4));
                    return true;
                }

                break;
            case 6:
                if (TryDecodeByte(s[0], s[1], out byte r6) &&
                    TryDecodeByte(s[2], s[3], out byte g6) &&
                    TryDecodeByte(s[4], s[5], out byte b6))
                {
                    result = new Rgba32(r6, g6, b6, 255);
                    return true;
                }

                break;
            case 8:
                if (TryDecodeByte(s[0], s[1], out byte r8) &&
                    TryDecodeByte(s[2], s[3], out byte g8) &&
                    TryDecodeByte(s[4], s[5], out byte b8) &&
                    TryDecodeByte(s[6], s[7], out byte a8))
                {
                    result = new Rgba32(r8, g8, b8, a8);
                    return true;
                }

                break;
        }

        return false;
    }

    /// <inheritdoc/>
    public static Rgba32 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
    {
        if (!TryParse(utf8Text, provider, out Rgba32 result))
        {
            ThrowHelper.ThrowFormatException("Invalid UTF-8 hexadecimal color string.");
        }

        return result;
    }

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Rgba32 result)
    {
        _ = provider;
        Unsafe.SkipInit(out result);
        if (utf8Text.IsEmpty)
        {
            return false;
        }

        if (utf8Text[0] == (byte)'#')
        {
            utf8Text = utf8Text.Slice(1);
        }

        switch (utf8Text.Length)
        {
            case 3:
                if (TryDecodeNibble((char)utf8Text[0], out uint r3) &&
                    TryDecodeNibble((char)utf8Text[1], out uint g3) &&
                    TryDecodeNibble((char)utf8Text[2], out uint b3))
                {
                    result = new Rgba32((byte)((r3 << 4) | r3), (byte)((g3 << 4) | g3), (byte)((b3 << 4) | b3), 255);
                    return true;
                }

                break;
            case 4:
                if (TryDecodeNibble((char)utf8Text[0], out uint r4) &&
                    TryDecodeNibble((char)utf8Text[1], out uint g4) &&
                    TryDecodeNibble((char)utf8Text[2], out uint b4) &&
                    TryDecodeNibble((char)utf8Text[3], out uint a4))
                {
                    result = new Rgba32((byte)((r4 << 4) | r4), (byte)((g4 << 4) | g4), (byte)((b4 << 4) | b4), (byte)((a4 << 4) | a4));
                    return true;
                }

                break;
            case 6:
                if (TryDecodeByte((char)utf8Text[0], (char)utf8Text[1], out byte r6) &&
                    TryDecodeByte((char)utf8Text[2], (char)utf8Text[3], out byte g6) &&
                    TryDecodeByte((char)utf8Text[4], (char)utf8Text[5], out byte b6))
                {
                    result = new Rgba32(r6, g6, b6, 255);
                    return true;
                }

                break;
            case 8:
                if (TryDecodeByte((char)utf8Text[0], (char)utf8Text[1], out byte r8) &&
                    TryDecodeByte((char)utf8Text[2], (char)utf8Text[3], out byte g8) &&
                    TryDecodeByte((char)utf8Text[4], (char)utf8Text[5], out byte b8) &&
                    TryDecodeByte((char)utf8Text[6], (char)utf8Text[7], out byte a8))
                {
                    result = new Rgba32(r8, g8, b8, a8);
                    return true;
                }

                break;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool TryDecodeNibble(char c, out uint nibble)
    {
        uint value = c;
        uint dec = value - '0';
        if (dec <= 9)
        {
            nibble = dec;
            return true;
        }

        uint hex = (value | 0x20) - 'a';
        if (hex <= 5)
        {
            nibble = hex + 10;
            return true;
        }

        nibble = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool TryDecodeByte(char hi, char lo, out byte value)
    {
        if (TryDecodeNibble(hi, out uint h) && TryDecodeNibble(lo, out uint l))
        {
            value = (byte)((h << 4) | l);
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc/>
    public static Rgba32 Parse(string s, IFormatProvider? provider = null) => Parse(s.AsSpan(), provider);

    /// <inheritdoc/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Rgba32 result) =>
        TryParse(s.AsSpan(), provider, out result);
}
