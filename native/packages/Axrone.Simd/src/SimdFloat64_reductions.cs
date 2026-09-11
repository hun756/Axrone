namespace Axrone.Simd;

public static unsafe partial class SimdFloat64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorClamp(ReadOnlySpan<double> source, double min, double max, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorClamp(source, min, max, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMax(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.ElementWiseMax(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMin(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.ElementWiseMin(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeSum(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeSum(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeProduct(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeProduct(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeSumOfSquares(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeSumOfSquares(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeMean(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeMean(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeVariance(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeVariance(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeStdDev(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeStdDev(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeDotProduct(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
        => SimdFloatingPointOps<double>.ComputeDotProduct(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeL2Norm(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeL2Norm(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeL1Norm(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeL1Norm(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeLinfNorm(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeLinfNorm(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ExtremaPair<double> ComputeExtrema(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeExtrema(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMin(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeArgMin(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMax(ReadOnlySpan<double> source)
        => SimdFloatingPointOps<double>.ComputeArgMax(source);
}
