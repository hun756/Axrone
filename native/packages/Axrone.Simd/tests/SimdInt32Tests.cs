namespace Axrone.Simd.Tests;

public class SimdInt32Tests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static int[] Range(int length, int start = 1) =>
        Enumerable.Range(start, length).ToArray();

    // ── Add ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void Add_ProducesElementWiseSum(int size)
    {
        int[] left = Range(size);
        int[] right = Range(size, 100);
        int[] dst = new int[size];

        SimdInt32.Add(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(left[i] + right[i], "element {0} mismatch", i);
    }

    [Fact]
    public void Add_EmptySpans_DoesNotThrow()
    {
        SimdInt32.Add(ReadOnlySpan<int>.Empty, ReadOnlySpan<int>.Empty, Span<int>.Empty);
    }

    [Fact]
    public void Add_MismatchedLengths_Throws()
    {
        int[] a = new int[4], b = new int[8], dst = new int[4];
        var act = () => SimdInt32.Add(a, b, dst);
        act.Should().Throw<ArgumentException>();
    }

    // ── Subtract ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void Subtract_ProducesElementWiseDifference(int size)
    {
        int[] left = Range(size, 200);
        int[] right = Range(size);
        int[] dst = new int[size];

        SimdInt32.Subtract(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(left[i] - right[i]);
    }

    // ── Multiply ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void Multiply_ProducesElementWiseProduct(int size)
    {
        int[] left = Range(size);
        int[] right = Range(size, 2);
        int[] dst = new int[size];

        SimdInt32.Multiply(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(left[i] * right[i]);
    }

    // ── Negate ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void Negate_FlipsSign(int size)
    {
        int[] src = Range(size);
        int[] dst = new int[size];

        SimdInt32.Negate(src, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(-src[i]);
    }

    // ── Abs ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void Abs_ReturnsAbsoluteValues(int size)
    {
        int[] src = Enumerable.Range(0, size).Select(i => i % 2 == 0 ? i : -i).ToArray();
        int[] dst = new int[size];

        SimdInt32.Abs(src, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(Math.Abs(src[i]));
    }

    // ── Clamp ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void Clamp_ClipsToRange(int size)
    {
        int[] src = Enumerable.Range(0, size).Select(i => i - size / 2).ToArray();
        int[] dst = new int[size];
        const int lo = -5, hi = 5;

        SimdInt32.Clamp(src, lo, hi, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(Math.Clamp(src[i], lo, hi));
    }

    // ── ElementWiseMax / ElementWiseMin ──────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void ElementWiseMax_ReturnsLargerPerElement(int size)
    {
        int[] left = Range(size);
        int[] right = Range(size, size / 2);
        int[] dst = new int[size];

        SimdInt32.ElementWiseMax(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(Math.Max(left[i], right[i]));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void ElementWiseMin_ReturnsSmallerPerElement(int size)
    {
        int[] left = Range(size);
        int[] right = Range(size, size / 2);
        int[] dst = new int[size];

        SimdInt32.ElementWiseMin(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(Math.Min(left[i], right[i]));
    }

    // ── ComputeSum ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    [InlineData(256)]
    public void ComputeSum_ReturnsCorrectTotal(int size)
    {
        int[] src = Range(size);
        int expected = src.Sum();

        SimdInt32.ComputeSum(src).Should().Be(expected);
    }

    [Fact]
    public void ComputeSum_EmptySpan_ReturnsZero()
    {
        SimdInt32.ComputeSum(ReadOnlySpan<int>.Empty).Should().Be(0);
    }

    // ── ComputeDotProduct ────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    [InlineData(256)]
    public void ComputeDotProduct_ReturnsCorrectValue(int size)
    {
        int[] left = Range(size);
        int[] right = Range(size, 2);
        int expected = left.Zip(right, (a, b) => a * b).Sum();

        SimdInt32.ComputeDotProduct(left, right).Should().Be(expected);
    }

    // ── FindFirstGreaterThan ─────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void FindFirstGreaterThan_ReturnsFirstIndex(int size)
    {
        int[] src = Range(size);
        int threshold = size / 2;

        int idx = SimdInt32.FindFirstGreaterThan(src, threshold);

        int expectedIdx = Array.FindIndex(src, v => v > threshold);
        idx.Should().Be(expectedIdx);
    }

    [Fact]
    public void FindFirstGreaterThan_NoneMatch_ReturnsMinusOne()
    {
        int[] src = [1, 2, 3];
        SimdInt32.FindFirstGreaterThan(src, 100).Should().Be(-1);
    }

    [Fact]
    public void FindFirstGreaterThan_EmptySpan_ReturnsMinusOne()
    {
        SimdInt32.FindFirstGreaterThan(ReadOnlySpan<int>.Empty, 0).Should().Be(-1);
    }

    // ── CountGreaterThan ─────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void CountGreaterThan_ReturnsCorrectCount(int size)
    {
        int[] src = Range(size);
        int threshold = size / 4;
        nuint expected = (nuint)src.Count(v => v > threshold);

        SimdInt32.CountGreaterThan(src, threshold).Should().Be(expected);
    }

    // ── VectorAnd ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void VectorAnd_ProducesBitwiseAnd(int size)
    {
        int[] left = Range(size);
        int[] right = Enumerable.Range(0, size).Select(i => ~i).ToArray();
        int[] dst = new int[size];

        SimdInt32.VectorAnd(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(left[i] & right[i]);
    }

    // ── VectorOr ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void VectorOr_ProducesBitwiseOr(int size)
    {
        int[] left = Range(size);
        int[] right = Enumerable.Range(0, size).Select(i => i * 3).ToArray();
        int[] dst = new int[size];

        SimdInt32.VectorOr(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(left[i] | right[i]);
    }

    // ── VectorXor ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void VectorXor_ProducesBitwiseXor(int size)
    {
        int[] left = Range(size);
        int[] right = Range(size, 7);
        int[] dst = new int[size];

        SimdInt32.VectorXor(left, right, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(left[i] ^ right[i]);
    }

    [Fact]
    public void VectorXor_SameInputs_ProducesZero()
    {
        int[] src = Range(64);
        int[] dst = new int[64];

        SimdInt32.VectorXor(src, src, dst);

        dst.Should().OnlyContain(v => v == 0);
    }

    // ── ShiftLeft / ShiftRight ───────────────────────────────────────────

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void ShiftLeft_ShiftsEachElement(int size)
    {
        int[] src = Range(size);
        int[] dst = new int[size];

        SimdInt32.ShiftLeft(src, 2, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(src[i] << 2);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    public void ShiftRight_ShiftsEachElement(int size)
    {
        int[] src = Enumerable.Range(0, size).Select(i => i * 8).ToArray();
        int[] dst = new int[size];

        SimdInt32.ShiftRight(src, 3, dst);

        for (int i = 0; i < size; i++)
            dst[i].Should().Be(src[i] >> 3);
    }
}
