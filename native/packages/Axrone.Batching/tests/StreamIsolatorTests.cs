namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for compaction and bisection isolation.
/// </summary>
public class StreamIsolatorTests
{
    private readonly struct IsEven : IBatchPredicate<int>
    {
        public bool Evaluate(in int item) => (item & 1) == 0;
    }

    private readonly struct IsPositive : IBatchPredicate<int>
    {
        public bool Evaluate(in int item) => item > 0;
    }

    private readonly struct EvenKeepNegativePoison : IBatchFatePredicate<int>
    {
        public BatchItemFate Classify(in int item) =>
            item < 0 ? BatchItemFate.Poison : (item & 1) == 0 ? BatchItemFate.Keep : BatchItemFate.Skip;
    }

    private readonly struct AllFinite : IBatchValidator<float>
    {
        public bool Validate(ReadOnlySpan<float> batch)
        {
            foreach (var value in batch)
            {
                if (!float.IsFinite(value))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [Fact]
    public void Compact_KeepsMatchesInOrder()
    {
        var source = new int[] { 1, 2, 3, 4, 5, 6 };
        var destination = new int[6];

        var written = NativeStreamCompactor.Compact<int, IsEven>(source, destination, new IsEven());

        written.Should().Be(3);
        destination[..written].Should().Equal(2, 4, 6);
    }

    [Fact]
    public void Compact_ShortDestination_Truncates()
    {
        var source = new int[] { 2, 4, 6, 8 };

        var written = NativeStreamCompactor.Compact<int, IsEven>(source, new int[2], new IsEven());

        written.Should().Be(2);
    }

    [Fact]
    public void CompactIndices_WritesSourcePositions()
    {
        var source = new int[] { 5, -1, 7, -2, 0 };

        var written = NativeStreamCompactor.CompactIndices<int, IsPositive>(source, new int[5], new IsPositive());

        written.Should().Be(2);
    }

    [Fact]
    public void CompactInPlace_PartitionsStably()
    {
        var batch = new int[] { 1, 2, 3, 4, 5 };

        var kept = NativeStreamCompactor.CompactInPlace<int, IsEven>(batch, new IsEven());

        kept.Should().Be(2);
        batch[..kept].Should().Equal(2, 4);
    }

    [Fact]
    public void CompactInPlace_NonePass_ReturnsZero()
    {
        var batch = new int[] { 1, 3, 5 };

        NativeStreamCompactor.CompactInPlace<int, IsEven>(batch, new IsEven()).Should().Be(0);
    }

    [Fact]
    public void Bisection_AllValid_ReturnsZero()
    {
        var items = new float[] { 1f, 2f, 3f, 4f };

        NativeBisectionIsolator.FindPoisonRanges<float, AllFinite>(items, new int[4], new AllFinite())
            .Should().Be(0);
    }

    [Fact]
    public void Bisection_SinglePoison_FindsIndex()
    {
        var items = new float[] { 1f, 2f, float.NaN, 4f, 5f };
        var output = new int[4];

        var found = NativeBisectionIsolator.FindPoisonRanges<float, AllFinite>(items, output, new AllFinite());

        found.Should().Be(1);
        output[0].Should().Be(2);
    }

    [Fact]
    public void Bisection_MultiplePoison_FindsAllAscending()
    {
        var items = new float[] { float.PositiveInfinity, 1f, 2f, float.NaN, 4f, float.NegativeInfinity };
        var output = new int[6];

        var found = NativeBisectionIsolator.FindPoisonRanges<float, AllFinite>(items, output, new AllFinite());

        found.Should().Be(3);
        output[..found].Should().Equal(0, 3, 5);
    }

    [Fact]
    public void Bisection_ShortOutput_Truncates()
    {
        var items = new float[] { float.NaN, 1f, float.NaN, 2f, float.NaN };
        var output = new int[2];

        var found = NativeBisectionIsolator.FindPoisonRanges<float, AllFinite>(items, output, new AllFinite());

        found.Should().Be(2);
        output[..found].Should().Equal(0, 2);
    }

    [Fact]
    public void Bisection_EmptyInput_ReturnsZero()
    {
        NativeBisectionIsolator.FindPoisonRanges<float, AllFinite>([], new int[2], new AllFinite())
            .Should().Be(0);
    }

    [Fact]
    public void ClassifyCompact_SeparatesKeepSkipPoison()
    {
        var source = new int[] { 2, 3, -1, 4, -5, 7, 8 };

        var result = NativeStreamCompactor.ClassifyCompact<int, EvenKeepNegativePoison>(
            source, new int[7], new int[7], new EvenKeepNegativePoison());

        result.KeptCount.Should().Be(3);
        result.PoisonCount.Should().Be(2);
    }

    [Fact]
    public void ClassifyCompact_PreservesOrderAndIndices()
    {
        var source = new int[] { 2, 3, -1, 4 };
        var kept = new int[4];
        var poison = new int[4];

        NativeStreamCompactor.ClassifyCompact<int, EvenKeepNegativePoison>(
            source, kept, poison, new EvenKeepNegativePoison());

        kept[..2].Should().Equal(2, 4);
        poison[..1].Should().Equal(2);
    }

    [Fact]
    public void ClassifyCompact_ShortDestinations_Truncate()
    {
        var source = new int[] { 2, 4, -1, -2, 6 };

        var result = NativeStreamCompactor.ClassifyCompact<int, EvenKeepNegativePoison>(
            source, new int[1], new int[1], new EvenKeepNegativePoison());

        result.KeptCount.Should().Be(1);
        result.PoisonCount.Should().Be(1);
    }
}
