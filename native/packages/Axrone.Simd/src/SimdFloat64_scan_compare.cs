namespace Axrone.Simd;

public static unsafe partial class SimdFloat64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstGreaterThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloatingPointOps<double>.FindFirstGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountGreaterThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloatingPointOps<double>.CountGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterGreaterThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
        => SimdFloatingPointOps<double>.FilterGreaterThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstLessThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloatingPointOps<double>.FindFirstLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstEqual(ReadOnlySpan<double> source, double value)
        => SimdFloatingPointOps<double>.FindFirstEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountLessThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloatingPointOps<double>.CountLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountEqual(ReadOnlySpan<double> source, double value)
        => SimdFloatingPointOps<double>.CountEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterLessThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
        => SimdFloatingPointOps<double>.FilterLessThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterEqual(ReadOnlySpan<double> source, double value, Span<double> destination)
        => SimdFloatingPointOps<double>.FilterEqual(source, value, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<double> condition, ReadOnlySpan<double> trueValues, ReadOnlySpan<double> falseValues, Span<double> destination)
        => SimdFloatingPointOps<double>.Select(condition, trueValues, falseValues, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<double> condition, double trueValue, double falseValue, Span<double> destination)
        => SimdFloatingPointOps<double>.Select(condition, trueValue, falseValue, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.CompareLessThan(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.CompareGreaterThan(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.CompareEqual(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareNotEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.CompareNotEqual(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.CompareLessThanOrEqual(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloatingPointOps<double>.CompareGreaterThanOrEqual(left, right, destination);
}
