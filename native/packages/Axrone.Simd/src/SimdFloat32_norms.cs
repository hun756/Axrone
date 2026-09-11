namespace Axrone.Simd;

public static unsafe partial class SimdFloat32
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeDotProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
        => SimdFloatingPointOps<float>.ComputeDotProduct(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeL2Norm(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeL2Norm(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ExtremaPair<float> ComputeExtrema(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeExtrema(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeL1Norm(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeL1Norm(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeLinfNorm(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeLinfNorm(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMin(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeArgMin(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMax(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeArgMax(source);
}
