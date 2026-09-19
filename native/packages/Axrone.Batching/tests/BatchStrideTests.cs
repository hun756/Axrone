namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the stride bounds value object.
/// </summary>
public class BatchStrideTests
{
    [Fact]
    public void Default_MatchesPipelineTuning()
    {
        BatchStride.Default.Min.Should().Be(TimeSlicedPipeline<int>.MinStride);
        BatchStride.Default.Max.Should().Be(TimeSlicedPipeline<int>.MaxStride);
    }

    [Theory]
    [InlineData(0, 64)]
    [InlineData(-1, 64)]
    [InlineData(128, 64)]
    public void Constructor_RejectsBadBounds(int min, int max)
    {
        var act = () => new BatchStride(min, max);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1, 16, 64, 16)]
    [InlineData(100, 16, 64, 64)]
    [InlineData(32, 16, 64, 32)]
    public void Clamp_PinsToRange(int value, int min, int max, int expected)
    {
        new BatchStride(min, max).Clamp(value).Should().Be(expected);
    }

    [Fact]
    public void Equality_IsByValue()
    {
        new BatchStride(16, 1024).Should().Be(BatchStride.Default);
        (new BatchStride(16, 64) == BatchStride.Default).Should().BeFalse();
    }

    [Fact]
    public void Presets_AreOrderedAndInsideDefault()
    {
        BatchStride.LowLatency.Min.Should().Be(BatchStride.Default.Min);
        BatchStride.LowLatency.Max.Should().BeLessThan(BatchStride.Default.Max);
        BatchStride.Throughput.Min.Should().BeGreaterThan(BatchStride.Default.Min);
        BatchStride.Throughput.Max.Should().Be(BatchStride.Default.Max);
    }

    [Fact]
    public void PresetPipeline_ServesWork()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(64)
            .WithStride(BatchStride.LowLatency)
            .Build();

        pipeline.Capacity.Should().Be(64);
    }
}
