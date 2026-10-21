namespace Axrone.Shapes2D;

/// <summary>CSS-like color parser: hex and named colors.</summary>
public static class ColorParser
{
    private static readonly Dictionary<string, ResolvedColor> s_namedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transparent"] = ResolvedColor.Transparent,
        ["black"] = ResolvedColor.Black,
        ["white"] = ResolvedColor.White,
        ["red"] = new(1f, 0f, 0f, 1f),
        ["green"] = new(0f, 0.50196f, 0f, 1f),
        ["blue"] = new(0f, 0f, 1f, 1f),
        ["yellow"] = new(1f, 1f, 0f, 1f),
        ["cyan"] = new(0f, 1f, 1f, 1f),
        ["magenta"] = new(1f, 0f, 1f, 1f),
        ["gray"] = new(0.50196f, 0.50196f, 0.50196f, 1f),
        ["grey"] = new(0.50196f, 0.50196f, 0.50196f, 1f),
        ["orange"] = new(1f, 0.647f, 0f, 1f),
        ["purple"] = new(0.50196f, 0f, 0.50196f, 1f),
    };

    /// <summary>Parses a hex or named color.</summary>
    public static ResolvedColor ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ThrowHelper.ThrowPaintValidation("Color string must not be empty.");
        }

        string trimmed = value.Trim();
        if (trimmed.StartsWith('#'))
        {
            return ParseHexColor(trimmed.AsSpan(1));
        }

        if (s_namedColors.TryGetValue(trimmed, out ResolvedColor named))
        {
            return named;
        }

        throw new PaintValidationException($"Unsupported color string: '{trimmed}'.");
    }

    /// <summary>Creates a clamped color from channels.</summary>
    public static ResolvedColor FromRgb(float r, float g, float b, float a = 1.0f)
    {
        ThrowHelper.AssertFinite(r, "color.r");
        ThrowHelper.AssertFinite(g, "color.g");
        ThrowHelper.AssertFinite(b, "color.b");
        ThrowHelper.AssertFinite(a, "color.a");
        return new ResolvedColor(r, g, b, a).Clamp();
    }

    private static ResolvedColor ParseHexColor(ReadOnlySpan<char> hex)
    {
        if (hex.Length == 3)
        {
            float r = ParseHexNibble(hex[0]) * (17f / 255f);
            float g = ParseHexNibble(hex[1]) * (17f / 255f);
            float b = ParseHexNibble(hex[2]) * (17f / 255f);
            return new ResolvedColor(r, g, b, 1f);
        }

        if (hex.Length == 4)
        {
            float r = ParseHexNibble(hex[0]) * (17f / 255f);
            float g = ParseHexNibble(hex[1]) * (17f / 255f);
            float b = ParseHexNibble(hex[2]) * (17f / 255f);
            float a = ParseHexNibble(hex[3]) * (17f / 255f);
            return new ResolvedColor(r, g, b, a);
        }

        if (hex.Length == 6)
        {
            float r = ParseHexByte(hex.Slice(0, 2)) / 255f;
            float g = ParseHexByte(hex.Slice(2, 2)) / 255f;
            float b = ParseHexByte(hex.Slice(4, 2)) / 255f;
            return new ResolvedColor(r, g, b, 1f);
        }

        if (hex.Length == 8)
        {
            float r = ParseHexByte(hex.Slice(0, 2)) / 255f;
            float g = ParseHexByte(hex.Slice(2, 2)) / 255f;
            float b = ParseHexByte(hex.Slice(4, 2)) / 255f;
            float a = ParseHexByte(hex.Slice(6, 2)) / 255f;
            return new ResolvedColor(r, g, b, a);
        }

        throw new PaintValidationException($"Invalid hexadecimal color format: '#{hex.ToString()}'.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ParseHexNibble(char c)
    {
        uint val = c;
        if (val - '0' <= 9)
        {
            return (int)(val - '0');
        }

        if ((val | 0x20) - 'a' <= 5)
        {
            return (int)((val | 0x20) - 'a' + 10);
        }

        ThrowHelper.ThrowPaintValidation($"Invalid hexadecimal character: '{c}'.");
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ParseHexByte(ReadOnlySpan<char> span) => (ParseHexNibble(span[0]) << 4) | ParseHexNibble(span[1]);
}
