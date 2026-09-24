namespace Axrone.Simd;

/// <summary>AVX-512 tier policy: foundation, byte/word, vector lengths, 512-bit registers.</summary>
public readonly struct Avx512Policy : ISimdPolicy
{
    /// <inheritdoc/>
    public static FeatureBitmask256 RequiredFeatures =>
        FeatureBitmask256.Create(SimdFeature.Avx512F) |
        FeatureBitmask256.Create(SimdFeature.Avx512BW) |
        FeatureBitmask256.Create(SimdFeature.Avx512VL) |
        FeatureBitmask256.Create(SimdFeature.Vector512HardwareAccelerated);

    /// <inheritdoc/>
    public static SimdRegisterWidth MinimumWidth => SimdRegisterWidth.Bits512;

    /// <inheritdoc/>
    public static string PolicyIdentifier => "AVX512-BW-VL";
}

/// <summary>AVX2 tier policy.</summary>
public readonly struct Avx2Policy : ISimdPolicy
{
    /// <inheritdoc/>
    public static FeatureBitmask256 RequiredFeatures =>
        FeatureBitmask256.Create(SimdFeature.Avx2) |
        FeatureBitmask256.Create(SimdFeature.Vector256HardwareAccelerated);

    /// <inheritdoc/>
    public static SimdRegisterWidth MinimumWidth => SimdRegisterWidth.Bits256;

    /// <inheritdoc/>
    public static string PolicyIdentifier => "AVX2";
}

/// <summary>ARM64 AdvSIMD tier policy.</summary>
public readonly struct AdvSimdPolicy : ISimdPolicy
{
    /// <inheritdoc/>
    public static FeatureBitmask256 RequiredFeatures =>
        FeatureBitmask256.Create(SimdFeature.AdvSimd) |
        FeatureBitmask256.Create(SimdFeature.Vector128HardwareAccelerated);

    /// <inheritdoc/>
    public static SimdRegisterWidth MinimumWidth => SimdRegisterWidth.Bits128;

    /// <inheritdoc/>
    public static string PolicyIdentifier => "ARM64-AdvSIMD";
}

/// <summary>Portable 128-bit tier policy.</summary>
public readonly struct Vector128Policy : ISimdPolicy
{
    /// <inheritdoc/>
    public static FeatureBitmask256 RequiredFeatures =>
        FeatureBitmask256.Create(SimdFeature.Vector128HardwareAccelerated);

    /// <inheritdoc/>
    public static SimdRegisterWidth MinimumWidth => SimdRegisterWidth.Bits128;

    /// <inheritdoc/>
    public static string PolicyIdentifier => "Vector128";
}

/// <summary>Scalar fallback: always satisfied, no width.</summary>
public readonly struct ScalarPolicy : ISimdPolicy
{
    /// <inheritdoc/>
    public static FeatureBitmask256 RequiredFeatures => FeatureBitmask256.Empty;

    /// <inheritdoc/>
    public static SimdRegisterWidth MinimumWidth => SimdRegisterWidth.None;

    /// <inheritdoc/>
    public static string PolicyIdentifier => "Scalar";
}

/// <summary>One kernel, four tier entry points. The dispatcher picks the widest that holds.</summary>
public interface ISimdKernelStrategy<TElement, TContext, TResult>
    where TElement : unmanaged
    where TContext : allows ref struct
{
    /// <summary>AVX-512 implementation.</summary>
    TResult ExecuteAvx512(ReadOnlySpan<TElement> input, ref TContext context);

    /// <summary>AVX2 implementation.</summary>
    TResult ExecuteAvx2(ReadOnlySpan<TElement> input, ref TContext context);

    /// <summary>128-bit implementation.</summary>
    TResult ExecuteVector128(ReadOnlySpan<TElement> input, ref TContext context);

    /// <summary>Scalar implementation (always available, correctness baseline).</summary>
    TResult ExecuteScalar(ReadOnlySpan<TElement> input, ref TContext context);
}

