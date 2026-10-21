namespace Axrone.Numeric;

/// <summary>Blend mode.</summary>
public enum ColorBlendMode : byte
{
    /// <summary>Replace.</summary>
    Normal = 0,

    /// <summary>Multiply.</summary>
    Multiply = 1,

    /// <summary>Screen.</summary>
    Screen = 2,

    /// <summary>Overlay.</summary>
    Overlay = 3,

    /// <summary>Soft light.</summary>
    SoftLight = 4,

    /// <summary>Hard light.</summary>
    HardLight = 5,

    /// <summary>Color dodge.</summary>
    ColorDodge = 6,

    /// <summary>Color burn.</summary>
    ColorBurn = 7,

    /// <summary>Darken.</summary>
    Darken = 8,

    /// <summary>Lighten.</summary>
    Lighten = 9,

    /// <summary>Difference.</summary>
    Difference = 10,

    /// <summary>Exclusion.</summary>
    Exclusion = 11,

    /// <summary>Hue.</summary>
    Hue = 12,

    /// <summary>Saturation.</summary>
    Saturation = 13,

    /// <summary>Color.</summary>
    Color = 14,

    /// <summary>Luminosity.</summary>
    Luminosity = 15,
}

/// <summary>Color harmony scheme.</summary>
public enum ColorHarmonyType : byte
{
    /// <summary>Monochromatic.</summary>
    Monochromatic = 0,

    /// <summary>Complementary.</summary>
    Complementary = 1,

    /// <summary>Split complementary.</summary>
    SplitComplementary = 2,

    /// <summary>Triadic.</summary>
    Triadic = 3,

    /// <summary>Tetradic.</summary>
    Tetradic = 4,

    /// <summary>Analogous.</summary>
    Analogous = 5,

    /// <summary>Square.</summary>
    Square = 6,
}

/// <summary>Color comparison mode.</summary>
public enum ColorComparisonMode : byte
{
    /// <summary>By luminance.</summary>
    Luminance = 0,

    /// <summary>By hue.</summary>
    Hue = 1,

    /// <summary>By saturation.</summary>
    Saturation = 2,

    /// <summary>By RGB distance from black.</summary>
    RgbDistance = 3,

    /// <summary>By Lab distance from black.</summary>
    LabDistance = 4,

    /// <summary>By alpha.</summary>
    Alpha = 5,
}

/// <summary>Accessibility contrast level.</summary>
public enum AccessibilityLevel : byte
{
    /// <summary>WCAG AA (4.5:1).</summary>
    AA = 0,

    /// <summary>WCAG AAA (7:1).</summary>
    AAA = 1,
}

