#pragma warning disable CA1822 // Members that don't access instance data should be static — these implement interfaces

namespace Axrone.Simd;

public sealed unsafe class AlignedCounter : IDisposable
{
    private long* _value;
    private int _disposed;

    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.Read(ref *_value);
    }

    public AlignedCounter(long initialValue = 0)
    {
        _value = (long*)NativeMemory.AlignedAlloc(128, 128);
        NativeMemory.Clear(_value, 128);
        *_value = initialValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Increment() => Interlocked.Increment(ref *_value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Add(long value) => Interlocked.Add(ref *_value, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareExchange(long expected, long next) => Interlocked.CompareExchange(ref *_value, next, expected) == expected;

    ~AlignedCounter()
    {
        Release();
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            long* ptr = _value;
            _value = null;
            if (ptr != null)
            {
                NativeMemory.AlignedFree(ptr);
            }
        }
    }
}

public sealed class Float32VectorEngine : IVectorTransformer<float>, IVectorReducer<float>, IVectorScanner<float>, IDisposable
{
    private readonly AlignedCounter _transformsExecuted = new();
    private readonly AlignedCounter _reductionsExecuted = new();

    public long TransformsExecuted => _transformsExecuted.Value;
    public long ReductionsExecuted => _reductionsExecuted.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void TransformLinear(ReadOnlySpan<float> source, float multiplier, float offset, Span<float> destination)
    {
        SimdFloat32.TransformLinear(source, multiplier, offset, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorAdd(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        SimdFloat32.VectorAdd(left, right, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorMultiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        SimdFloat32.VectorMultiply(left, right, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorFma(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
    {
        SimdFloat32.VectorFma(a, b, c, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorClamp(ReadOnlySpan<float> source, float min, float max, Span<float> destination)
    {
        SimdFloat32.VectorClamp(source, min, max, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeSum(ReadOnlySpan<float> source)
    {
        float result = SimdFloat32.ComputeSum(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeDotProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        float result = SimdFloat32.ComputeDotProduct(left, right);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeL2Norm(ReadOnlySpan<float> source)
    {
        float result = SimdFloat32.ComputeL2Norm(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ExtremaPair<float> ComputeExtrema(ReadOnlySpan<float> source)
    {
        ExtremaPair<float> result = SimdFloat32.ComputeExtrema(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstGreaterThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloat32.FindFirstGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountGreaterThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloat32.CountGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterGreaterThan(ReadOnlySpan<float> source, float threshold, Span<float> destination)
        => SimdFloat32.FilterGreaterThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorSubtract(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    { SimdFloat32.VectorSubtract(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorNegate(ReadOnlySpan<float> source, Span<float> destination)
    { SimdFloat32.VectorNegate(source, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorDivide(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    { SimdFloat32.VectorDivide(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorAbs(ReadOnlySpan<float> source, Span<float> destination)
    { SimdFloat32.VectorAbs(source, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorFms(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
    { SimdFloat32.VectorFms(a, b, c, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorLerp(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float t, Span<float> destination)
    { SimdFloat32.VectorLerp(a, b, t, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ElementWiseMax(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    { SimdFloat32.ElementWiseMax(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ElementWiseMin(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    { SimdFloat32.ElementWiseMin(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeMean(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeMean(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeVariance(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeVariance(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeStdDev(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeStdDev(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeSumOfSquares(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeSumOfSquares(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeProduct(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeProduct(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeL1Norm(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeL1Norm(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeLinfNorm(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeLinfNorm(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstLessThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloat32.FindFirstLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstEqual(ReadOnlySpan<float> source, float value)
        => SimdFloat32.FindFirstEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountLessThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloat32.CountLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountEqual(ReadOnlySpan<float> source, float value)
        => SimdFloat32.CountEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterLessThan(ReadOnlySpan<float> source, float threshold, Span<float> destination)
        => SimdFloat32.FilterLessThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterEqual(ReadOnlySpan<float> source, float value, Span<float> destination)
        => SimdFloat32.FilterEqual(source, value, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint ComputeArgMin(ReadOnlySpan<float> source)
        => SimdFloat32.ComputeArgMin(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint ComputeArgMax(ReadOnlySpan<float> source)
        => SimdFloat32.ComputeArgMax(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Select(ReadOnlySpan<float> condition, ReadOnlySpan<float> trueValues, ReadOnlySpan<float> falseValues, Span<float> destination)
        => SimdFloat32.Select(condition, trueValues, falseValues, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Select(ReadOnlySpan<float> condition, float trueValue, float falseValue, Span<float> destination)
        => SimdFloat32.Select(condition, trueValue, falseValue, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareLessThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareLessThan(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareGreaterThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareGreaterThan(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareNotEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareNotEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareLessThanOrEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareLessThanOrEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareGreaterThanOrEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareGreaterThanOrEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Fill(Span<float> destination, float value) => SimdFloat32.Fill(destination, value);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void FillLinear(Span<float> destination, float start, float step) => SimdFloat32.FillLinear(destination, start, step);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Gather(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
        => SimdFloat32.Gather(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Scatter(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
        => SimdFloat32.Scatter(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorExp(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorExp(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorLog(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorLog(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorSigmoid(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorSigmoid(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorTanh(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorTanh(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorReLU(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorReLU(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorRSqrt(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorRSqrt(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorPow(ReadOnlySpan<float> b, ReadOnlySpan<float> e, Span<float> d) => SimdFloat32.VectorPow(b, e, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Softmax(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.Softmax(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Mat4x4Multiply(ReadOnlySpan<float> l, ReadOnlySpan<float> r, Span<float> d) => SimdFloat32.Mat4x4Multiply(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CrossProduct(ReadOnlySpan<float> l, ReadOnlySpan<float> r, Span<float> d) => SimdFloat32.CrossProduct(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QuaternionMultiply(ReadOnlySpan<float> l, ReadOnlySpan<float> r, Span<float> d) => SimdFloat32.QuaternionMultiply(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QuaternionSlerp(ReadOnlySpan<float> f, ReadOnlySpan<float> t, float tVal, Span<float> d) => SimdFloat32.QuaternionSlerp(f, t, tVal, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Swizzle(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination) => SimdFloat32.Swizzle(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void UnpackHalfToFloat(ReadOnlySpan<byte> source, Span<float> destination) => SimdFloat32.UnpackHalfToFloat(source, destination);

    public void Dispose()
    {
        _transformsExecuted.Dispose();
        _reductionsExecuted.Dispose();
    }
}

public sealed class Float64VectorEngine : IVectorTransformer<double>, IVectorReducer<double>, IVectorScanner<double>, IDisposable
{
    private readonly AlignedCounter _transformsExecuted = new();
    private readonly AlignedCounter _reductionsExecuted = new();

    public long TransformsExecuted => _transformsExecuted.Value;
    public long ReductionsExecuted => _reductionsExecuted.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void TransformLinear(ReadOnlySpan<double> source, double multiplier, double offset, Span<double> destination)
    {
        SimdFloat64.TransformLinear(source, multiplier, offset, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorAdd(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        SimdFloat64.VectorAdd(left, right, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorMultiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        SimdFloat64.VectorMultiply(left, right, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorFma(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
    {
        SimdFloat64.VectorFma(a, b, c, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorClamp(ReadOnlySpan<double> source, double min, double max, Span<double> destination)
    {
        SimdFloat64.VectorClamp(source, min, max, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeSum(ReadOnlySpan<double> source)
    {
        double result = SimdFloat64.ComputeSum(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeDotProduct(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
    {
        double result = SimdFloat64.ComputeDotProduct(left, right);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeL2Norm(ReadOnlySpan<double> source)
    {
        double result = SimdFloat64.ComputeL2Norm(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ExtremaPair<double> ComputeExtrema(ReadOnlySpan<double> source)
    {
        ExtremaPair<double> result = SimdFloat64.ComputeExtrema(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstGreaterThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloat64.FindFirstGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountGreaterThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloat64.CountGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterGreaterThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
        => SimdFloat64.FilterGreaterThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorSubtract(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    { SimdFloat64.VectorSubtract(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorNegate(ReadOnlySpan<double> source, Span<double> destination)
    { SimdFloat64.VectorNegate(source, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorDivide(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    { SimdFloat64.VectorDivide(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorAbs(ReadOnlySpan<double> source, Span<double> destination)
    { SimdFloat64.VectorAbs(source, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorFms(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
    { SimdFloat64.VectorFms(a, b, c, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorLerp(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double t, Span<double> destination)
    { SimdFloat64.VectorLerp(a, b, t, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ElementWiseMax(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    { SimdFloat64.ElementWiseMax(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ElementWiseMin(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    { SimdFloat64.ElementWiseMin(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeMean(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeMean(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeVariance(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeVariance(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeStdDev(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeStdDev(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeSumOfSquares(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeSumOfSquares(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeProduct(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeProduct(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeL1Norm(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeL1Norm(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeLinfNorm(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeLinfNorm(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstLessThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloat64.FindFirstLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstEqual(ReadOnlySpan<double> source, double value)
        => SimdFloat64.FindFirstEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountLessThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloat64.CountLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountEqual(ReadOnlySpan<double> source, double value)
        => SimdFloat64.CountEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterLessThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
        => SimdFloat64.FilterLessThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterEqual(ReadOnlySpan<double> source, double value, Span<double> destination)
        => SimdFloat64.FilterEqual(source, value, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint ComputeArgMin(ReadOnlySpan<double> source)
        => SimdFloat64.ComputeArgMin(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint ComputeArgMax(ReadOnlySpan<double> source)
        => SimdFloat64.ComputeArgMax(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Select(ReadOnlySpan<double> condition, ReadOnlySpan<double> trueValues, ReadOnlySpan<double> falseValues, Span<double> destination)
        => SimdFloat64.Select(condition, trueValues, falseValues, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Select(ReadOnlySpan<double> condition, double trueValue, double falseValue, Span<double> destination)
        => SimdFloat64.Select(condition, trueValue, falseValue, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareLessThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareLessThan(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareGreaterThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareGreaterThan(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareNotEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareNotEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareLessThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareLessThanOrEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareGreaterThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareGreaterThanOrEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Fill(Span<double> destination, double value) => SimdFloat64.Fill(destination, value);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void FillLinear(Span<double> destination, double start, double step) => SimdFloat64.FillLinear(destination, start, step);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Gather(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
        => SimdFloat64.Gather(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Scatter(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
        => SimdFloat64.Scatter(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorExp(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorExp(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorLog(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorLog(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorSigmoid(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorSigmoid(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorTanh(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorTanh(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorReLU(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorReLU(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorRSqrt(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorRSqrt(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorPow(ReadOnlySpan<double> b, ReadOnlySpan<double> e, Span<double> d) => SimdFloat64.VectorPow(b, e, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Softmax(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.Softmax(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Mat4x4Multiply(ReadOnlySpan<double> l, ReadOnlySpan<double> r, Span<double> d) => SimdFloat64.Mat4x4Multiply(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CrossProduct(ReadOnlySpan<double> l, ReadOnlySpan<double> r, Span<double> d) => SimdFloat64.CrossProduct(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QuaternionMultiply(ReadOnlySpan<double> l, ReadOnlySpan<double> r, Span<double> d) => SimdFloat64.QuaternionMultiply(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QuaternionSlerp(ReadOnlySpan<double> f, ReadOnlySpan<double> t, double tVal, Span<double> d) => SimdFloat64.QuaternionSlerp(f, t, tVal, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Swizzle(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination) => SimdFloat64.Swizzle(source, indices, destination);

    public void Dispose()
    {
        _transformsExecuted.Dispose();
        _reductionsExecuted.Dispose();
    }
}