/// <summary>Function-pointer dispatch table for the fastest dispatch shape.</summary>
public readonly unsafe struct SimdFunctionTable<TElement, TContext, TResult>
    where TElement : unmanaged
    where TContext : allows ref struct
{
    /// <summary>AVX-512 entry.</summary>
    public readonly delegate*<ReadOnlySpan<TElement>, ref TContext, TResult> Avx512;

    /// <summary>AVX2 entry.</summary>
    public readonly delegate*<ReadOnlySpan<TElement>, ref TContext, TResult> Avx2;

    /// <summary>128-bit entry.</summary>
    public readonly delegate*<ReadOnlySpan<TElement>, ref TContext, TResult> Vector128;

    /// <summary>Scalar entry.</summary>
    public readonly delegate*<ReadOnlySpan<TElement>, ref TContext, TResult> Scalar;

    /// <summary>Creates a table; every tier must be wired, scalar is the net.</summary>
    public SimdFunctionTable(
        delegate*<ReadOnlySpan<TElement>, ref TContext, TResult> avx512,
        delegate*<ReadOnlySpan<TElement>, ref TContext, TResult> avx2,
        delegate*<ReadOnlySpan<TElement>, ref TContext, TResult> vector128,
        delegate*<ReadOnlySpan<TElement>, ref TContext, TResult> scalar)
    {
        Avx512 = avx512;
        Avx2 = avx2;
        Vector128 = vector128;
        Scalar = scalar;
    }
}

/// <summary>Tier dispatcher: widest satisfied policy wins, every choice recorded.</summary>
public static class SpecializedDispatcher
{
    /// <summary>Dispatches through a kernel strategy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TResult Dispatch<TElement, TContext, TResult, TKernel>(
        ReadOnlySpan<TElement> source,
        ref TContext context,
        TKernel kernel)
        where TElement : unmanaged
        where TContext : allows ref struct
        where TKernel : struct, ISimdKernelStrategy<TElement, TContext, TResult>
    {
        ref readonly SimdCapabilities caps = ref SimdRuntime.Capabilities;

        if (caps.SatisfiesPolicy<Avx512Policy>())
        {
            SimdRuntime.RecordDispatch(SimdDispatchTier.Avx512);
            return kernel.ExecuteAvx512(source, ref context);
        }

        if (caps.SatisfiesPolicy<Avx2Policy>())
        {
            SimdRuntime.RecordDispatch(SimdDispatchTier.Avx2);
            return kernel.ExecuteAvx2(source, ref context);
        }

        if (caps.SatisfiesPolicy<Vector128Policy>() || caps.SatisfiesPolicy<AdvSimdPolicy>())
        {
            SimdRuntime.RecordDispatch(SimdDispatchTier.Vector128);
            return kernel.ExecuteVector128(source, ref context);
        }

        SimdRuntime.RecordDispatch(SimdDispatchTier.Scalar);
        return kernel.ExecuteScalar(source, ref context);
    }

    /// <summary>Dispatches through a function-pointer table (no generic dispatch cost).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe TResult DispatchFast<TElement, TContext, TResult>(
        ReadOnlySpan<TElement> source,
        ref TContext context,
        in SimdFunctionTable<TElement, TContext, TResult> table)
        where TElement : unmanaged
        where TContext : allows ref struct
    {
        ref readonly SimdCapabilities caps = ref SimdRuntime.Capabilities;

        if (caps.SatisfiesPolicy<Avx512Policy>())
        {
            SimdRuntime.RecordDispatch(SimdDispatchTier.Avx512);
            return table.Avx512(source, ref context);
        }

        if (caps.SatisfiesPolicy<Avx2Policy>())
        {
            SimdRuntime.RecordDispatch(SimdDispatchTier.Avx2);
            return table.Avx2(source, ref context);
        }

        if (caps.SatisfiesPolicy<Vector128Policy>() || caps.SatisfiesPolicy<AdvSimdPolicy>())
        {
            SimdRuntime.RecordDispatch(SimdDispatchTier.Vector128);
            return table.Vector128(source, ref context);
        }

        SimdRuntime.RecordDispatch(SimdDispatchTier.Scalar);
        return table.Scalar(source, ref context);
    }
}
