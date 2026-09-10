namespace Axrone.Simd.Tests;

public sealed class VectorEngineTests : IDisposable
{
    private readonly Float32VectorEngine _f32Engine = new();
    private readonly Float64VectorEngine _f64Engine = new();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _f32Engine.Dispose();
            _f64Engine.Dispose();
        }
    }

    // ── Float32VectorEngine ──────────────────────────────────────────────

    [Fact]
    public void Float32Engine_TransformLinear_DelegatesToSimdFloat32()
    {
        float[] src = [1.0f, 2.0f, 3.0f, 4.0f];
        float[] dst = new float[4];

        _f32Engine.TransformLinear(src, 2.0f, 1.0f, dst);

        dst[0].Should().BeApproximately(3.0f, 1e-5f);
        dst[1].Should().BeApproximately(5.0f, 1e-5f);
        dst[2].Should().BeApproximately(7.0f, 1e-5f);
        dst[3].Should().BeApproximately(9.0f, 1e-5f);
    }

    [Fact]
    public void Float32Engine_VectorAdd_DelegatesToSimdFloat32()
    {
        float[] left = [1.0f, 2.0f, 3.0f, 4.0f];
        float[] right = [10.0f, 20.0f, 30.0f, 40.0f];
        float[] dst = new float[4];

        _f32Engine.VectorAdd(left, right, dst);

        dst[0].Should().BeApproximately(11.0f, 1e-5f);
        dst[3].Should().BeApproximately(44.0f, 1e-5f);
    }

    [Fact]
    public void Float32Engine_ComputeSum_DelegatesToSimdFloat32()
    {
        float[] src = [1.0f, 2.0f, 3.0f, 4.0f];

        float result = _f32Engine.ComputeSum(src);

        result.Should().BeApproximately(10.0f, 1e-5f);
    }

    [Fact]
    public void Float32Engine_ComputeDotProduct_DelegatesToSimdFloat32()
    {
        float[] left = [1.0f, 2.0f, 3.0f];
        float[] right = [4.0f, 5.0f, 6.0f];

        float result = _f32Engine.ComputeDotProduct(left, right);

        result.Should().BeApproximately(32.0f, 1e-5f);
    }

    [Fact]
    public void Float32Engine_ComputeExtrema_DelegatesToSimdFloat32()
    {
        float[] src = [3.0f, -1.0f, 7.0f, 2.0f];

        ExtremaPair<float> result = _f32Engine.ComputeExtrema(src);

        result.Min.Should().BeApproximately(-1.0f, 1e-5f);
        result.Max.Should().BeApproximately(7.0f, 1e-5f);
    }

    // ── Float64VectorEngine ──────────────────────────────────────────────

    [Fact]
    public void Float64Engine_TransformLinear_DelegatesToSimdFloat64()
    {
        double[] src = [1.0, 2.0, 3.0, 4.0];
        double[] dst = new double[4];

        _f64Engine.TransformLinear(src, 3.0, -1.0, dst);

        dst[0].Should().BeApproximately(2.0, 1e-9);
        dst[1].Should().BeApproximately(5.0, 1e-9);
        dst[2].Should().BeApproximately(8.0, 1e-9);
        dst[3].Should().BeApproximately(11.0, 1e-9);
    }

    [Fact]
    public void Float64Engine_VectorAdd_DelegatesToSimdFloat64()
    {
        double[] left = [1.0, 2.0, 3.0, 4.0];
        double[] right = [10.0, 20.0, 30.0, 40.0];
        double[] dst = new double[4];

        _f64Engine.VectorAdd(left, right, dst);

        dst[0].Should().BeApproximately(11.0, 1e-9);
        dst[3].Should().BeApproximately(44.0, 1e-9);
    }

    [Fact]
    public void Float64Engine_ComputeSum_DelegatesToSimdFloat64()
    {
        double[] src = [1.0, 2.0, 3.0, 4.0];

        double result = _f64Engine.ComputeSum(src);

        result.Should().BeApproximately(10.0, 1e-9);
    }

    [Fact]
    public void Float64Engine_ComputeDotProduct_DelegatesToSimdFloat64()
    {
        double[] left = [1.0, 2.0, 3.0];
        double[] right = [4.0, 5.0, 6.0];

        double result = _f64Engine.ComputeDotProduct(left, right);

        result.Should().BeApproximately(32.0, 1e-9);
    }

    [Fact]
    public void Float64Engine_ComputeExtrema_DelegatesToSimdFloat64()
    {
        double[] src = [3.0, -1.0, 7.0, 2.0];

        ExtremaPair<double> result = _f64Engine.ComputeExtrema(src);

        result.Min.Should().BeApproximately(-1.0, 1e-9);
        result.Max.Should().BeApproximately(7.0, 1e-9);
    }

    // ── Metrics counters ─────────────────────────────────────────────────

    [Fact]
    public void Float32Engine_TransformCounter_IncrementsOnTransform()
    {
        _f32Engine.TransformsExecuted.Should().Be(0);

        float[] src = [1.0f, 2.0f];
        float[] dst = new float[2];
        _f32Engine.TransformLinear(src, 1.0f, 0.0f, dst);

        _f32Engine.TransformsExecuted.Should().Be(1);
    }

    [Fact]
    public void Float32Engine_ReductionCounter_IncrementsOnReduction()
    {
        _f32Engine.ReductionsExecuted.Should().Be(0);

        float[] src = [1.0f, 2.0f];
        _f32Engine.ComputeSum(src);

        _f32Engine.ReductionsExecuted.Should().Be(1);
    }

    [Fact]
    public void Float64Engine_TransformCounter_IncrementsOnTransform()
    {
        _f64Engine.TransformsExecuted.Should().Be(0);

        double[] src = [1.0, 2.0];
        double[] dst = new double[2];
        _f64Engine.VectorAdd(src, src, dst);

        _f64Engine.TransformsExecuted.Should().Be(1);
    }

    [Fact]
    public void Float64Engine_ReductionCounter_IncrementsOnReduction()
    {
        _f64Engine.ReductionsExecuted.Should().Be(0);

        double[] src = [1.0, 2.0];
        _f64Engine.ComputeDotProduct(src, src);

        _f64Engine.ReductionsExecuted.Should().Be(1);
    }

    [Fact]
    public void Float32Engine_MultipleOperations_CountersAccumulate()
    {
        float[] src = [1.0f, 2.0f, 3.0f, 4.0f];
        float[] dst = new float[4];

        _f32Engine.VectorAdd(src, src, dst);
        _f32Engine.VectorMultiply(src, src, dst);
        _f32Engine.ComputeSum(src);

        _f32Engine.TransformsExecuted.Should().Be(2);
        _f32Engine.ReductionsExecuted.Should().Be(1);
    }

    [Fact]
    public void Float64Engine_MultipleOperations_CountersAccumulate()
    {
        double[] src = [1.0, 2.0, 3.0, 4.0];
        double[] dst = new double[4];

        _f64Engine.TransformLinear(src, 1.0, 0.0, dst);
        _f64Engine.VectorClamp(src, 0.0, 10.0, dst);
        _f64Engine.ComputeSum(src);
        _f64Engine.ComputeExtrema(src);

        _f64Engine.TransformsExecuted.Should().Be(2);
        _f64Engine.ReductionsExecuted.Should().Be(2);
    }
}
