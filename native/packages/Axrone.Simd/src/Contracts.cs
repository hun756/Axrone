namespace Axrone.Simd;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct ExtremaPair<T>(T Min, T Max) where T : struct;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ScanResult
{
    public OperationStatus Status { get; }
    public nuint WrittenCount { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ScanResult(OperationStatus status, nuint writtenCount)
    {
        Status = status;
        WrittenCount = writtenCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScanResult Success(nuint count) => new(OperationStatus.Done, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScanResult Overflow(nuint count) => new(OperationStatus.DestinationTooSmall, count);
}

public interface IVectorTransformer<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void TransformLinear(ReadOnlySpan<T> source, T multiplier, T offset, Span<T> destination);
    void VectorAdd(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void VectorMultiply(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void VectorFma(ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c, Span<T> destination);
    void VectorClamp(ReadOnlySpan<T> source, T min, T max, Span<T> destination);
    void VectorSubtract(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void VectorNegate(ReadOnlySpan<T> source, Span<T> destination);
    void VectorDivide(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void VectorAbs(ReadOnlySpan<T> source, Span<T> destination);
    void VectorFms(ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c, Span<T> destination);
    void VectorLerp(ReadOnlySpan<T> a, ReadOnlySpan<T> b, T t, Span<T> destination);
    void ElementWiseMax(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void ElementWiseMin(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
}

public interface IVectorReducer<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    T ComputeSum(ReadOnlySpan<T> source);
    T ComputeDotProduct(ReadOnlySpan<T> left, ReadOnlySpan<T> right);
    T ComputeL2Norm(ReadOnlySpan<T> source);
    ExtremaPair<T> ComputeExtrema(ReadOnlySpan<T> source);
    T ComputeMean(ReadOnlySpan<T> source);
    T ComputeVariance(ReadOnlySpan<T> source);
    T ComputeStdDev(ReadOnlySpan<T> source);
    T ComputeSumOfSquares(ReadOnlySpan<T> source);
    T ComputeProduct(ReadOnlySpan<T> source);
    T ComputeL1Norm(ReadOnlySpan<T> source);
    T ComputeLinfNorm(ReadOnlySpan<T> source);
}

public interface IVectorScanner<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    int FindFirstGreaterThan(ReadOnlySpan<T> source, T threshold);
    nuint CountGreaterThan(ReadOnlySpan<T> source, T threshold);
    ScanResult FilterGreaterThan(ReadOnlySpan<T> source, T threshold, Span<T> destination);
    int FindFirstLessThan(ReadOnlySpan<T> source, T threshold);
    int FindFirstEqual(ReadOnlySpan<T> source, T value);
    nuint CountLessThan(ReadOnlySpan<T> source, T threshold);
    nuint CountEqual(ReadOnlySpan<T> source, T value);
    ScanResult FilterLessThan(ReadOnlySpan<T> source, T threshold, Span<T> destination);
    ScanResult FilterEqual(ReadOnlySpan<T> source, T value, Span<T> destination);
    nuint ComputeArgMin(ReadOnlySpan<T> source);
    nuint ComputeArgMax(ReadOnlySpan<T> source);
}

public interface IVectorMaskOps<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void Select(ReadOnlySpan<T> condition, ReadOnlySpan<T> trueValues, ReadOnlySpan<T> falseValues, Span<T> destination);
    void Select(ReadOnlySpan<T> condition, T trueValue, T falseValue, Span<T> destination);
    void CompareLessThan(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareGreaterThan(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareLessThanOrEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareGreaterThanOrEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareNotEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
}

public interface IVectorFillOps<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void Fill(Span<T> destination, T value);
    void FillLinear(Span<T> destination, T start, T step);
}

public interface IVectorGatherOps<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void Gather(ReadOnlySpan<T> source, ReadOnlySpan<int> indices, Span<T> destination);
    void Scatter(ReadOnlySpan<T> source, ReadOnlySpan<int> indices, Span<T> destination);
}

public interface IVectorMathOps<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void VectorExp(ReadOnlySpan<T> source, Span<T> destination);
    void VectorLog(ReadOnlySpan<T> source, Span<T> destination);
    void VectorPow(ReadOnlySpan<T> @base, ReadOnlySpan<T> exponent, Span<T> destination);
    void VectorSigmoid(ReadOnlySpan<T> source, Span<T> destination);
    void VectorTanh(ReadOnlySpan<T> source, Span<T> destination);
    void VectorReLU(ReadOnlySpan<T> source, Span<T> destination);
    void Softmax(ReadOnlySpan<T> source, Span<T> destination);
    void VectorRSqrt(ReadOnlySpan<T> source, Span<T> destination);
}
