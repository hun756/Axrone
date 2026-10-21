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

    /// <summary>Hue (identity fallback).</summary>
    Hue = 12,

    /// <summary>Saturation (identity fallback).</summary>
    Saturation = 13,

    /// <summary>Color (identity fallback).</summary>
    Color = 14,

    /// <summary>Luminosity (identity fallback).</summary>
    Luminosity = 15,
}

/// <summary>Premultiplied-over compositor.</summary>
public readonly struct PremultipliedOverCompositor : IVectorCompositor
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector512<float> Composite(Vector512<float> source, Vector512<float> destination)
    {
        Vector512<float> ones = Vector512.Create(1.0f);
        Vector512<int> shuffleMask = Vector512.Create(3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15);
        Vector512<float> sAlpha = Vector512.Shuffle(source, shuffleMask);
        return source + (destination * (ones - sAlpha));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector256<float> Composite(Vector256<float> source, Vector256<float> destination)
    {
        Vector256<float> ones = Vector256.Create(1.0f);
        Vector256<int> shuffleMask = Vector256.Create(3, 3, 3, 3, 7, 7, 7, 7);
        Vector256<float> sAlpha = Vector256.Shuffle(source, shuffleMask);
        return source + (destination * (ones - sAlpha));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector128<float> Composite(Vector128<float> source, Vector128<float> destination)
    {
        Vector128<float> ones = Vector128.Create(1.0f);
        Vector128<int> shuffleMask = Vector128.Create(3, 3, 3, 3);
        Vector128<float> sAlpha = Vector128.Shuffle(source, shuffleMask);
        return source + (destination * (ones - sAlpha));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float CompositeScalar(scoped in Rgba128Float source, scoped in Rgba128Float destination)
    {
        float invAlpha = 1.0f - source.A;
        return new Rgba128Float(
            source.R + destination.R * invAlpha,
            source.G + destination.G * invAlpha,
            source.B + destination.B * invAlpha,
            source.A + destination.A * invAlpha);
    }
}

/// <summary>Additive compositor.</summary>
public readonly struct AdditiveCompositor : IVectorCompositor
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector512<float> Composite(Vector512<float> source, Vector512<float> destination) => source + destination;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector256<float> Composite(Vector256<float> source, Vector256<float> destination) => source + destination;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector128<float> Composite(Vector128<float> source, Vector128<float> destination) => source + destination;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float CompositeScalar(scoped in Rgba128Float source, scoped in Rgba128Float destination) =>
        new(source.R + destination.R, source.G + destination.G, source.B + destination.B, source.A + destination.A);
}

/// <summary>Scalar blending and monomorphized vectorized compositing.</summary>
public static class ColorBlending
{
    /// <summary>Straight-alpha over.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float BlendStraightOver(Rgba128Float source, Rgba128Float destination)
    {
        float outAlpha = source.A + destination.A * (1.0f - source.A);
        if (outAlpha <= 1e-7f)
        {
            return new Rgba128Float(0.0f, 0.0f, 0.0f, 0.0f);
        }

        float invAlpha = 1.0f / outAlpha;
        float dstFactor = destination.A * (1.0f - source.A);
        float r = (source.R * source.A + destination.R * dstFactor) * invAlpha;
        float g = (source.G * source.A + destination.G * dstFactor) * invAlpha;
        float b = (source.B * source.A + destination.B * dstFactor) * invAlpha;

        return new Rgba128Float(r, g, b, outAlpha);
    }

    /// <summary>Premultiplied over.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float BlendPremultipliedOver(Rgba128Float source, Rgba128Float destination)
    {
        Vector128<float> s = source.AsVector128();
        Vector128<float> d = destination.AsVector128();
        Vector128<float> result = PremultipliedOverCompositor.Composite(s, d);
        return new Rgba128Float(result);
    }

    /// <summary>Premultiplies straight alpha.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float Premultiply(Rgba128Float color) =>
        new(color.R * color.A, color.G * color.A, color.B * color.A, color.A);

    /// <summary>Unpremultiplies.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float Unpremultiply(Rgba128Float color)
    {
        if (color.A <= 1e-7f)
        {
            return new Rgba128Float(0.0f, 0.0f, 0.0f, 0.0f);
        }

        float invAlpha = 1.0f / color.A;
        return new Rgba128Float(color.R * invAlpha, color.G * invAlpha, color.B * invAlpha, color.A);
    }

    /// <summary>Blends two colors with a mode.</summary>
    public static Rgba128Float Blend(Rgba128Float @base, Rgba128Float overlay, ColorBlendMode mode)
    {
        float BlendChannel(float a, float b) => mode switch
        {
            ColorBlendMode.Normal => b,
            ColorBlendMode.Multiply => a * b,
            ColorBlendMode.Screen => 1f - (1f - a) * (1f - b),
            ColorBlendMode.Overlay => a < 0.5f ? 2f * a * b : 1f - 2f * (1f - a) * (1f - b),
            ColorBlendMode.SoftLight => b < 0.5f
                ? 2f * a * b + a * a * (1f - 2f * b)
                : 2f * a * (1f - b) + MathF.Sqrt(MathF.Max(a, 0f)) * (2f * b - 1f),
            ColorBlendMode.HardLight => b < 0.5f ? 2f * a * b : 1f - 2f * (1f - a) * (1f - b),
            ColorBlendMode.ColorDodge => b >= 1f ? 1f : MathF.Min(1f, a / (1f - b)),
            ColorBlendMode.ColorBurn => b <= 0f ? 0f : MathF.Max(0f, 1f - (1f - a) / b),
            ColorBlendMode.Darken => MathF.Min(a, b),
            ColorBlendMode.Lighten => MathF.Max(a, b),
            ColorBlendMode.Difference => MathF.Abs(a - b),
            ColorBlendMode.Exclusion => a + b - 2f * a * b,
            ColorBlendMode.Hue or ColorBlendMode.Saturation or ColorBlendMode.Color or ColorBlendMode.Luminosity => b,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported blend mode."),
        };

        return new Rgba128Float(
            Math.Clamp(BlendChannel(@base.R, overlay.R), 0f, 1f),
            Math.Clamp(BlendChannel(@base.G, overlay.G), 0f, 1f),
            Math.Clamp(BlendChannel(@base.B, overlay.B), 0f, 1f),
            overlay.A);
    }

    /// <summary>Monomorphized vectorized batch compositing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void BatchComposite<TCompositor>(
        ReadOnlySpan<Rgba128Float> source,
        ReadOnlySpan<Rgba128Float> destination,
        Span<Rgba128Float> target)
        where TCompositor : struct, IVectorCompositor
    {
        int length = source.Length;
        if ((uint)destination.Length < (uint)length || (uint)target.Length < (uint)length)
        {
            ThrowHelper.ThrowArgumentException_BufferTooSmall();
        }

        ref Rgba128Float srcRef = ref MemoryMarshal.GetReference(source);
        ref Rgba128Float dstRef = ref MemoryMarshal.GetReference(destination);
        ref Rgba128Float tgtRef = ref MemoryMarshal.GetReference(target);
        nuint i = 0;
        nuint count = (nuint)length;

        if (Vector512.IsHardwareAccelerated && count >= 4)
        {
            nuint bound512 = count & ~3u;
            for (; i < bound512; i += 4)
            {
                Vector512<float> s = Unsafe.As<Rgba128Float, Vector512<float>>(ref Unsafe.Add(ref srcRef, i));
                Vector512<float> d = Unsafe.As<Rgba128Float, Vector512<float>>(ref Unsafe.Add(ref dstRef, i));
                Vector512<float> result = TCompositor.Composite(s, d);
                Unsafe.As<Rgba128Float, Vector512<float>>(ref Unsafe.Add(ref tgtRef, i)) = result;
            }
        }

        if (Vector256.IsHardwareAccelerated && (count - i) >= 2)
        {
            nuint bound256 = count & ~1u;
            for (; i < bound256; i += 2)
            {
                Vector256<float> s = Unsafe.As<Rgba128Float, Vector256<float>>(ref Unsafe.Add(ref srcRef, i));
                Vector256<float> d = Unsafe.As<Rgba128Float, Vector256<float>>(ref Unsafe.Add(ref dstRef, i));
                Vector256<float> result = TCompositor.Composite(s, d);
                Unsafe.As<Rgba128Float, Vector256<float>>(ref Unsafe.Add(ref tgtRef, i)) = result;
            }
        }

        if (Vector128.IsHardwareAccelerated && i < count)
        {
            for (; i < count; i++)
            {
                Vector128<float> s = Unsafe.As<Rgba128Float, Vector128<float>>(ref Unsafe.Add(ref srcRef, i));
                Vector128<float> d = Unsafe.As<Rgba128Float, Vector128<float>>(ref Unsafe.Add(ref dstRef, i));
                Vector128<float> result = TCompositor.Composite(s, d);
                Unsafe.As<Rgba128Float, Vector128<float>>(ref Unsafe.Add(ref tgtRef, i)) = result;
            }

            return;
        }

        for (; i < count; i++)
        {
            ref readonly Rgba128Float s = ref Unsafe.Add(ref srcRef, i);
            ref readonly Rgba128Float d = ref Unsafe.Add(ref dstRef, i);
            Unsafe.Add(ref tgtRef, i) = TCompositor.CompositeScalar(in s, in d);
        }
    }

    /// <summary>Batches premultiplied-over compositing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void BatchBlendPremultipliedOver(
        ReadOnlySpan<Rgba128Float> source,
        ReadOnlySpan<Rgba128Float> destination,
        Span<Rgba128Float> target) =>
        BatchComposite<PremultipliedOverCompositor>(source, destination, target);
}