/// <summary>
/// Immutable RGBA color (16 bytes): factories, space conversions, arithmetic,
/// blending, metrics, harmonies and formatting.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Color : IEquatable<Color>
{
    private const float Epsilon = 1e-6f;
    private const float D65X = 0.95047f;
    private const float D65Y = 1.0f;
    private const float D65Z = 1.08883f;

    /// <summary>Red channel, clamped to [0, 1].</summary>
    public float R { get; init => field = Math.Clamp(value, 0f, 1f); }

    /// <summary>Green channel, clamped to [0, 1].</summary>
    public float G { get; init => field = Math.Clamp(value, 0f, 1f); }

    /// <summary>Blue channel, clamped to [0, 1].</summary>
    public float B { get; init => field = Math.Clamp(value, 0f, 1f); }

    /// <summary>Alpha channel, clamped to [0, 1].</summary>
    public float A { get; init => field = Math.Clamp(value, 0f, 1f); }

    /// <summary>Creates a color, clamping channels to [0, 1].</summary>
    public Color(float r, float g, float b, float a = 1f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Transparent black.</summary>
    public static readonly Color Transparent = new(0f, 0f, 0f, 0f);

    /// <summary>Opaque black.</summary>
    public static readonly Color Black = new(0f, 0f, 0f, 1f);

    /// <summary>Opaque white.</summary>
    public static readonly Color White = new(1f, 1f, 1f, 1f);

    /// <summary>Opaque red.</summary>
    public static readonly Color Red = new(1f, 0f, 0f, 1f);

    /// <summary>Opaque green (sRGB primary).</summary>
    public static readonly Color Green = new(0f, 1f, 0f, 1f);

    /// <summary>Opaque blue.</summary>
    public static readonly Color Blue = new(0f, 0f, 1f, 1f);

    /// <summary>Opaque yellow.</summary>
    public static readonly Color Yellow = new(1f, 1f, 0f, 1f);

    /// <summary>Opaque cyan.</summary>
    public static readonly Color Cyan = new(0f, 1f, 1f, 1f);

    /// <summary>Opaque magenta.</summary>
    public static readonly Color Magenta = new(1f, 0f, 1f, 1f);

    /// <summary>Opaque orange.</summary>
    public static readonly Color Orange = new(1f, 0.5f, 0f, 1f);

    /// <summary>Opaque purple.</summary>
    public static readonly Color Purple = new(0.5f, 0f, 0.5f, 1f);

    /// <summary>Opaque brown.</summary>
    public static readonly Color Brown = new(0.6f, 0.4f, 0.2f, 1f);

    /// <summary>Opaque pink.</summary>
    public static readonly Color Pink = new(1f, 0.75f, 0.8f, 1f);

    /// <summary>Opaque gray.</summary>
    public static readonly Color Gray = new(0.5f, 0.5f, 0.5f, 1f);

    /// <summary>Opaque light gray.</summary>
    public static readonly Color LightGray = new(0.75f, 0.75f, 0.75f, 1f);

    /// <summary>Opaque dark gray.</summary>
    public static readonly Color DarkGray = new(0.25f, 0.25f, 0.25f, 1f);

    /// <summary>Opaque navy.</summary>
    public static readonly Color Navy = new(0f, 0f, 0.5f, 1f);

    /// <summary>Opaque maroon.</summary>
    public static readonly Color Maroon = new(0.5f, 0f, 0f, 1f);

    /// <summary>Opaque olive.</summary>
    public static readonly Color Olive = new(0.5f, 0.5f, 0f, 1f);

    /// <summary>Opaque lime.</summary>
    public static readonly Color Lime = new(0.5f, 1f, 0f, 1f);

    /// <summary>Opaque aqua.</summary>
    public static readonly Color Aqua = new(0f, 1f, 1f, 1f);

    /// <summary>Opaque teal.</summary>
    public static readonly Color Teal = new(0f, 0.5f, 0.5f, 1f);

    /// <summary>Opaque silver.</summary>
    public static readonly Color Silver = new(0.7529411764705882f, 0.7529411764705882f, 0.7529411764705882f, 1f);

    /// <summary>Opaque fuchsia.</summary>
    public static readonly Color Fuchsia = new(1f, 0f, 1f, 1f);

    private static readonly Dictionary<string, Color> s_namedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transparent"] = Transparent,
        ["black"] = Black,
        ["white"] = White,
        ["red"] = Red,
        ["green"] = Green,
        ["blue"] = Blue,
        ["yellow"] = Yellow,
        ["cyan"] = Cyan,
        ["magenta"] = Magenta,
        ["orange"] = Orange,
        ["purple"] = Purple,
        ["brown"] = Brown,
        ["pink"] = Pink,
        ["gray"] = Gray,
        ["grey"] = Gray,
        ["lightgray"] = LightGray,
        ["lightgrey"] = LightGray,
        ["darkgray"] = DarkGray,
        ["darkgrey"] = DarkGray,
        ["navy"] = Navy,
        ["maroon"] = Maroon,
        ["olive"] = Olive,
        ["lime"] = Lime,
        ["aqua"] = Aqua,
        ["teal"] = Teal,
        ["silver"] = Silver,
        ["fuchsia"] = Fuchsia,
    };

    /// <inheritdoc/>
    public bool Equals(Color other) =>
        R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    /// <summary>Epsilon equality.</summary>
    public bool ApproximatelyEquals(Color other, float epsilon = Epsilon) =>
        MathF.Abs(R - other.R) < epsilon &&
        MathF.Abs(G - other.G) < epsilon &&
        MathF.Abs(B - other.B) < epsilon &&
        MathF.Abs(A - other.A) < epsilon;

    /// <summary>Creates from channels, clamped to [0, 1].</summary>
    public static Color Create(float r = 0f, float g = 0f, float b = 0f, float a = 1f) =>
        new(Math.Clamp(r, 0f, 1f), Math.Clamp(g, 0f, 1f), Math.Clamp(b, 0f, 1f), Math.Clamp(a, 0f, 1f));

    /// <summary>Creates from an array slice.</summary>
    public static Color FromArray(ReadOnlySpan<float> values, int offset = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (values.Length < offset + 3)
        {
            throw new ArgumentOutOfRangeException(nameof(values), values.Length, "Array must hold at least 3 elements at the offset.");
        }

        float a = values.Length >= offset + 4 ? values[offset + 3] : 1f;
        return Create(values[offset], values[offset + 1], values[offset + 2], a);
    }

    /// <summary>Parses #rgb, #rgba, #rrggbb or #rrggbbaa.</summary>
    public static Color FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        string cleaned = hex.StartsWith('#') ? hex[1..].Trim() : hex.Trim();

        if (cleaned.Length is not (3 or 4 or 6 or 8))
        {
            throw new FormatException($"Invalid hex color format: must be 3, 4, 6, or 8 hex digits (got {cleaned.Length}).");
        }

        foreach (char c in cleaned)
        {
            if (!Uri.IsHexDigit(c))
            {
                throw new FormatException("Invalid hex color format: contains non-hexadecimal characters.");
            }
        }

        if (cleaned.Length is 3 or 4)
        {
            var doubled = new StringBuilder(cleaned.Length * 2);
            foreach (char c in cleaned)
            {
                doubled.Append(c).Append(c);
            }

            cleaned = doubled.ToString();
        }

        uint parsed = Convert.ToUInt32(cleaned, 16);
        if (cleaned.Length == 6)
        {
            return new Color(
                ((parsed >> 16) & 0xFF) / 255f,
                ((parsed >> 8) & 0xFF) / 255f,
                (parsed & 0xFF) / 255f,
                1f);
        }

        return new Color(
            ((parsed >> 24) & 0xFF) / 255f,
            ((parsed >> 16) & 0xFF) / 255f,
            ((parsed >> 8) & 0xFF) / 255f,
            (parsed & 0xFF) / 255f);
    }

    /// <summary>Creates from 0-1 floats, or 0-255 bytes when any channel exceeds 1.</summary>
    public static Color FromRgb(float r, float g, float b, float a = 1f)
    {
        if (r > 1f || g > 1f || b > 1f)
        {
            return Create(r / 255f, g / 255f, b / 255f, a > 1f ? a / 255f : a);
        }

        return Create(r, g, b, a);
    }

    /// <summary>Creates from HSL.</summary>
    public static Color FromHsl(float h, float s, float l, float a = 1f)
    {
        float hN = Mod(h, 360f);
        (float r, float g, float b) = HslToRgbRaw(hN, Math.Clamp(s, 0f, 1f), Math.Clamp(l, 0f, 1f));
        return new Color(r, g, b, Math.Clamp(a, 0f, 1f));
    }

    /// <summary>Creates from HSV.</summary>
    public static Color FromHsv(float h, float s, float v, float a = 1f)
    {
        h = Mod(h, 360f);
        s = Math.Clamp(s, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);
        a = Math.Clamp(a, 0f, 1f);

        float c = v * s;
        float x = c * (1f - MathF.Abs((h / 60f % 2f) - 1f));
        float m = v - c;

        float r = 0f;
        float g = 0f;
        float b = 0f;

        if (h < 60f)
        {
            r = c;
            g = x;
        }
        else if (h < 120f)
        {
            r = x;
            g = c;
        }
        else if (h < 180f)
        {
            g = c;
            b = x;
        }
        else if (h < 240f)
        {
            g = x;
            b = c;
        }
        else if (h < 300f)
        {
            r = x;
            b = c;
        }
        else
        {
            r = c;
            b = x;
        }

        return new Color(r + m, g + m, b + m, a);
    }

    /// <summary>Creates from CMYK.</summary>
    public static Color FromCmyk(float c, float m, float y, float k, float a = 1f)
    {
        c = Math.Clamp(c, 0f, 1f);
        m = Math.Clamp(m, 0f, 1f);
        y = Math.Clamp(y, 0f, 1f);
        k = Math.Clamp(k, 0f, 1f);
        a = Math.Clamp(a, 0f, 1f);

        float invK = 1f - k;
        return new Color((1f - c) * invK, (1f - m) * invK, (1f - y) * invK, a);
    }

    /// <summary>Creates from CIELAB.</summary>
    public static Color FromLab(float l, float a, float b, float alpha = 1f)
    {
        (float r, float g, float bl) = LabToRgbRaw(l, a, b);
        return new Color(Math.Clamp(r, 0f, 1f), Math.Clamp(g, 0f, 1f), Math.Clamp(bl, 0f, 1f), Math.Clamp(alpha, 0f, 1f));
    }

    /// <summary>Creates from CIEXYZ.</summary>
    public static Color FromXyz(float x, float y, float z, float alpha = 1f)
    {
        (float r, float g, float b) = XyzToLinearRgbRaw(x, y, z);
        return new Color(Math.Clamp(r, 0f, 1f), Math.Clamp(g, 0f, 1f), Math.Clamp(b, 0f, 1f), Math.Clamp(alpha, 0f, 1f));
    }

    /// <summary>Creates from color temperature in Kelvin (Tanner Helland).</summary>
    public static Color FromTemperature(float kelvin, float alpha = 1f)
    {
        float temp = Math.Clamp(kelvin, 1000f, 40000f) / 100f;

        float r;
        float g;
        float b;

        if (temp <= 66f)
        {
            r = 255f;
            g = temp <= 19f ? 0f : 99.4708025861f * MathF.Log(temp - 10f) - 161.1195681661f;
            b = temp >= 66f ? 255f : temp <= 19f ? 0f : 138.5177312231f * MathF.Log(temp - 10f) - 305.0447927307f;
        }
        else
        {
            r = 329.698727446f * MathF.Pow(temp - 60f, -0.1332047592f);
            g = 288.1221695283f * MathF.Pow(temp - 60f, -0.0755148492f);
            b = 255f;
        }

        return new Color(Math.Clamp(r / 255f, 0f, 1f), Math.Clamp(g / 255f, 0f, 1f), Math.Clamp(b / 255f, 0f, 1f), Math.Clamp(alpha, 0f, 1f));
    }

    /// <summary>Looks up a named color (case-insensitive, whitespace ignored).</summary>
    public static Color FromNamedColor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        StringBuilder normalized = new(name.Length);
        foreach (char c in name)
        {
            if (!char.IsWhiteSpace(c))
            {
                normalized.Append(char.ToLowerInvariant(c));
            }
        }

        if (s_namedColors.TryGetValue(normalized.ToString(), out Color found))
        {
            return found;
        }

        throw new KeyNotFoundException($"Unknown color name: '{name}'.");
    }

    /// <summary>Converts to HSL.</summary>
    public HslColor ToHsl()
    {
        float max = MathF.Max(R, MathF.Max(G, B));
        float min = MathF.Min(R, MathF.Min(G, B));
        float diff = max - min;

        float l = (max + min) / 2f;
        float h = 0f;
        float s = 0f;

        if (diff != 0f)
        {
            s = l > 0.5f ? diff / (2f - max - min) : diff / (max + min);

            if (max == R)
            {
                h = (G - B) / diff + (G < B ? 6f : 0f);
            }
            else if (max == G)
            {
                h = (B - R) / diff + 2f;
            }
            else
            {
                h = (R - G) / diff + 4f;
            }

            h *= 60f;
        }

        return new HslColor(h, s, l, A);
    }

    /// <summary>Converts to HSV.</summary>
    public HsvColor ToHsv()
    {
        float max = MathF.Max(R, MathF.Max(G, B));
        float min = MathF.Min(R, MathF.Min(G, B));
        float diff = max - min;

        float v = max;
        float s = max == 0f ? 0f : diff / max;
        float h = 0f;

        if (diff != 0f)
        {
            if (max == R)
            {
                h = (G - B) / diff + (G < B ? 6f : 0f);
            }
            else if (max == G)
            {
                h = (B - R) / diff + 2f;
            }
            else
            {
                h = (R - G) / diff + 4f;
            }

            h *= 60f;
        }

        return new HsvColor(h, s, v, A);
    }

    /// <summary>Converts to CMYK.</summary>
    public CmykColor ToCmyk()
    {
        float k = 1f - MathF.Max(R, MathF.Max(G, B));
        float invK = 1f - k;
        float c = k == 1f ? 0f : (1f - R - k) / invK;
        float m = k == 1f ? 0f : (1f - G - k) / invK;
        float y = k == 1f ? 0f : (1f - B - k) / invK;
        return new CmykColor(c, m, y, k, A);
    }

    /// <summary>Converts to CIEXYZ.</summary>
    public XyzColor ToXyz()
    {
        float r = SrgbToLinear(R);
        float g = SrgbToLinear(G);
        float b = SrgbToLinear(B);

        float x = r * 0.4124564f + g * 0.3575761f + b * 0.1804375f;
        float y = r * 0.2126729f + g * 0.7151522f + b * 0.072175f;
        float z = r * 0.0193339f + g * 0.119192f + b * 0.9503041f;
        return new XyzColor(x, y, z, A);
    }

    /// <summary>Converts to CIELAB.</summary>
    public LabColor ToLab()
    {
        XyzColor xyz = ToXyz();

        float xr = xyz.X / D65X;
        float yr = xyz.Y / D65Y;
        float zr = xyz.Z / D65Z;

        float fx = xr > 0.008856f ? MathF.Pow(xr, 1f / 3f) : (903.3f * xr + 16f) / 116f;
        float fy = yr > 0.008856f ? MathF.Pow(yr, 1f / 3f) : (903.3f * yr + 16f) / 116f;
        float fz = zr > 0.008856f ? MathF.Pow(zr, 1f / 3f) : (903.3f * zr + 16f) / 116f;

        float l = 116f * fy - 16f;
        float a = 500f * (fx - fy);
        float b = 200f * (fy - fz);
        return new LabColor(l, a, b, A);
    }

    /// <summary>Formats as #RRGGBB[AA] (uppercase).</summary>
    public string ToHex(bool includeAlpha = false)
    {
        int r = (int)MathF.Round(R * 255f);
        int g = (int)MathF.Round(G * 255f);
        int b = (int)MathF.Round(B * 255f);
        int a = (int)MathF.Round(A * 255f);

        return includeAlpha
            ? $"#{r:X2}{g:X2}{b:X2}{a:X2}"
            : $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>Formats as rgb()/rgba().</summary>
    public string ToRgb(bool includeAlpha = false)
    {
        int r = (int)MathF.Round(R * 255f);
        int g = (int)MathF.Round(G * 255f);
        int b = (int)MathF.Round(B * 255f);

        return includeAlpha
            ? FormattableString.Invariant($"rgba({r}, {g}, {b}, {A})")
            : FormattableString.Invariant($"rgb({r}, {g}, {b})");
    }

    /// <summary>Formats as hsla().</summary>
    public string ToHslString()
    {
        HslColor hsl = ToHsl();
        return FormattableString.Invariant($"hsla({MathF.Round(hsl.H)}, {MathF.Round(hsl.S * 100f)}%, {MathF.Round(hsl.L * 100f)}%, {hsl.A})");
    }

    /// <inheritdoc/>
    public override string ToString() => ToHex(includeAlpha: true);

    private static float Mod(float n, float m) => (n % m + m) % m;

    private static float SrgbToLinear(float c) =>
        c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    private static float LinearToSrgb(float c) =>
        c <= 0.0031308f ? 12.92f * c : 1.055f * MathF.Pow(MathF.Max(c, 0f), 1f / 2.4f) - 0.055f;

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f)
        {
            t += 1f;
        }

        if (t > 1f)
        {
            t -= 1f;
        }

        if (t < 1f / 6f)
        {
            return p + (q - p) * 6f * t;
        }

        if (t < 1f / 2f)
        {
            return q;
        }

        if (t < 2f / 3f)
        {
            return p + (q - p) * (2f / 3f - t) * 6f;
        }

        return p;
    }

    private static (float R, float G, float B) HslToRgbRaw(float h, float s, float l)
    {
        if (s == 0f)
        {
            return (l, l, l);
        }

        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;
        float hNorm = h / 360f;
        return (HueToRgb(p, q, hNorm + 1f / 3f), HueToRgb(p, q, hNorm), HueToRgb(p, q, hNorm - 1f / 3f));
    }

    private static (float R, float G, float B) LabToRgbRaw(float l, float a, float b)
    {
        float fy = (l + 16f) / 116f;
        float fx = a / 500f + fy;
        float fz = fy - b / 200f;

        float xr = fx * fx * fx > 0.008856f ? fx * fx * fx : (116f * fx - 16f) / 903.3f;
        float yr = l > 8f ? fy * fy * fy : l / 903.3f;
        float zr = fz * fz * fz > 0.008856f ? fz * fz * fz : (116f * fz - 16f) / 903.3f;

        float x = xr * D65X;
        float y = yr * D65Y;
        float z = zr * D65Z;

        return XyzToLinearRgbRaw(x, y, z);
    }

    private static (float R, float G, float B) XyzToLinearRgbRaw(float x, float y, float z)
    {
        float r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
        float g = x * -0.9689f + y * 1.8758f + z * 0.0415f;
        float b = x * 0.0557f + y * -0.204f + z * 1.057f;
        return (LinearToSrgb(r), LinearToSrgb(g), LinearToSrgb(b));
    }
}
