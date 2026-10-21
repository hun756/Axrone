namespace Axrone.Numeric;

/// <summary>Color arithmetic, interpolation, adjustments and blending.</summary>
public readonly partial record struct Color
{
    /// <summary>Adds two colors (clamped).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color operator +(Color left, Color right) => new(
        Math.Clamp(left.R + right.R, 0f, 1f),
        Math.Clamp(left.G + right.G, 0f, 1f),
        Math.Clamp(left.B + right.B, 0f, 1f),
        Math.Clamp(left.A + right.A, 0f, 1f));

    /// <summary>Subtracts two colors (clamped).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color operator -(Color left, Color right) => new(
        Math.Clamp(left.R - right.R, 0f, 1f),
        Math.Clamp(left.G - right.G, 0f, 1f),
        Math.Clamp(left.B - right.B, 0f, 1f),
        Math.Clamp(left.A - right.A, 0f, 1f));

    /// <summary>Multiplies two colors channel-wise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color operator *(Color left, Color right) =>
        new(left.R * right.R, left.G * right.G, left.B * right.B, left.A * right.A);

    /// <summary>Scales RGB channels, preserving alpha.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color operator *(Color color, float scalar) => new(
        Math.Clamp(color.R * scalar, 0f, 1f),
        Math.Clamp(color.G * scalar, 0f, 1f),
        Math.Clamp(color.B * scalar, 0f, 1f),
        color.A);

    /// <summary>Scales RGB channels, preserving alpha.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color operator *(float scalar, Color color) => color * scalar;

    /// <summary>Adds two colors (clamped).</summary>
    public static Color Add(Color a, Color b) => a + b;

    /// <summary>Subtracts two colors (clamped).</summary>
    public static Color Subtract(Color a, Color b) => a - b;

    /// <summary>Multiplies two colors channel-wise.</summary>
    public static Color Multiply(Color a, Color b) => a * b;

    /// <summary>Scales RGB channels, preserving alpha.</summary>
    public static Color MultiplyScalar(Color color, float scalar) => color * scalar;

    /// <summary>Linearly interpolates two colors.</summary>
    public static Color Lerp(Color a, Color b, float t)
    {
        float t1 = Math.Clamp(t, 0f, 1f);
        return new Color(
            a.R + (b.R - a.R) * t1,
            a.G + (b.G - a.G) * t1,
            a.B + (b.B - a.B) * t1,
            a.A + (b.A - a.A) * t1);
    }

    /// <summary>Interpolates through HSL space with hue wrapping.</summary>
    public static Color LerpHsl(Color a, Color b, float t)
    {
        float t1 = Math.Clamp(t, 0f, 1f);
        HslColor ha = a.ToHsl();
        HslColor hb = b.ToHsl();

        float dh = hb.H - ha.H;
        if (dh > 180f)
        {
            dh -= 360f;
        }
        else if (dh < -180f)
        {
            dh += 360f;
        }

        return FromHsl(
            Mod(ha.H + dh * t1, 360f),
            ha.S + (hb.S - ha.S) * t1,
            ha.L + (hb.L - ha.L) * t1,
            ha.A + (hb.A - ha.A) * t1);
    }

    /// <summary>Interpolates through Lab space.</summary>
    public static Color LerpLab(Color a, Color b, float t)
    {
        float t1 = Math.Clamp(t, 0f, 1f);
        LabColor la = a.ToLab();
        LabColor lb = b.ToLab();

        return FromLab(
            la.L + (lb.L - la.L) * t1,
            la.A + (lb.A - la.A) * t1,
            la.B + (lb.B - la.B) * t1,
            la.Alpha + (lb.Alpha - la.Alpha) * t1);
    }

    /// <summary>Lightens by adding to lightness.</summary>
    public Color Lighten(float amount)
    {
        HslColor hsl = ToHsl();
        return FromHsl(Mod(hsl.H, 360f), Math.Clamp(hsl.S, 0f, 1f), Math.Clamp(hsl.L + amount, 0f, 1f), Math.Clamp(hsl.A, 0f, 1f));
    }

    /// <summary>Darkens by subtracting from lightness.</summary>
    public Color Darken(float amount) => Lighten(-amount);

    /// <summary>Increases saturation.</summary>
    public Color Saturate(float amount)
    {
        HslColor hsl = ToHsl();
        return FromHsl(Mod(hsl.H, 360f), Math.Clamp(hsl.S + amount, 0f, 1f), Math.Clamp(hsl.L, 0f, 1f), Math.Clamp(hsl.A, 0f, 1f));
    }

    /// <summary>Decreases saturation.</summary>
    public Color Desaturate(float amount) => Saturate(-amount);

    /// <summary>Rotates hue by degrees.</summary>
    public Color AdjustHue(float degrees)
    {
        HslColor hsl = ToHsl();
        return FromHsl(Mod(hsl.H + degrees, 360f), Math.Clamp(hsl.S, 0f, 1f), Math.Clamp(hsl.L, 0f, 1f), Math.Clamp(hsl.A, 0f, 1f));
    }

    /// <summary>Inverts channels, preserving alpha.</summary>
    public Color Invert() => new(1f - R, 1f - G, 1f - B, A);

    /// <summary>Converts to luminance gray.</summary>
    public Color Grayscale()
    {
        float gray = 0.299f * R + 0.587f * G + 0.114f * B;
        return new Color(gray, gray, gray, A);
    }

    /// <summary>Blends two colors with a blend mode.</summary>
    public static Color Blend(Color baseColor, Color overlay, ColorBlendMode mode)
    {
        Func<float, float, float> blend = mode switch
        {
            ColorBlendMode.Normal => static (a, b) => b,
            ColorBlendMode.Multiply => static (a, b) => a * b,
            ColorBlendMode.Screen => static (a, b) => 1f - (1f - a) * (1f - b),
            ColorBlendMode.Overlay => static (a, b) => a < 0.5f ? 2f * a * b : 1f - 2f * (1f - a) * (1f - b),
            ColorBlendMode.SoftLight => static (a, b) => b < 0.5f
                ? 2f * a * b + a * a * (1f - 2f * b)
                : 2f * a * (1f - b) + MathF.Sqrt(a) * (2f * b - 1f),
            ColorBlendMode.HardLight => static (a, b) => b < 0.5f ? 2f * a * b : 1f - 2f * (1f - a) * (1f - b),
            ColorBlendMode.ColorDodge => static (a, b) => b == 1f ? 1f : MathF.Min(1f, a / (1f - b)),
            ColorBlendMode.ColorBurn => static (a, b) => b == 0f ? 0f : MathF.Max(0f, 1f - (1f - a) / b),
            ColorBlendMode.Darken => static (a, b) => MathF.Min(a, b),
            ColorBlendMode.Lighten => static (a, b) => MathF.Max(a, b),
            ColorBlendMode.Difference => static (a, b) => MathF.Abs(a - b),
            ColorBlendMode.Exclusion => static (a, b) => a + b - 2f * a * b,
            ColorBlendMode.Hue or ColorBlendMode.Saturation or ColorBlendMode.Color or ColorBlendMode.Luminosity => static (a, b) => b,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported blend mode."),
        };

        return new Color(blend(baseColor.R, overlay.R), blend(baseColor.G, overlay.G), blend(baseColor.B, overlay.B), overlay.A);
    }
}
