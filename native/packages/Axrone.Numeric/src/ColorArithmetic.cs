namespace Axrone.Numeric;

/// <summary>Arithmetic, interpolation and adjustments over linear colors.</summary>
public readonly partial struct Rgba128Float
{
    /// <summary>Adds two colors (clamped).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float operator +(Rgba128Float left, Rgba128Float right) => new(
        Math.Clamp(left.R + right.R, 0f, 1f),
        Math.Clamp(left.G + right.G, 0f, 1f),
        Math.Clamp(left.B + right.B, 0f, 1f),
        Math.Clamp(left.A + right.A, 0f, 1f));

    /// <summary>Subtracts two colors (clamped).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float operator -(Rgba128Float left, Rgba128Float right) => new(
        Math.Clamp(left.R - right.R, 0f, 1f),
        Math.Clamp(left.G - right.G, 0f, 1f),
        Math.Clamp(left.B - right.B, 0f, 1f),
        Math.Clamp(left.A - right.A, 0f, 1f));

    /// <summary>Multiplies two colors channel-wise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float operator *(Rgba128Float left, Rgba128Float right) =>
        new(left.R * right.R, left.G * right.G, left.B * right.B, left.A * right.A);

    /// <summary>Scales RGB channels, preserving alpha.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float operator *(Rgba128Float color, float scalar) => new(
        Math.Clamp(color.R * scalar, 0f, 1f),
        Math.Clamp(color.G * scalar, 0f, 1f),
        Math.Clamp(color.B * scalar, 0f, 1f),
        color.A);

    /// <summary>Scales RGB channels, preserving alpha.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float operator *(float scalar, Rgba128Float color) => color * scalar;

    /// <summary>Adds two colors (clamped).</summary>
    public static Rgba128Float Add(Rgba128Float a, Rgba128Float b) => a + b;

    /// <summary>Subtracts two colors (clamped).</summary>
    public static Rgba128Float Subtract(Rgba128Float a, Rgba128Float b) => a - b;

    /// <summary>Multiplies two colors channel-wise.</summary>
    public static Rgba128Float Multiply(Rgba128Float a, Rgba128Float b) => a * b;

    /// <summary>Scales RGB channels, preserving alpha.</summary>
    public static Rgba128Float MultiplyScalar(Rgba128Float a, float scalar) => a * scalar;

    /// <summary>Creates from an array slice.</summary>
    public static Rgba128Float FromArray(ReadOnlySpan<float> values, int offset = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (values.Length < offset + 3)
        {
            throw new ArgumentOutOfRangeException(nameof(values), values.Length, "Array must hold at least 3 elements at the offset.");
        }

        float a = values.Length >= offset + 4 ? values[offset + 3] : 1f;
        return new Rgba128Float(values[offset], values[offset + 1], values[offset + 2], a);
    }

    /// <summary>Linearly interpolates two colors.</summary>
    public static Rgba128Float Lerp(Rgba128Float a, Rgba128Float b, float t)
    {
        float t1 = Math.Clamp(t, 0f, 1f);
        return new Rgba128Float(
            a.R + (b.R - a.R) * t1,
            a.G + (b.G - a.G) * t1,
            a.B + (b.B - a.B) * t1,
            a.A + (b.A - a.A) * t1);
    }

    /// <summary>Interpolates through HSL space with hue wrapping.</summary>
    public static Rgba128Float LerpHsl(Rgba128Float a, Rgba128Float b, float t)
    {
        float t1 = Math.Clamp(t, 0f, 1f);
        HslColor ha = ColorOps.ToHsl(a);
        HslColor hb = ColorOps.ToHsl(b);

        float dh = hb.H - ha.H;
        if (dh > 180f)
        {
            dh -= 360f;
        }
        else if (dh < -180f)
        {
            dh += 360f;
        }

        return ColorOps.FromHsl(ha.H + dh * t1, ha.S + (hb.S - ha.S) * t1, ha.L + (hb.L - ha.L) * t1, ha.A + (hb.A - ha.A) * t1);
    }

    /// <summary>Interpolates through Lab space.</summary>
    public static Rgba128Float LerpLab(Rgba128Float a, Rgba128Float b, float t)
    {
        float t1 = Math.Clamp(t, 0f, 1f);
        LabColor la = ColorOps.ToLab(a);
        LabColor lb = ColorOps.ToLab(b);

        return ColorOps.FromLab(
            la.L + (lb.L - la.L) * t1,
            la.A + (lb.A - la.A) * t1,
            la.B + (lb.B - la.B) * t1,
            la.Alpha + (lb.Alpha - la.Alpha) * t1);
    }

    /// <summary>Lightens by adding to lightness.</summary>
    public Rgba128Float Lighten(float amount) => ColorOps.Lighten(this, amount);

    /// <summary>Darkens by subtracting from lightness.</summary>
    public Rgba128Float Darken(float amount) => ColorOps.Darken(this, amount);

    /// <summary>Increases saturation.</summary>
    public Rgba128Float Saturate(float amount) => ColorOps.Saturate(this, amount);

    /// <summary>Decreases saturation.</summary>
    public Rgba128Float Desaturate(float amount) => ColorOps.Desaturate(this, amount);

    /// <summary>Rotates hue by degrees.</summary>
    public Rgba128Float AdjustHue(float degrees) => ColorOps.AdjustHue(this, degrees);

    /// <summary>Inverts channels, preserving alpha.</summary>
    public Rgba128Float Invert() => ColorOps.Invert(this);

    /// <summary>Converts to luminance gray, preserving alpha.</summary>
    public Rgba128Float Grayscale() => ColorOps.Grayscale(this);

    /// <summary>Relative luminance (WCAG).</summary>
    public float Luminance() => ColorOps.Luminance(this);

    /// <summary>WCAG contrast ratio against another color.</summary>
    public float ContrastRatio(Rgba128Float other) => ColorOps.ContrastRatio(this, other);

    /// <summary>Euclidean distance in RGBA space.</summary>
    public float DistanceTo(Rgba128Float other) => ColorOps.Distance(this, other);
}
