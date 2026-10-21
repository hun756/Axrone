namespace Axrone.Numeric;

/// <summary>Inline sRGB expansion table (256 entries, zero per-call cost).</summary>
[InlineArray(256)]
internal struct SrgbExpansionBuffer
{
    private float _element;
}

/// <summary>sRGB expansion lookup.</summary>
internal static class SrgbExpansionTable
{
    private static readonly SrgbExpansionBuffer s_buffer = Build();

    private static SrgbExpansionBuffer Build()
    {
        Unsafe.SkipInit(out SrgbExpansionBuffer buffer);
        for (int i = 0; i < 256; i++)
        {
            float c = i * (1f / 255f);
            buffer[i] = c <= 0.04045f ? c * (1f / 12.92f) : MathF.Pow((c + 0.055f) * (1f / 1.055f), 2.4f);
        }

        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ref readonly float GetReference() => ref MemoryMarshal.GetReference((ReadOnlySpan<float>)s_buffer);
}

/// <summary>Zero-allocation color space conversion engine (Native AOT trim-safe).</summary>
public static class ColorSpaceConverter
{
    /// <summary>Converts sRGB bytes to linear.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float SrgbToLinearRgb(Rgba32 srgb)
    {
        ref readonly float tableRef = ref SrgbExpansionTable.GetReference();
        float r = Unsafe.Add(ref Unsafe.AsRef(in tableRef), (nuint)srgb.R);
        float g = Unsafe.Add(ref Unsafe.AsRef(in tableRef), (nuint)srgb.G);
        float b = Unsafe.Add(ref Unsafe.AsRef(in tableRef), (nuint)srgb.B);
        float a = srgb.A * (1f / 255f);
        return new Rgba128Float(r, g, b, a);
    }

    /// <summary>Converts linear to sRGB bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba32 LinearRgbToSrgb(Rgba128Float linear)
    {
        byte r = LinearChannelToSrgbByte(linear.R);
        byte g = LinearChannelToSrgbByte(linear.G);
        byte b = LinearChannelToSrgbByte(linear.B);
        byte a = (byte)Math.Clamp((int)MathF.Round(linear.A * 255f), 0, 255);
        return new Rgba32(r, g, b, a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static byte LinearChannelToSrgbByte(float value)
    {
        if (value <= 0f)
        {
            return 0;
        }

        if (value >= 1f)
        {
            return 255;
        }

        float s = value <= 0.0031308f
            ? value * 12.92f
            : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;

        return (byte)Math.Clamp((int)MathF.Round(s * 255f), 0, 255);
    }

    /// <summary>Converts linear to Oklab.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Oklab LinearRgbToOklab(Rgba128Float rgb)
    {
        float l = 0.4122214708f * rgb.R + 0.5363325363f * rgb.G + 0.0514459929f * rgb.B;
        float m = 0.2119034982f * rgb.R + 0.6806995451f * rgb.G + 0.1073969566f * rgb.B;
        float s = 0.0883024619f * rgb.R + 0.2817188376f * rgb.G + 0.6299787005f * rgb.B;

        float l_ = MathF.Cbrt(l);
        float m_ = MathF.Cbrt(m);
        float s_ = MathF.Cbrt(s);

        float lOut = 0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_;
        float aOut = 1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937096f * s_;
        float bOut = 0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_;
        return new Oklab(lOut, aOut, bOut, rgb.A);
    }

    /// <summary>Converts Oklab to linear.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float OklabToLinearRgb(Oklab oklab)
    {
        float l_ = oklab.L + 0.3963377774f * oklab.A + 0.2158037573f * oklab.B;
        float m_ = oklab.L - 0.1055613458f * oklab.A - 0.0638541728f * oklab.B;
        // Note: reference draft had 1.4914854809 here (digit transposition);
        // the correct Björn Ottosson inverse coefficient is 1.2914854809.
        float s_ = oklab.L - 0.0894841775f * oklab.A - 1.2914854809f * oklab.B;

        float l = l_ * l_ * l_;
        float m = m_ * m_ * m_;
        float s = s_ * s_ * s_;

        float r = 4.0767416621f * l - 3.3077115901f * m + 0.2309699292f * s;
        float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
        float b = -0.0041960863f * l - 0.2040259117f * m + 1.0572252f * s;
        return new Rgba128Float(r, g, b, oklab.Alpha);
    }

    /// <summary>Converts Oklab to Oklch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Oklch OklabToOklch(Oklab lab)
    {
        float c = MathF.Sqrt(lab.A * lab.A + lab.B * lab.B);
        float h = MathF.Atan2(lab.B, lab.A) * (180.0f / MathF.PI);
        if (h < 0f)
        {
            h += 360f;
        }

        return new Oklch(lab.L, c, h, lab.Alpha);
    }

    /// <summary>Converts Oklch to Oklab.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Oklab OklchToOklab(Oklch lch)
    {
        float hRad = lch.H * (MathF.PI / 180.0f);
        (float sin, float cos) = MathF.SinCos(hRad);
        return new Oklab(lch.L, lch.C * cos, lch.C * sin, lch.Alpha);
    }

    /// <summary>Converts sRGB bytes to Oklab.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Oklab SrgbToOklab(Rgba32 srgb) => LinearRgbToOklab(SrgbToLinearRgb(srgb));

    /// <summary>Converts Oklab to sRGB bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba32 OklabToSrgb(Oklab oklab) => LinearRgbToSrgb(OklabToLinearRgb(oklab));

    /// <summary>Oklab perceptual distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float DeltaE(Oklab left, Oklab right)
    {
        float dL = left.L - right.L;
        float da = left.A - right.A;
        float db = left.B - right.B;
        return MathF.Sqrt(dL * dL + da * da + db * db);
    }

    /// <summary>Relative luminance (WCAG).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float GetRelativeLuminance(Rgba128Float linear) =>
        0.2126f * linear.R + 0.7152f * linear.G + 0.0722f * linear.B;

    /// <summary>WCAG contrast ratio.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeContrastRatio(Rgba128Float foreground, Rgba128Float background)
    {
        float l1 = GetRelativeLuminance(foreground);
        float l2 = GetRelativeLuminance(background);

        if (l2 > l1)
        {
            float temp = l1;
            l1 = l2;
            l2 = temp;
        }

        return (l1 + 0.05f) / (l2 + 0.05f);
    }
}
