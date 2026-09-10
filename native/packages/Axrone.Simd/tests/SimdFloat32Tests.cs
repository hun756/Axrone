using System.Buffers;
using Axrone.Simd;
using FluentAssertions;

namespace Axrone.Simd.Tests;

public class SimdFloat32Tests
{
    private const float Tolerance = 1e-4f;
    private const float LooseTolerance = 1e-2f;

    #region Arithmetic

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(100)]
    public void VectorAdd_ProducesCorrectResult(int size)
    {
        float[] left = CreateSequence(size, 1f);
        float[] right = CreateSequence(size, 10f);
        float[] dst = new float[size];

        SimdFloat32.VectorAdd(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(left[i] + right[i], Tolerance);
    }

    [Fact]
    public void VectorAdd_EmptySpan_DoesNotThrow()
    {
        float[] dst = [];
        var act = () => SimdFloat32.VectorAdd(ReadOnlySpan<float>.Empty, ReadOnlySpan<float>.Empty, dst);
        act.Should().NotThrow();
    }

    [Fact]
    public void VectorAdd_MismatchedLengths_Throws()
    {
        float[] left = [1f, 2f, 3f];
        float[] right = [1f, 2f];
        float[] dst = new float[3];

        var act = () => SimdFloat32.VectorAdd(left, right, dst);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorSubtract_ProducesCorrectResult(int size)
    {
        float[] left = CreateSequence(size, 10f);
        float[] right = CreateSequence(size, 3f);
        float[] dst = new float[size];

        SimdFloat32.VectorSubtract(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(left[i] - right[i], Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorMultiply_ProducesCorrectResult(int size)
    {
        float[] left = CreateSequence(size, 2f);
        float[] right = CreateSequence(size, 5f);
        float[] dst = new float[size];

        SimdFloat32.VectorMultiply(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(left[i] * right[i], Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorDivide_ProducesCorrectResult(int size)
    {
        float[] left = CreateSequence(size, 10f);
        float[] right = CreateFilled(size, 2f);
        float[] dst = new float[size];

        SimdFloat32.VectorDivide(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(left[i] / right[i], Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorNegate_ProducesCorrectResult(int size)
    {
        float[] src = CreateSequence(size, 1f);
        float[] dst = new float[size];

        SimdFloat32.VectorNegate(src, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(-src[i], Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorAbs_ProducesCorrectResult_WithNegativeValues(int size)
    {
        float[] src = new float[size];
        for (int i = 0; i < size; i++)
            src[i] = (i % 2 == 0) ? -(i + 1) : (i + 1);
        float[] dst = new float[size];

        SimdFloat32.VectorAbs(src, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(MathF.Abs(src[i]), Tolerance);
    }

    #endregion

    #region Transform

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void TransformLinear_ProducesCorrectResult(int size)
    {
        float[] src = CreateSequence(size, 1f);
        float[] dst = new float[size];
        const float multiplier = 3f;
        const float offset = 10f;

        SimdFloat32.TransformLinear(src, multiplier, offset, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(src[i] * multiplier + offset, Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorFma_ProducesCorrectResult(int size)
    {
        float[] a = CreateSequence(size, 2f);
        float[] b = CreateSequence(size, 3f);
        float[] c = CreateSequence(size, 100f);
        float[] dst = new float[size];

        SimdFloat32.VectorFma(a, b, c, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(a[i] * b[i] + c[i], Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorFms_ProducesCorrectResult(int size)
    {
        float[] a = CreateSequence(size, 2f);
        float[] b = CreateSequence(size, 3f);
        float[] c = CreateSequence(size, 1f);
        float[] dst = new float[size];

        SimdFloat32.VectorFms(a, b, c, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(a[i] * b[i] - c[i], Tolerance);
    }

    [Fact]
    public void VectorLerp_AtT0_ReturnsFirstVector()
    {
        float[] a = [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f];
        float[] b = [10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f];
        float[] dst = new float[8];

        SimdFloat32.VectorLerp(a, b, 0f, dst);

        for (int i = 0; i < a.Length; i++)
            dst[i].Should().BeApproximately(a[i], Tolerance);
    }

    [Fact]
    public void VectorLerp_AtT1_ReturnsSecondVector()
    {
        float[] a = [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f];
        float[] b = [10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f];
        float[] dst = new float[8];

        SimdFloat32.VectorLerp(a, b, 1f, dst);

        for (int i = 0; i < a.Length; i++)
            dst[i].Should().BeApproximately(b[i], Tolerance);
    }

    [Fact]
    public void VectorLerp_AtT05_ReturnsMidpoint()
    {
        float[] a = [0f, 0f, 0f, 0f];
        float[] b = [10f, 20f, 30f, 40f];
        float[] dst = new float[4];

        SimdFloat32.VectorLerp(a, b, 0.5f, dst);

        dst[0].Should().BeApproximately(5f, Tolerance);
        dst[1].Should().BeApproximately(10f, Tolerance);
        dst[2].Should().BeApproximately(15f, Tolerance);
        dst[3].Should().BeApproximately(20f, Tolerance);
    }

    #endregion

    #region Clamping

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorClamp_ClampsValuesWithinAndOutsideRange(int size)
    {
        float[] src = new float[size];
        for (int i = 0; i < size; i++)
            src[i] = i * 2f - size; // ranges from -size to roughly +size
        float[] dst = new float[size];
        const float min = -5f;
        const float max = 5f;

        SimdFloat32.VectorClamp(src, min, max, dst);

        for (int i = 0; i < size; i++)
        {
            float expected = Math.Clamp(src[i], min, max);
            dst[i].Should().BeApproximately(expected, Tolerance);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void ElementWiseMax_ProducesCorrectPairwiseMax(int size)
    {
        float[] left = CreateSequence(size, 1f);
        float[] right = CreateFilled(size, 5f);
        float[] dst = new float[size];

        SimdFloat32.ElementWiseMax(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(MathF.Max(left[i], right[i]), Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void ElementWiseMin_ProducesCorrectPairwiseMin(int size)
    {
        float[] left = CreateSequence(size, 1f);
        float[] right = CreateFilled(size, 5f);
        float[] dst = new float[size];

        SimdFloat32.ElementWiseMin(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(MathF.Min(left[i], right[i]), Tolerance);
    }

    #endregion

    #region Reductions

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void ComputeSum_ProducesCorrectSum(int size)
    {
        float[] src = CreateSequence(size, 1f);
        float expected = 0f;
        for (int i = 0; i < size; i++) expected += src[i];

        float result = SimdFloat32.ComputeSum(src);
        result.Should().BeApproximately(expected, Tolerance);
    }

    [Fact]
    public void ComputeSum_EmptySpan_ReturnsZero()
    {
        SimdFloat32.ComputeSum(ReadOnlySpan<float>.Empty).Should().Be(0f);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void ComputeDotProduct_ProducesCorrectResult(int size)
    {
        float[] left = CreateSequence(size, 1f);
        float[] right = CreateSequence(size, 2f);
        float expected = 0f;
        for (int i = 0; i < size; i++) expected += left[i] * right[i];

        float result = SimdFloat32.ComputeDotProduct(left, right);
        result.Should().BeApproximately(expected, Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void ComputeL2Norm_ProducesCorrectResult(int size)
    {
        float[] src = CreateSequence(size, 1f);
        float expected = 0f;
        for (int i = 0; i < size; i++) expected += src[i] * src[i];
        expected = MathF.Sqrt(expected);

        float result = SimdFloat32.ComputeL2Norm(src);
        result.Should().BeApproximately(expected, Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void ComputeExtrema_ProducesCorrectMinMax(int size)
    {
        float[] src = new float[size];
        for (int i = 0; i < size; i++)
            src[i] = (i * 7 + 3) % 11 - 5f;

        ExtremaPair<float> result = SimdFloat32.ComputeExtrema(src);

        result.Min.Should().BeApproximately(src.Min(), Tolerance);
        result.Max.Should().BeApproximately(src.Max(), Tolerance);
    }

    [Fact]
    public void ComputeExtrema_EmptySpan_Throws()
    {
        var act = () => SimdFloat32.ComputeExtrema(ReadOnlySpan<float>.Empty);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void ComputeMean_ProducesCorrectResult(int size)
    {
        float[] src = CreateSequence(size, 1f);
        float expected = 0f;
        for (int i = 0; i < size; i++) expected += src[i];
        expected /= size;

        float result = SimdFloat32.ComputeMean(src);
        result.Should().BeApproximately(expected, Tolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void ComputeVariance_ProducesCorrectResult(int size)
    {
        float[] src = CreateSequence(size, 1f);
        float mean = 0f;
        for (int i = 0; i < size; i++) mean += src[i];
        mean /= size;
        float expected = 0f;
        for (int i = 0; i < size; i++)
        {
            float diff = src[i] - mean;
            expected += diff * diff;
        }
        expected /= size; // population variance

        float result = SimdFloat32.ComputeVariance(src);
        result.Should().BeApproximately(expected, LooseTolerance);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void ComputeStdDev_ProducesCorrectResult(int size)
    {
        float[] src = CreateSequence(size, 1f);
        float mean = 0f;
        for (int i = 0; i < size; i++) mean += src[i];
        mean /= size;
        float variance = 0f;
        for (int i = 0; i < size; i++)
        {
            float diff = src[i] - mean;
            variance += diff * diff;
        }
        variance /= size;
        float expected = MathF.Sqrt(variance);

        float result = SimdFloat32.ComputeStdDev(src);
        result.Should().BeApproximately(expected, LooseTolerance);
    }

    #endregion

    #region Scan

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void FindFirstGreaterThan_FindsCorrectIndex(int size)
    {
        float[] src = CreateSequence(size, 0f); // 0,1,2,...,size-1
        float threshold = size / 2f;

        int result = SimdFloat32.FindFirstGreaterThan(src, threshold);

        int expected = (int)threshold + 1;
        if (expected < size)
            result.Should().Be(expected);
        else
            result.Should().Be(-1);
    }

    [Fact]
    public void FindFirstGreaterThan_NoMatch_ReturnsMinusOne()
    {
        float[] src = [1f, 2f, 3f, 4f];
        SimdFloat32.FindFirstGreaterThan(src, 100f).Should().Be(-1);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void CountGreaterThan_ProducesCorrectCount(int size)
    {
        float[] src = CreateSequence(size, 0f); // 0,1,2,...,size-1
        float threshold = size / 2f;

        nuint result = SimdFloat32.CountGreaterThan(src, threshold);

        int expected = 0;
        for (int i = 0; i < size; i++)
            if (src[i] > threshold) expected++;
        ((int)result).Should().Be(expected);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void FilterGreaterThan_ProducesCorrectOutput(int size)
    {
        float[] src = CreateSequence(size, 0f);
        float threshold = size / 3f;
        float[] dst = new float[size];

        ScanResult result = SimdFloat32.FilterGreaterThan(src, threshold, dst);

        result.Status.Should().Be(OperationStatus.Done);
        int expectedCount = 0;
        for (int i = 0; i < size; i++)
            if (src[i] > threshold) expectedCount++;
        ((int)result.WrittenCount).Should().Be(expectedCount);

        for (int i = 0; i < expectedCount; i++)
            dst[i].Should().BeGreaterThan(threshold);
    }

    #endregion

    #region Compare

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void CompareEqual_ProducesCorrectMask(int size)
    {
        float[] left = CreateSequence(size, 1f);
        float[] right = new float[size];
        for (int i = 0; i < size; i++)
            right[i] = (i % 2 == 0) ? left[i] : left[i] + 1f;
        float[] dst = new float[size];

        SimdFloat32.CompareEqual(left, right, dst);

        for (int i = 0; i < size; i++)
        {
            if (left[i] == right[i])
                float.IsNaN(dst[i]).Should().BeTrue("equal elements should produce NaN mask (all-ones bits)");
            else
                dst[i].Should().Be(0f, "unequal elements should produce zero mask");
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void CompareGreaterThan_ProducesCorrectMask(int size)
    {
        float[] left = CreateSequence(size, 1f);
        float[] right = CreateFilled(size, (float)(size / 2));
        float[] dst = new float[size];

        SimdFloat32.CompareGreaterThan(left, right, dst);

        for (int i = 0; i < size; i++)
        {
            if (left[i] > right[i])
                float.IsNaN(dst[i]).Should().BeTrue("true comparison should produce NaN mask");
            else
                dst[i].Should().Be(0f, "false comparison should produce zero mask");
        }
    }

    #endregion

    #region Fill / Gather

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void Fill_SetsAllElements(int size)
    {
        float[] dst = new float[size];
        const float value = 42f;

        SimdFloat32.Fill(dst, value);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(value);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void FillLinear_ProducesCorrectSequence(int size)
    {
        float[] dst = new float[size];
        const float start = 5f;
        const float step = 2.5f;

        SimdFloat32.FillLinear(dst, start, step);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(start + i * step, Tolerance);
    }

    #endregion

    #region Math

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorExp_ProducesCorrectResult(int size)
    {
        float[] src = new float[size];
        for (int i = 0; i < size; i++)
            src[i] = (i - size / 2) * 0.5f; // range around zero
        float[] dst = new float[size];

        SimdFloat32.VectorExp(src, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(MathF.Exp(src[i]), LooseTolerance,
                $"at index {i}: exp({src[i]})");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void VectorLog_ProducesCorrectResult(int size)
    {
        float[] src = new float[size];
        for (int i = 0; i < size; i++)
            src[i] = (i + 1) * 0.5f; // positive values: 0.5, 1.0, 1.5, ...
        float[] dst = new float[size];

        SimdFloat32.VectorLog(src, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().BeApproximately(MathF.Log(src[i]), LooseTolerance,
                $"at index {i}: log({src[i]})");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(100)]
    public void Softmax_SumsToOne(int size)
    {
        float[] src = CreateSequence(size, 0.1f);
        float[] dst = new float[size];

        SimdFloat32.Softmax(src, dst);

        float sum = 0f;
        for (int i = 0; i < size; i++)
        {
            dst[i].Should().BeGreaterThan(0f);
            sum += dst[i];
        }
        sum.Should().BeApproximately(1f, Tolerance);
    }

    #endregion

    #region Helpers

    private static float[] CreateSequence(int size, float start)
    {
        float[] arr = new float[size];
        for (int i = 0; i < size; i++)
            arr[i] = start + i;
        return arr;
    }

    private static float[] CreateFilled(int size, float value)
    {
        float[] arr = new float[size];
        Array.Fill(arr, value);
        return arr;
    }

    #endregion
}
