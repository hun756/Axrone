namespace Axrone.Batching.Tests;

/// <summary>
/// Exercises every contract in <c>Contracts.cs</c> through a struct implementation reached via
/// generic dispatch. The point is not the arithmetic — it is that a <c>struct</c> kernel can
/// satisfy each interface and be consumed without boxing, which is the guarantee the batching
/// pipeline's allocation-free hot path depends on.
/// </summary>
public class ContractsTests
{
    private struct AddKernel : IBatchKernel<int>
    {
        public readonly int Delta;

        public AddKernel(int delta) => Delta = delta;

        public void Execute(scoped Span<int> batch)
        {
            for (var i = 0; i < batch.Length; i++)
            {
                batch[i] += Delta;
            }
        }
    }

    private struct AbsElementKernel : IElementKernel<int>
    {
        public void Execute(scoped ref int item) => item = Math.Abs(item);
    }

    private struct PositivePredicate : IBatchPredicate<int>
    {
        public readonly bool Evaluate(in int item) => item > 0;
    }

    private struct InRangeValidator : IBatchValidator<int>
    {
        public readonly int Max;

        public InRangeValidator(int max) => Max = max;

        public bool Validate(scoped ReadOnlySpan<int> batch)
        {
            for (var i = 0; i < batch.Length; i++)
            {
                if (batch[i] > Max)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private struct DescendingComparer : IBatchComparer<int>
    {
        public readonly int Compare(in int left, in int right) => right.CompareTo(left);
    }

    private struct AscendingComparer : IBatchComparer<int>
    {
        public readonly int Compare(in int left, in int right) => left.CompareTo(right);
    }

    // ── IBatchKernel ────────────────────────────────────────────────────

    [Fact]
    public void BatchKernel_TransformsSpanInPlace()
    {
        int[] data = [1, 2, 3, 4];
        var kernel = new AddKernel(10);

        Apply(ref kernel, data);

        data.Should().Equal(11, 12, 13, 14);
    }

    [Fact]
    public void BatchKernel_EmptySpan_IsNoOp()
    {
        int[] data = [];
        var kernel = new AddKernel(5);

        var act = () => Apply(ref kernel, data);

        act.Should().NotThrow();
        data.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(64)]
    public void BatchKernel_HandlesAnyLength(int length)
    {
        int[] data = new int[length];
        var kernel = new AddKernel(1);

        Apply(ref kernel, data);

        data.Should().OnlyContain(value => value == 1);
    }

    // ── IElementKernel ──────────────────────────────────────────────────

    [Fact]
    public void ElementKernel_AppliesPerElement()
    {
        int[] data = [-1, 2, -3, 4];
        var kernel = new AbsElementKernel();

        for (var i = 0; i < data.Length; i++)
        {
            ref int slot = ref data[i];
            kernel.Execute(ref slot);
        }

        data.Should().Equal(1, 2, 3, 4);
    }

    // ── IBatchPredicate ─────────────────────────────────────────────────

    [Fact]
    public void Predicate_SelectsRetainedElements()
    {
        int[] data = [-2, -1, 0, 1, 2];
        var predicate = new PositivePredicate();

        var retained = data.Where(item => predicate.Evaluate(in item)).ToArray();

        retained.Should().Equal(1, 2);
    }

    [Fact]
    public void Predicate_OnAllNegativeInput_RetainsNothing()
    {
        var predicate = new PositivePredicate();
        int[] data = [-3, -2, -1];

        var retained = data.Where(item => predicate.Evaluate(in item)).ToArray();

        retained.Should().BeEmpty();
    }

    [Fact]
    public void Predicate_OnEmptyInput_RetainsNothing()
    {
        var predicate = new PositivePredicate();

        var retained = Array.Empty<int>().Where(item => predicate.Evaluate(in item)).ToArray();

        retained.Should().BeEmpty();
    }

    // ── IBatchValidator ─────────────────────────────────────────────────

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, true)]
    [InlineData(new[] { 1, 9, 3 }, true)]
    [InlineData(new[] { 1, 10, 3 }, false)]
    [InlineData(new[] { 11 }, false)]
    [InlineData(new int[0], true)]
    public void Validator_ReportsInvariantOverRange(int[] data, bool expected)
    {
        var validator = new InRangeValidator(9);

        validator.Validate(data).Should().Be(expected);
    }

    [Fact]
    public void Validator_HalvingPreservesFailureLocation()
    {
        // The bisection isolator relies on: a failing range always contains at least one failing
        // half. If Validate were range-dependent in any other way, this would break.
        int[] data = [1, 2, 3, 99, 5, 6, 7, 8];
        var validator = new InRangeValidator(9);

        validator.Validate(data).Should().BeFalse();
        validator.Validate(data.AsSpan(0, 4)).Should().BeFalse();
        validator.Validate(data.AsSpan(4, 4)).Should().BeTrue();
        validator.Validate(data.AsSpan(3, 1)).Should().BeFalse();
        validator.Validate(data.AsSpan(2, 1)).Should().BeTrue();
    }

    // ── IBatchComparer ──────────────────────────────────────────────────

    [Fact]
    public void Comparer_AntisymmetricForEveryPair()
    {
        var ascending = new AscendingComparer();
        int[] samples = [-5, 0, 1, 1, 7, 42];

        foreach (var left in samples)
        {
            foreach (var right in samples)
            {
                Math.Sign(ascending.Compare(in left, in right))
                    .Should().Be(-Math.Sign(ascending.Compare(in right, in left)),
                        $"comparing {left} and {right}");
            }
        }
    }

    [Fact]
    public void Comparer_TransitiveOnChainedTriples()
    {
        var ascending = new AscendingComparer();

        int a = 1, b = 2, c = 3;

        ascending.Compare(in a, in b).Should().BeLessThan(0);
        ascending.Compare(in b, in c).Should().BeLessThan(0);
        ascending.Compare(in a, in c).Should().BeLessThan(0);
    }

    [Fact]
    public void Comparer_EqualElements_CompareToZero()
    {
        var ascending = new AscendingComparer();
        int a = 5, b = 5;

        ascending.Compare(in a, in b).Should().Be(0);
    }

    [Fact]
    public void Comparer_DrivesArraySortConsistently()
    {
        var descending = new DescendingComparer();
        int[] data = [3, 1, 4, 1, 5, 9, 2, 6];
        int[] expected = (int[])data.Clone();
        Array.Sort(expected, (left, right) => descending.Compare(in left, in right));

        for (var i = 1; i < expected.Length; i++)
        {
            // Sanity: the ordering Array.Sort produced is non-increasing under our comparer.
            descending.Compare(in expected[i - 1], in expected[i]).Should().BeLessThanOrEqualTo(0);
        }

        expected.Should().Equal(9, 6, 5, 4, 3, 2, 1, 1);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a kernel through a generic constraint, mirroring how the pipeline holds kernels by
    /// <c>ref</c> so the call devirtualizes.
    /// </summary>
    private static void Apply<TKernel>(ref TKernel kernel, Span<int> target)
        where TKernel : struct, IBatchKernel<int> => kernel.Execute(target);
}
