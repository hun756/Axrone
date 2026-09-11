namespace Axrone.Simd;

public static unsafe partial class SimdFloat64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void TransformLinear(ReadOnlySpan<double> source, double multiplier, double offset, Span<double> destination)
        => SimdFloatingPointOps<double>.TransformLinear(source, multiplier, offset, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorAdd(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSubtract(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorSubtract(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMultiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorMultiply(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorDivide(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorDivide(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorNegate(ReadOnlySpan<double> source, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorNegate(source, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAbs(ReadOnlySpan<double> source, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorAbs(source, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFma(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorFma(a, b, c, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFms(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorFms(a, b, c, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLerp(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double t, Span<double> destination)
        => SimdFloatingPointOps<double>.VectorLerp(a, b, t, destination);
}
