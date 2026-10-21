namespace Axrone.Numeric;

/// <summary>Accessibility level.</summary>
public enum AccessibilityLevel : byte
{
    /// <summary>WCAG AA (4.5:1).</summary>
    AA = 0,

    /// <summary>WCAG AAA (7:1).</summary>
    AAA = 1,
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

/// <summary>High-level color operations: spaces, names, harmony, metrics, random.</summary>
public static class ColorOps
{
    private const float Epsilon = 1e-6f;
    private const float D65X = 0.95047f;
    private const float D65Y = 1.0f;
    private const float D65Z = 1.08883f;

    private static readonly Dictionary<string, Rgba32> s_namedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transparent"] = new Rgba32(0, 0, 0, 0),
        ["black"] = new Rgba32(0, 0, 0, 255),
        ["white"] = new Rgba32(255, 255, 255, 255),
        ["red"] = new Rgba32(255, 0, 0, 255),
        ["green"] = new Rgba32(0, 255, 0, 255),
        ["blue"] = new Rgba32(0, 0, 255, 255),
        ["yellow"] = new Rgba32(255, 255, 0, 255),
        ["cyan"] = new Rgba32(0, 255, 255, 255),
        ["magenta"] = new Rgba32(255, 0, 255, 255),
        ["orange"] = new Rgba32(255, 128, 0, 255),
        ["purple"] = new Rgba32(128, 0, 128, 255),
        ["brown"] = new Rgba32(153, 102, 51, 255),
        ["pink"] = new Rgba32(255, 191, 204, 255),
        ["gray"] = new Rgba32(128, 128, 128, 255),
        ["grey"] = new Rgba32(128, 128, 128, 255),
        ["lightgray"] = new Rgba32(191, 191, 191, 255),
        ["lightgrey"] = new Rgba32(191, 191, 191, 255),
        ["darkgray"] = new Rgba32(64, 64, 64, 255),
        ["darkgrey"] = new Rgba32(64, 64, 64, 255),
        ["navy"] = new Rgba32(0, 0, 128, 255),
        ["maroon"] = new Rgba32(128, 0, 0, 255),
        ["olive"] = new Rgba32(128, 128, 0, 255),
        ["lime"] = new Rgba32(0, 255, 0, 255),
        ["aqua"] = new Rgba32(0, 255, 255, 255),
        ["teal"] = new Rgba32(0, 128, 128, 255),
        ["silver"] = new Rgba32(192, 192, 192, 255),
        ["fuchsia"] = new Rgba32(255, 0, 255, 255),
    };

