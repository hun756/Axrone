namespace Axrone.Simd.Tests;

public class SimdFloat64Tests
{
    private const double Tolerance = 1e-9;
    private const double RelativeTolerance = 1e-12;

    private static double[] Range(int length, double start = 1.0) =>
        Enumerable.Range(0, length).Select(i => start + i).ToArray();

    // ── VectorAdd ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void VectorAdd_ProducesElementWiseSum(int size)
    {
        double[] left = Range(size);
        double[] right = Range(size, 10.0);
        double[] dst = new double[size];

        SimdFloat64.VectorAdd(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(left[i] + right[i], Tolerance);
    }

    [Fact]
    public void VectorAdd_EmptySpans_DoesNotThrow()
    {
        SimdFloat64.VectorAdd(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty, Span<double>.Empty);
    }

    // ── VectorMultiply ───────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void VectorMultiply_ProducesElementWiseProduct(int size)
    {
        double[] left = Range(size);
        double[] right = Range(size, 0.5);
        double[] dst = new double[size];

        SimdFloat64.VectorMultiply(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(left[i] * right[i], Tolerance);
    }

    // ── TransformLinear ──────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void TransformLinear_AppliesMultiplierAndOffset(int size)
    {
        double[] src = Range(size);
        double[] dst = new double[size];
        const double mul = 2.5, off = -3.0;

        SimdFloat64.TransformLinear(src, mul, off, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(src[i] * mul + off, Tolerance);
    }

    // ── ComputeSum ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void ComputeSum_ReturnsCorrectTotal(int size)
    {
        double[] src = Range(size);
        double expected = src.Sum();

        SimdFloat64.ComputeSum(src).Should().BeApproximately(expected, Tolerance);
    }

    [Fact]
    public void ComputeSum_EmptySpan_ReturnsZero()
    {
        SimdFloat64.ComputeSum(ReadOnlySpan<double>.Empty).Should().Be(0.0);
    }

    // ── ComputeDotProduct ────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void ComputeDotProduct_ReturnsCorrectValue(int size)
    {
        double[] left = Range(size);
        double[] right = Range(size, 0.5);
        double expected = left.Zip(right, (a, b) => a * b).Sum();

        SimdFloat64.ComputeDotProduct(left, right).Should().BeApproximately(expected, Tolerance);
    }

    // ── ComputeExtrema ───────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void ComputeExtrema_ReturnsMinAndMax(int size)
    {
        double[] src = Range(size);
        src[0] = -999.0;
        src[size - 1] = 999.0;

        ExtremaPair<double> result = SimdFloat64.ComputeExtrema(src);

        result.Min.Should().Be(-999.0);
        result.Max.Should().Be(999.0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(33)]
    public void ComputeExtrema_SmallAndBoundarySizes_MatchesScalar(int size)
    {
        double[] src = new double[size];
        for (int i = 0; i < size; i++)
            src[i] = (i * 13 + 5) % 17 - 8.0;

        ExtremaPair<double> result = SimdFloat64.ComputeExtrema(src);

        result.Min.Should().Be(src.Min());
        result.Max.Should().Be(src.Max());
    }

    [Fact]
    public void ComputeExtrema_EmptySpan_Throws()
    {
        var act = () => SimdFloat64.ComputeExtrema(ReadOnlySpan<double>.Empty);
        act.Should().Throw<InvalidOperationException>();
    }

    // ── FindFirstGreaterThan ─────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void FindFirstGreaterThan_ReturnsFirstIndex(int size)
    {
        double[] src = Range(size);
        double threshold = size / 2.0;

        int idx = SimdFloat64.FindFirstGreaterThan(src, threshold);

        int expectedIdx = Array.FindIndex(src, v => v > threshold);
        idx.Should().Be(expectedIdx);
    }

    [Fact]
    public void FindFirstGreaterThan_NoneMatch_ReturnsMinusOne()
    {
        double[] src = [1.0, 2.0, 3.0];
        SimdFloat64.FindFirstGreaterThan(src, 100.0).Should().Be(-1);
    }

    // ── CountEqual ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void CountEqual_ReturnsCorrectCount(int size)
    {
        double[] src = Range(size);
        // Sprinkle some duplicates of value 5.0
        for (int i = 0; i < Math.Min(3, size); i++)
            src[i] = 5.0;

        nuint expected = (nuint)src.Count(v => v == 5.0);
        SimdFloat64.CountEqual(src, 5.0).Should().Be(expected);
    }

    [Fact]
    public void CountEqual_EmptySpan_ReturnsZero()
    {
        SimdFloat64.CountEqual(ReadOnlySpan<double>.Empty, 1.0).Should().Be(0);
    }

    // ── VectorClamp ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void VectorClamp_ClipsToRange(int size)
    {
        double[] src = Enumerable.Range(0, size).Select(i => (double)i - size / 2.0).ToArray();
        double[] dst = new double[size];
        const double lo = -5.0, hi = 5.0;

        SimdFloat64.VectorClamp(src, lo, hi, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(Math.Clamp(src[i], lo, hi), Tolerance);
    }

    // ── Fill ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(128)]
    public void Fill_SetsAllElementsToValue(int size)
    {
        double[] dst = new double[size];
        const double value = 42.0;

        SimdFloat64.Fill(dst, value);

        dst.Should().OnlyContain(v => Math.Abs(v - value) < Tolerance);
    }

    [Fact]
    public void Fill_EmptySpan_DoesNotThrow()
    {
        SimdFloat64.Fill(Span<double>.Empty, 1.0);
    }

    // ── VectorExp ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorExp_ProducesCorrectResult(int size)
    {
        double[] src = new double[size];
        for (int i = 0; i < size; i++)
            src[i] = (i - size / 2) * 0.5; // range around zero
        double[] dst = new double[size];

        SimdFloat64.VectorExp(src, dst);

        for (int i = 0; i < size; i++)
        {
            // absolute tolerance is physically impossible above ~2^53 scale (double spacing exceeds it)
            double expected = Math.Exp(src[i]);
            dst[i].Should().BeApproximately(expected, Math.Max(Tolerance, Math.Abs(expected) * RelativeTolerance),
                $"at index {i}: exp({src[i]})");
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorLog_ProducesCorrectResult(int size)
    {
        double[] src = new double[size];
        for (int i = 0; i < size; i++)
            src[i] = (i + 1) * 0.5; // positive values: 0.5, 1.0, 1.5, ...
        double[] dst = new double[size];

        SimdFloat64.VectorLog(src, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(Math.Log(src[i]), Math.Max(Tolerance, Math.Abs(Math.Log(src[i])) * RelativeTolerance),
                $"at index {i}: log({src[i]})");
    }
}
