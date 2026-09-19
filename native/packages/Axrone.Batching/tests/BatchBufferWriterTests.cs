namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the buffer-writer adapter: staging, flush, backpressure, POH staging.
/// </summary>
public class BatchBufferWriterTests
{
    private struct SumKernel : IBatchKernel<int>
    {
        public int Total;

        public void Execute(Span<int> batch)
        {
            foreach (var item in batch)
            {
                Total += item;
            }
        }
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(4).Build();

        var act = () => new BatchBufferWriter<int>(pipeline, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WriteFlushSwapExecute_RoundTrips()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(8).Build();
        var writer = new BatchBufferWriter<int>(pipeline);

        var span = writer.GetSpan(4);
        span[0] = 1;
        span[1] = 2;
        span[2] = 3;
        span[3] = 4;
        writer.Advance(4);
        writer.WrittenCount.Should().Be(4);

        writer.Flush().Should().Be(4);
        writer.WrittenCount.Should().Be(0);
        pipeline.SwapProducer();

        var kernel = new SumKernel();
        pipeline.ExecuteSlice(ref kernel, FrameBudget.Infinite).Status
            .Should().Be(FrameBudgetStatus.Completed);
        kernel.Total.Should().Be(10);
    }

    [Fact]
    public void Flush_FullSlot_KeepsRemainderStaged()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(2).Build();
        var writer = new BatchBufferWriter<int>(pipeline);

        writer.GetSpan(4).Slice(0, 4).Fill(7);
        writer.Advance(4);

        writer.Flush().Should().Be(2);
        writer.WrittenCount.Should().Be(2);

        pipeline.SwapProducer();
        writer.Flush().Should().Be(2);
        writer.WrittenCount.Should().Be(0);
    }

    [Fact]
    public void Advance_OutOfRange_Throws()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(4).Build();
        var writer = new BatchBufferWriter<int>(pipeline);

        var act = () => writer.Advance(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PinnedStaging_RoundTrips()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(8).Build();
        var writer = new BatchBufferWriter<int>(pipeline, 8, pinnedStaging: true);

        writer.GetSpan(2)[0] = 5;
        writer.Advance(1);
        writer.Flush().Should().Be(1);
        pipeline.SwapProducer();

        var kernel = new SumKernel();
        pipeline.ExecuteSlice(ref kernel, FrameBudget.Infinite);
        kernel.Total.Should().Be(5);
    }

    [Fact]
    public void Reset_DiscardsStaged()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(8).Build();
        var writer = new BatchBufferWriter<int>(pipeline);

        writer.GetSpan(3).Fill(9);
        writer.Advance(3);
        writer.Reset();

        writer.WrittenCount.Should().Be(0);
        writer.Flush().Should().Be(0);
    }
}
