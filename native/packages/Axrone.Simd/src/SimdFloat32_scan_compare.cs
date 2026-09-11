namespace Axrone.Simd;

public static unsafe partial class SimdFloat32
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstGreaterThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloatingPointOps<float>.FindFirstGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountGreaterThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloatingPointOps<float>.CountGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterGreaterThan(ReadOnlySpan<float> source, float threshold, Span<float> destination)
        => SimdFloatingPointOps<float>.FilterGreaterThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstLessThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloatingPointOps<float>.FindFirstLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountLessThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloatingPointOps<float>.CountLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterLessThan(ReadOnlySpan<float> source, float threshold, Span<float> destination)
        => SimdFloatingPointOps<float>.FilterLessThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstEqual(ReadOnlySpan<float> source, float value)
        => SimdFloatingPointOps<float>.FindFirstEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountEqual(ReadOnlySpan<float> source, float value)
        => SimdFloatingPointOps<float>.CountEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterEqual(ReadOnlySpan<float> source, float value, Span<float> destination)
        => SimdFloatingPointOps<float>.FilterEqual(source, value, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<float> condition, ReadOnlySpan<float> trueValues, ReadOnlySpan<float> falseValues, Span<float> destination)
        => SimdFloatingPointOps<float>.Select(condition, trueValues, falseValues, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<float> condition, float trueValue, float falseValue, Span<float> destination)
        => SimdFloatingPointOps<float>.Select(condition, trueValue, falseValue, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.CompareLessThan(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.CompareGreaterThan(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.CompareEqual(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareNotEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.CompareNotEqual(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThanOrEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.CompareLessThanOrEqual(left, right, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThanOrEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloatingPointOps<float>.CompareGreaterThanOrEqual(left, right, destination);
}