    private static float Mod(float n, float m) => (n % m + m) % m;

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);

    private static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

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
        float hN = h / 360f;
        return (HueToRgb(p, q, hN + 1f / 3f), HueToRgb(p, q, hN), HueToRgb(p, q, hN - 1f / 3f));
    }

    /// <summary>Creates from HSL.</summary>
    public static Rgba128Float FromHsl(float h, float s, float l, float a = 1f)
    {
        (float r, float g, float b) = HslToRgbRaw(Mod(h, 360f), Clamp01(s), Clamp01(l));
        return new Rgba128Float(r, g, b, Clamp01(a));
    }

    /// <summary>Converts to HSL.</summary>
    public static HslColor ToHsl(Rgba128Float color)
    {
        float max = MathF.Max(color.R, MathF.Max(color.G, color.B));
        float min = MathF.Min(color.R, MathF.Min(color.G, color.B));
        float diff = max - min;

        float l = (max + min) / 2f;
        float h = 0f;
        float s = 0f;

        if (diff != 0f)
        {
            s = l > 0.5f ? diff / (2f - max - min) : diff / (max + min);

            if (max == color.R)
            {
                h = (color.G - color.B) / diff + (color.G < color.B ? 6f : 0f);
            }
            else if (max == color.G)
            {
                h = (color.B - color.R) / diff + 2f;
            }
            else
            {
                h = (color.R - color.G) / diff + 4f;
            }

            h *= 60f;
        }

        return new HslColor(h, s, l, color.A);
    }

    /// <summary>Creates from HSV.</summary>
    public static Rgba128Float FromHsv(float h, float s, float v, float a = 1f)
    {
        h = Mod(h, 360f);
        s = Clamp01(s);
        v = Clamp01(v);
        a = Clamp01(a);

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

        return new Rgba128Float(r + m, g + m, b + m, a);
    }

    /// <summary>Converts to HSV.</summary>
    public static HsvColor ToHsv(Rgba128Float color)
    {
        float max = MathF.Max(color.R, MathF.Max(color.G, color.B));
        float min = MathF.Min(color.R, MathF.Min(color.G, color.B));
        float diff = max - min;

        float v = max;
        float s = max == 0f ? 0f : diff / max;
        float h = 0f;

        if (diff != 0f)
        {
            if (max == color.R)
            {
                h = (color.G - color.B) / diff + (color.G < color.B ? 6f : 0f);
            }
            else if (max == color.G)
            {
                h = (color.B - color.R) / diff + 2f;
            }
            else
            {
                h = (color.R - color.G) / diff + 4f;
            }

            h *= 60f;
        }

        return new HsvColor(h, s, v, color.A);
    }

    /// <summary>Creates from CMYK.</summary>
    public static Rgba128Float FromCmyk(float c, float m, float y, float k, float a = 1f)
    {
        c = Clamp01(c);
        m = Clamp01(m);
        y = Clamp01(y);
        k = Clamp01(k);
        a = Clamp01(a);

        float invK = 1f - k;
        return new Rgba128Float((1f - c) * invK, (1f - m) * invK, (1f - y) * invK, a);
    }

    /// <summary>Converts to CMYK.</summary>
    public static CmykColor ToCmyk(Rgba128Float color)
    {
        float k = 1f - MathF.Max(color.R, MathF.Max(color.G, color.B));
        float invK = 1f - k;
        float c = k == 1f ? 0f : (1f - color.R - k) / invK;
        float m = k == 1f ? 0f : (1f - color.G - k) / invK;
        float y = k == 1f ? 0f : (1f - color.B - k) / invK;
        return new CmykColor(c, m, y, k, color.A);
    }

    /// <summary>Creates from CIEXYZ.</summary>
    public static Rgba128Float FromXyz(float x, float y, float z, float alpha = 1f)
    {
        float r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
        float g = x * -0.9689f + y * 1.8758f + z * 0.0415f;
        float b = x * 0.0557f + y * -0.204f + z * 1.057f;
        return new Rgba128Float(
            Math.Clamp(LinearToSrgb(r), 0f, 1f),
            Math.Clamp(LinearToSrgb(g), 0f, 1f),
            Math.Clamp(LinearToSrgb(b), 0f, 1f),
            Math.Clamp(alpha, 0f, 1f));
    }

    /// <summary>Converts to CIEXYZ.</summary>
    public static XyzColor ToXyz(Rgba128Float color)
    {
        float r = SrgbToLinear(color.R);
        float g = SrgbToLinear(color.G);
        float b = SrgbToLinear(color.B);

        float x = r * 0.4124564f + g * 0.3575761f + b * 0.1804375f;
        float y = r * 0.2126729f + g * 0.7151522f + b * 0.072175f;
        float z = r * 0.0193339f + g * 0.119192f + b * 0.9503041f;
        return new XyzColor(x, y, z, color.A);
    }

    private static float LinearToSrgb(float c) =>
        c <= 0.0031308f ? 12.92f * c : 1.055f * MathF.Pow(MathF.Max(c, 0f), 1f / 2.4f) - 0.055f;

    /// <summary>Converts to CIELAB (D65).</summary>
    public static LabColor ToLab(Rgba128Float color)
    {
        XyzColor xyz = ToXyz(color);

        float fx = xyz.X / D65X;
        float fy = xyz.Y / D65Y;
        float fz = xyz.Z / D65Z;

        fx = fx > 0.008856f ? MathF.Pow(fx, 1f / 3f) : (903.3f * fx + 16f) / 116f;
        fy = fy > 0.008856f ? MathF.Pow(fy, 1f / 3f) : (903.3f * fy + 16f) / 116f;
        fz = fz > 0.008856f ? MathF.Pow(fz, 1f / 3f) : (903.3f * fz + 16f) / 116f;

        return new LabColor(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz), color.A);
    }

    /// <summary>Creates from color temperature in Kelvin (Tanner Hellman).</summary>
    public static Rgba128Float FromTemperature(float kelvin, float alpha = 1f)
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

        return new Rgba128Float(
            Math.Clamp(r / 255f, 0f, 1f),
            Math.Clamp(g / 255f, 0f, 1f),
            Math.Clamp(b / 255f, 0f, 1f),
            Math.Clamp(alpha, 0f, 1f));
    }

    /// <summary>Tries a named color lookup (case-insensitive, whitespace ignored).</summary>
    public static bool TryGetNamedColor(string name, out Rgba32 color)
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

        return s_namedColors.TryGetValue(normalized.ToString(), out color);
    }

    /// <summary>Looks up a named color.</summary>
    public static Rgba32 GetNamedColor(string name)
    {
        if (!TryGetNamedColor(name, out Rgba32 color))
        {
            throw new KeyNotFoundException($"Unknown color name: '{name}'.");
        }

        return color;
    }

    /// <summary>Relative luminance (WCAG).</summary>
    public static float Luminance(Rgba128Float color)
    {
        float r = SrgbToLinear(color.R);
        float g = SrgbToLinear(color.G);
        float b = SrgbToLinear(color.B);
        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }

    /// <summary>WCAG contrast ratio.</summary>
    public static float ContrastRatio(Rgba128Float a, Rgba128Float b)
    {
        float lighter = MathF.Max(Luminance(a), Luminance(b));
        float darker = MathF.Min(Luminance(a), Luminance(b));
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    /// <summary>Euclidean distance in RGBA space.</summary>
    public static float Distance(Rgba128Float a, Rgba128Float b)
    {
        float dr = a.R - b.R;
        float dg = a.G - b.G;
        float db = a.B - b.B;
        float da = a.A - b.A;
        return MathF.Sqrt(dr * dr + dg * dg + db * db + da * da);
    }

    /// <summary>Euclidean distance in Lab space.</summary>
    public static float DistanceLab(Rgba128Float a, Rgba128Float b)
    {
        LabColor la = ToLab(a);
        LabColor lb = ToLab(b);
        float dl = la.L - lb.L;
        float da = la.A - lb.A;
        float db = la.B - lb.B;
        return MathF.Sqrt(dl * dl + da * da + db * db);
    }

    /// <summary>WCAG accessibility check.</summary>
    public static bool IsAccessible(Rgba128Float foreground, Rgba128Float background, AccessibilityLevel level = AccessibilityLevel.AA)
    {
        float ratio = ContrastRatio(foreground, background);
        return level == AccessibilityLevel.AA ? ratio >= 4.5f : ratio >= 7f;
    }

    /// <summary>Compares two colors by metric.</summary>
    public static int Compare(Rgba128Float a, Rgba128Float b, ColorComparisonMode mode = ColorComparisonMode.Luminance)
    {
        switch (mode)
        {
            case ColorComparisonMode.Luminance:
            {
                float lumA = Luminance(a);
                float lumB = Luminance(b);
                if (MathF.Abs(lumA - lumB) < Epsilon)
                {
                    return 0;
                }

                return lumA < lumB ? -1 : 1;
            }

            case ColorComparisonMode.Hue:
            {
                float hA = ToHsl(a).H;
                float hB = ToHsl(b).H;
                if (MathF.Abs(hA - hB) < Epsilon)
                {
                    return 0;
                }

                return hA < hB ? -1 : 1;
            }

            case ColorComparisonMode.Saturation:
            {
                float sA = ToHsl(a).S;
                float sB = ToHsl(b).S;
                if (MathF.Abs(sA - sB) < Epsilon)
                {
                    return 0;
                }

                return sA < sB ? -1 : 1;
            }

            case ColorComparisonMode.RgbDistance:
            {
                float distA = MathF.Sqrt(a.R * a.R + a.G * a.G + a.B * a.B);
                float distB = MathF.Sqrt(b.R * b.R + b.G * b.G + b.B * b.B);
                if (MathF.Abs(distA - distB) < Epsilon)
                {
                    return 0;
                }

                return distA < distB ? -1 : 1;
            }

            case ColorComparisonMode.LabDistance:
            {
                LabColor labA = ToLab(a);
                LabColor labB = ToLab(b);
                float distA = MathF.Sqrt(labA.L * labA.L + labA.A * labA.A + labA.B * labA.B);
                float distB = MathF.Sqrt(labB.L * labB.L + labB.A * labB.A + labB.B * labB.B);
                if (MathF.Abs(distA - distB) < Epsilon)
                {
                    return 0;
                }

                return distA < distB ? -1 : 1;
            }

            case ColorComparisonMode.Alpha:
                if (MathF.Abs(a.A - b.A) < Epsilon)
                {
                    return 0;
                }

                return a.A < b.A ? -1 : 1;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported color comparison mode.");
        }
    }

    /// <summary>Epsilon equality.</summary>
    public static bool ApproximatelyEquals(Rgba128Float a, Rgba128Float b, float epsilon = 1e-6f) =>
        MathF.Abs(a.R - b.R) < epsilon &&
        MathF.Abs(a.G - b.G) < epsilon &&
        MathF.Abs(a.B - b.B) < epsilon &&
        MathF.Abs(a.A - b.A) < epsilon;

    /// <summary>Generates a color harmony.</summary>
    public static Rgba128Float[] GenerateHarmony(Rgba128Float baseColor, ColorHarmonyType type, int count = 5)
    {
        HslColor hsl = ToHsl(baseColor);
        var colors = new List<Rgba128Float>();

        switch (type)
        {
            case ColorHarmonyType.Monochromatic:
                for (int i = 0; i < count; i++)
                {
                    float lightness = Math.Clamp(hsl.L + (i - 2) * 0.15f, 0f, 1f);
                    colors.Add(FromHsl(hsl.H, hsl.S, lightness, hsl.A));
                }

                break;

            case ColorHarmonyType.Complementary:
                colors.Add(baseColor);
                colors.Add(FromHsl(Mod(hsl.H + 180f, 360f), hsl.S, hsl.L, hsl.A));
                break;

            case ColorHarmonyType.SplitComplementary:
                colors.Add(baseColor);
                colors.Add(FromHsl(Mod(hsl.H + 150f, 360f), hsl.S, hsl.L, hsl.A));
                colors.Add(FromHsl(Mod(hsl.H + 210f, 360f), hsl.S, hsl.L, hsl.A));
                break;

            case ColorHarmonyType.Triadic:
                colors.Add(baseColor);
                colors.Add(FromHsl(Mod(hsl.H + 120f, 360f), hsl.S, hsl.L, hsl.A));
                colors.Add(FromHsl(Mod(hsl.H + 240f, 360f), hsl.S, hsl.L, hsl.A));
                break;

            case ColorHarmonyType.Tetradic:
                colors.Add(baseColor);
                colors.Add(FromHsl(Mod(hsl.H + 90f, 360f), hsl.S, hsl.L, hsl.A));
                colors.Add(FromHsl(Mod(hsl.H + 180f, 360f), hsl.S, hsl.L, hsl.A));
                colors.Add(FromHsl(Mod(hsl.H + 270f, 360f), hsl.S, hsl.L, hsl.A));
                break;

            case ColorHarmonyType.Analogous:
                for (int i = 0; i < count; i++)
                {
                    float hue = Mod(hsl.H + (i - 2) * 30f, 360f);
                    colors.Add(FromHsl(hue, hsl.S, hsl.L, hsl.A));
                }

                break;

            case ColorHarmonyType.Square:
                colors.Add(baseColor);
                colors.Add(FromHsl(Mod(hsl.H + 90f, 360f), hsl.S, hsl.L, hsl.A));
                colors.Add(FromHsl(Mod(hsl.H + 180f, 360f), hsl.S, hsl.L, hsl.A));
                colors.Add(FromHsl(Mod(hsl.H + 270f, 360f), hsl.S, hsl.L, hsl.A));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported harmony type.");
        }

        return [.. colors];
    }

    /// <summary>Random opaque color.</summary>
    public static Rgba128Float Random(float alpha = 1f) =>
        new(
            System.Random.Shared.NextSingle(),
            System.Random.Shared.NextSingle(),
            System.Random.Shared.NextSingle(),
            Math.Clamp(alpha, 0f, 1f));

    /// <summary>Random hue color.</summary>
    public static Rgba128Float RandomHue(float saturation = 1f, float lightness = 0.5f, float alpha = 1f) =>
        FromHsl(System.Random.Shared.NextSingle() * 360f, saturation, lightness, alpha);

    /// <summary>Random pastel color.</summary>
    public static Rgba128Float RandomPastel(float alpha = 1f) =>
        FromHsl(
            System.Random.Shared.NextSingle() * 360f,
            0.3f + System.Random.Shared.NextSingle() * 0.4f,
            0.7f + System.Random.Shared.NextSingle() * 0.3f,
            alpha);

    /// <summary>Random vibrant color.</summary>
    public static Rgba128Float RandomVibrant(float alpha = 1f) =>
        FromHsl(
            System.Random.Shared.NextSingle() * 360f,
            0.8f + System.Random.Shared.NextSingle() * 0.2f,
            0.4f + System.Random.Shared.NextSingle() * 0.3f,
            alpha);
}
