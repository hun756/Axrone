namespace Axrone.Shapes2D;

/// <summary>Color space interpolation policy.</summary>
public interface IColorSpacePolicy
{
    /// <summary>Policy color space.</summary>
    static abstract GradientColorSpace SpaceKind { get; }

    /// <summary>Interpolates two colors.</summary>
    static abstract ResolvedColor Interpolate(in ResolvedColor left, in ResolvedColor right, float factor);
}

/// <summary>Gradient spread policy.</summary>
public interface ISpreadPolicy
{
    /// <summary>Policy spread.</summary>
    static abstract GradientSpread SpreadKind { get; }

    /// <summary>Applies the spread to a value.</summary>
    static abstract float Apply(float value);
}

/// <summary>sRGB interpolation policy.</summary>
public readonly struct SrgbColorPolicy : IColorSpacePolicy
{
    /// <inheritdoc/>
    public static GradientColorSpace SpaceKind => GradientColorSpace.Srgb;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ResolvedColor Interpolate(in ResolvedColor left, in ResolvedColor right, float factor) =>
        ColorSpaces.LerpSrgb(left, right, factor);
}

/// <summary>Linear sRGB interpolation policy.</summary>
public readonly struct LinearSrgbColorPolicy : IColorSpacePolicy
{
    /// <inheritdoc/>
    public static GradientColorSpace SpaceKind => GradientColorSpace.LinearSrgb;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ResolvedColor Interpolate(in ResolvedColor left, in ResolvedColor right, float factor) =>
        ColorSpaces.LerpLinearSrgb(left, right, factor);
}

/// <summary>HSL interpolation policy.</summary>
public readonly struct HslColorPolicy : IColorSpacePolicy
{
    /// <inheritdoc/>
    public static GradientColorSpace SpaceKind => GradientColorSpace.Hsl;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ResolvedColor Interpolate(in ResolvedColor left, in ResolvedColor right, float factor) =>
        ColorSpaces.LerpHsl(left, right, factor);
}

/// <summary>Lab interpolation policy.</summary>
public readonly struct LabColorPolicy : IColorSpacePolicy
{
    /// <inheritdoc/>
    public static GradientColorSpace SpaceKind => GradientColorSpace.Lab;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ResolvedColor Interpolate(in ResolvedColor left, in ResolvedColor right, float factor) =>
        ColorSpaces.LerpLab(left, right, factor);
}

/// <summary>Pad spread policy.</summary>
public readonly struct PadSpreadPolicy : ISpreadPolicy
{
    /// <inheritdoc/>
    public static GradientSpread SpreadKind => GradientSpread.Pad;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Apply(float value) => Math.Clamp(value, 0f, 1f);
}

/// <summary>Repeat spread policy.</summary>
public readonly struct RepeatSpreadPolicy : ISpreadPolicy
{
    /// <inheritdoc/>
    public static GradientSpread SpreadKind => GradientSpread.Repeat;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Apply(float value)
    {
        float mod = value % 1f;
        return mod < 0f ? mod + 1f : mod;
    }
}

/// <summary>Reflect spread policy.</summary>
public readonly struct ReflectSpreadPolicy : ISpreadPolicy
{
    /// <inheritdoc/>
    public static GradientSpread SpreadKind => GradientSpread.Reflect;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Apply(float value)
    {
        float wrapped = MathF.Abs(value % 2f);
        return wrapped > 1f ? 2f - wrapped : wrapped;
    }
}

