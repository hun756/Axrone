namespace Axrone.Numeric;

/// <summary>Color metrics, harmonies and random generation.</summary>
public readonly partial record struct Color
{
    /// <summary>Relative luminance (WCAG).</summary>
    public static float Luminance(Color color)
    {
        float r = SrgbToLinear(color.R);
        float g = SrgbToLinear(color.G);
        float b = SrgbToLinear(color.B);
        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }

    /// <summary>WCAG contrast ratio.</summary>
    public static float ContrastRatio(Color a, Color b)
    {
        float lumA = Luminance(a);
        float lumB = Luminance(b);
        float lighter = MathF.Max(lumA, lumB);
        float darker = MathF.Min(lumA, lumB);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    /// <summary>Euclidean distance in RGBA space.</summary>
    public static float Distance(Color a, Color b)
    {
        float dr = a.R - b.R;
        float dg = a.G - b.G;
        float db = a.B - b.B;
        float da = a.A - b.A;
        return MathF.Sqrt(dr * dr + dg * dg + db * db + da * da);
    }

    /// <summary>Euclidean distance in Lab space.</summary>
    public static float DistanceLab(Color a, Color b)
    {
        LabColor la = a.ToLab();
        LabColor lb = b.ToLab();
        float dl = la.L - lb.L;
        float da = la.A - lb.A;
        float db = la.B - lb.B;
        return MathF.Sqrt(dl * dl + da * da + db * db);
    }

    /// <summary>WCAG accessibility check.</summary>
    public static bool IsAccessible(Color foreground, Color background, AccessibilityLevel level = AccessibilityLevel.AA)
    {
        float ratio = ContrastRatio(foreground, background);
        return level == AccessibilityLevel.AA ? ratio >= 4.5f : ratio >= 7f;
    }

    /// <summary>Generates a color harmony.</summary>
    public static Color[] GenerateHarmony(Color baseColor, ColorHarmonyType type, int count = 5)
    {
        HslColor hsl = baseColor.ToHsl();
        var colors = new List<Color>();

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
    public static Color Random(float alpha = 1f) =>
        new(global::System.Random.Shared.NextSingle(), global::System.Random.Shared.NextSingle(), global::System.Random.Shared.NextSingle(), alpha);

    /// <summary>Random hue color.</summary>
    public static Color RandomHue(float saturation = 1f, float lightness = 0.5f, float alpha = 1f) =>
        FromHsl(global::System.Random.Shared.NextSingle() * 360f, saturation, lightness, alpha);

    /// <summary>Random pastel color.</summary>
    public static Color RandomPastel(float alpha = 1f) =>
        FromHsl(
            global::System.Random.Shared.NextSingle() * 360f,
            0.3f + global::System.Random.Shared.NextSingle() * 0.4f,
            0.7f + global::System.Random.Shared.NextSingle() * 0.3f,
            alpha);

    /// <summary>Random vibrant color.</summary>
    public static Color RandomVibrant(float alpha = 1f) =>
        FromHsl(
            global::System.Random.Shared.NextSingle() * 360f,
            0.8f + global::System.Random.Shared.NextSingle() * 0.2f,
            0.4f + global::System.Random.Shared.NextSingle() * 0.3f,
            alpha);
}

