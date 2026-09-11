namespace Axrone.Simd;

public static unsafe partial class SimdFloat32
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorAdd(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSubtract(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorSubtract(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMultiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorMultiply(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorDivide(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorDivide(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorNegate(ReadOnlySpan<float> source, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorNegate(source, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAbs(ReadOnlySpan<float> source, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorAbs(source, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void TransformLinear(ReadOnlySpan<float> source, float multiplier, float offset, Span<float> destination)
        => SimdFloatingPointOps<float>.TransformLinear(source, multiplier, offset, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFma(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorFma(a, b, c, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFms(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorFms(a, b, c, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLerp(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float t, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorLerp(a, b, t, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorClamp(ReadOnlySpan<float> source, float min, float max, Span<float> destination)
        => SimdFloatingPointOps<float>.VectorClamp(source, min, max, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeSum(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeSum(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMax(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.ElementWiseMax(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMin(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.ElementWiseMin(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeMean(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeMean(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeVariance(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeVariance(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeStdDev(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeStdDev(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeSumOfSquares(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeSumOfSquares(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeProduct(ReadOnlySpan<float> source)
        => SimdFloatingPointOps<float>.ComputeProduct(source);
}