/// <summary>sRGB/linear/HSL/Lab conversion and interpolation helpers.</summary>
internal static class ColorSpaces
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SrgbToLinear(float value) =>
        value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearToSrgb(float value) =>
        value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(MathF.Max(value, 0f), 1f / 2.4f) - 0.055f;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ResolvedColor LerpLinearSrgb(in ResolvedColor left, in ResolvedColor right, float factor)
    {
        float lR = SrgbToLinear(left.R);
        float lG = SrgbToLinear(left.G);
        float lB = SrgbToLinear(left.B);
        float rR = SrgbToLinear(right.R);
        float rG = SrgbToLinear(right.G);
        float rB = SrgbToLinear(right.B);

        float linR = lR + (rR - lR) * factor;
        float linG = lG + (rG - lG) * factor;
        float linB = lB + (rB - lB) * factor;

        return new ResolvedColor(
            Math.Clamp(LinearToSrgb(linR), 0f, 1f),
            Math.Clamp(LinearToSrgb(linG), 0f, 1f),
            Math.Clamp(LinearToSrgb(linB), 0f, 1f),
            Math.Clamp(left.A + (right.A - left.A) * factor, 0f, 1f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ResolvedColor LerpHsl(in ResolvedColor left, in ResolvedColor right, float factor)
    {
        RgbToHsl(left, out float h1, out float s1, out float l1);
        RgbToHsl(right, out float h2, out float s2, out float l2);

        float dh = h2 - h1;
        if (dh > 180f)
        {
            dh -= 360f;
        }
        else if (dh < -180f)
        {
            dh += 360f;
        }

        float h = (h1 + dh * factor) % 360f;
        if (h < 0f)
        {
            h += 360f;
        }

        float s = Math.Clamp(s1 + (s2 - s1) * factor, 0f, 1f);
        float l = Math.Clamp(l1 + (l2 - l1) * factor, 0f, 1f);
        float a = Math.Clamp(left.A + (right.A - left.A) * factor, 0f, 1f);

        return HslToRgb(h, s, l, a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ResolvedColor LerpLab(in ResolvedColor left, in ResolvedColor right, float factor)
    {
        RgbToLab(left, out float l1, out float a1, out float b1);
        RgbToLab(right, out float l2, out float a2, out float b2);

        float l = l1 + (l2 - l1) * factor;
        float a = a1 + (a2 - a1) * factor;
        float b = b1 + (b2 - b1) * factor;
        float alpha = Math.Clamp(left.A + (right.A - left.A) * factor, 0f, 1f);

        return LabToRgb(l, a, b, alpha);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ResolvedColor LerpSrgb(in ResolvedColor left, in ResolvedColor right, float factor) => new(
        Math.Clamp(left.R + (right.R - left.R) * factor, 0f, 1f),
        Math.Clamp(left.G + (right.G - left.G) * factor, 0f, 1f),
        Math.Clamp(left.B + (right.B - left.B) * factor, 0f, 1f),
        Math.Clamp(left.A + (right.A - left.A) * factor, 0f, 1f));

    private static void RgbToHsl(in ResolvedColor c, out float h, out float s, out float l)
    {
        float max = MathF.Max(c.R, MathF.Max(c.G, c.B));
        float min = MathF.Min(c.R, MathF.Min(c.G, c.B));
        l = (max + min) * 0.5f;

        if (MathF.Abs(max - min) <= ShapeConstants.Epsilon)
        {
            h = 0f;
            s = 0f;
            return;
        }

        float d = max - min;
        s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

        if (max == c.R)
        {
            h = (c.G - c.B) / d + (c.G < c.B ? 6f : 0f);
        }
        else if (max == c.G)
        {
            h = (c.B - c.R) / d + 2f;
        }
        else
        {
            h = (c.R - c.G) / d + 4f;
        }

        h *= 60f;
    }

    private static ResolvedColor HslToRgb(float h, float s, float l, float a)
    {
        if (s <= ShapeConstants.Epsilon)
        {
            return new ResolvedColor(l, l, l, a);
        }

        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;
        float hk = h / 360f;

        float r = HueToRgb(p, q, hk + 1f / 3f);
        float g = HueToRgb(p, q, hk);
        float b = HueToRgb(p, q, hk - 1f / 3f);

        return new ResolvedColor(r, g, b, a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    private static void RgbToLab(in ResolvedColor c, out float l, out float a, out float b)
    {
        float r = SrgbToLinear(c.R);
        float g = SrgbToLinear(c.G);
        float bl = SrgbToLinear(c.B);

        float x = (r * 0.4124564f + g * 0.3575761f + bl * 0.1804375f) / 0.95047f;
        float y = (r * 0.2126729f + g * 0.7151522f + bl * 0.0721750f) / 1.00000f;
        float z = (r * 0.0193339f + g * 0.1191920f + bl * 0.9503041f) / 1.08883f;

        x = x > 0.008856f ? MathF.Pow(x, 1f / 3f) : (7.787f * x) + (16f / 116f);
        y = y > 0.008856f ? MathF.Pow(y, 1f / 3f) : (7.787f * y) + (16f / 116f);
        z = z > 0.008856f ? MathF.Pow(z, 1f / 3f) : (7.787f * z) + (16f / 116f);

        l = (116f * y) - 16f;
        a = 500f * (x - y);
        b = 200f * (y - z);
    }

    private static ResolvedColor LabToRgb(float l, float a, float b, float alpha)
    {
        float y = (l + 16f) / 116f;
        float x = a / 500f + y;
        float z = y - b / 200f;

        float x3 = MathF.Pow(x, 3f);
        float y3 = MathF.Pow(y, 3f);
        float z3 = MathF.Pow(z, 3f);

        float xRef = (x3 > 0.008856f ? x3 : (x - 16f / 116f) / 7.787f) * 0.95047f;
        float yRef = (y3 > 0.008856f ? y3 : (y - 16f / 116f) / 7.787f) * 1.00000f;
        float zRef = (z3 > 0.008856f ? z3 : (z - 16f / 116f) / 7.787f) * 1.08883f;

        float r = xRef * 3.2404542f + yRef * -1.5371385f + zRef * -0.4985314f;
        float g = xRef * -0.9692660f + yRef * 1.8760108f + zRef * 0.0415560f;
        float bl = xRef * 0.0556434f + yRef * -0.2040259f + zRef * 1.0572252f;

        return new ResolvedColor(
            Math.Clamp(LinearToSrgb(r), 0f, 1f),
            Math.Clamp(LinearToSrgb(g), 0f, 1f),
            Math.Clamp(LinearToSrgb(bl), 0f, 1f),
            alpha);
    }
}
