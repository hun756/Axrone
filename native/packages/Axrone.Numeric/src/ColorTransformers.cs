namespace Axrone.Numeric;

/// <summary>sRGB to linear transformer.</summary>
public readonly struct SrgbToLinearTransformer : IColorTransformer<Rgba32, Rgba128Float>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float Transform(scoped in Rgba32 source) => ColorSpaceConverter.SrgbToLinearRgb(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void TransformBatch(ReadOnlySpan<Rgba32> source, Span<Rgba128Float> destination)
    {
        if ((uint)destination.Length < (uint)source.Length)
        {
            ThrowHelper.ThrowArgumentException_BufferTooSmall();
        }

        ref Rgba32 srcRef = ref MemoryMarshal.GetReference(source);
        ref Rgba128Float dstRef = ref MemoryMarshal.GetReference(destination);
        ref readonly float tableRef = ref SrgbExpansionTable.GetReference();
        nuint count = (nuint)source.Length;

        for (nuint i = 0; i < count; i++)
        {
            ref readonly Rgba32 s = ref Unsafe.Add(ref srcRef, i);
            float r = Unsafe.Add(ref Unsafe.AsRef(in tableRef), s.R);
            float g = Unsafe.Add(ref Unsafe.AsRef(in tableRef), s.G);
            float b = Unsafe.Add(ref Unsafe.AsRef(in tableRef), s.B);
            float a = s.A * (1f / 255f);
            Unsafe.Add(ref dstRef, i) = new Rgba128Float(r, g, b, a);
        }
    }
}

/// <summary>Linear to sRGB transformer.</summary>
public readonly struct LinearToSrgbTransformer : IColorTransformer<Rgba128Float, Rgba32>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba32 Transform(scoped in Rgba128Float source) => ColorSpaceConverter.LinearRgbToSrgb(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void TransformBatch(ReadOnlySpan<Rgba128Float> source, Span<Rgba32> destination)
    {
        if ((uint)destination.Length < (uint)source.Length)
        {
            ThrowHelper.ThrowArgumentException_BufferTooSmall();
        }

        ref Rgba128Float srcRef = ref MemoryMarshal.GetReference(source);
        ref Rgba32 dstRef = ref MemoryMarshal.GetReference(destination);
        nuint count = (nuint)source.Length;

        for (nuint i = 0; i < count; i++)
        {
            ref readonly Rgba128Float s = ref Unsafe.Add(ref srcRef, i);
            Unsafe.Add(ref dstRef, i) = ColorSpaceConverter.LinearRgbToSrgb(s);
        }
    }
}

/// <summary>Linear to Oklab transformer.</summary>
public readonly struct LinearToOklabTransformer : IColorTransformer<Rgba128Float, Oklab>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Oklab Transform(scoped in Rgba128Float source) => ColorSpaceConverter.LinearRgbToOklab(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void TransformBatch(ReadOnlySpan<Rgba128Float> source, Span<Oklab> destination)
    {
        if ((uint)destination.Length < (uint)source.Length)
        {
            ThrowHelper.ThrowArgumentException_BufferTooSmall();
        }

        ref Rgba128Float srcRef = ref MemoryMarshal.GetReference(source);
        ref Oklab dstRef = ref MemoryMarshal.GetReference(destination);
        nuint count = (nuint)source.Length;

        for (nuint i = 0; i < count; i++)
        {
            Unsafe.Add(ref dstRef, i) = ColorSpaceConverter.LinearRgbToOklab(Unsafe.Add(ref srcRef, i));
        }
    }
}

/// <summary>Oklab to linear transformer.</summary>
public readonly struct OklabToLinearTransformer : IColorTransformer<Oklab, Rgba128Float>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rgba128Float Transform(scoped in Oklab source) => ColorSpaceConverter.OklabToLinearRgb(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void TransformBatch(ReadOnlySpan<Oklab> source, Span<Rgba128Float> destination)
    {
        if ((uint)destination.Length < (uint)source.Length)
        {
            ThrowHelper.ThrowArgumentException_BufferTooSmall();
        }

        ref Oklab srcRef = ref MemoryMarshal.GetReference(source);
        ref Rgba128Float dstRef = ref MemoryMarshal.GetReference(destination);
        nuint count = (nuint)source.Length;

        for (nuint i = 0; i < count; i++)
        {
            Unsafe.Add(ref dstRef, i) = ColorSpaceConverter.OklabToLinearRgb(Unsafe.Add(ref srcRef, i));
        }
    }
}
