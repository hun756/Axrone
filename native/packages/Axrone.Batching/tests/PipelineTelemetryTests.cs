namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for pipeline telemetry and snapshots.
/// </summary>
public class PipelineTelemetryTests
{
    private struct NoopKernel : IBatchKernel<int>
    {
        public void Execute(Span<int> batch)
        {
        }
    }

    [Fact]
    public void GetSnapshot_ReflectsPendingWork()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(8).Build();
        for (var i = 0; i < 5; i++)
        {
            pipeline.TryWrite(i);
        }

        pipeline.SwapProducer();

        var snapshot = pipeline.GetSnapshot();

        snapshot.Capacity.Should().Be(8);
        snapshot.PendingItems.Should().Be(0);
        snapshot.HasRemainingWork.Should().BeFalse();

        var kernel = new NoopKernel();
        pipeline.ExecuteSlice(ref kernel, FrameBudget.Infinite);

        pipeline.GetSnapshot().HasRemainingWork.Should().BeFalse();
    }

    [Fact]
    public void GetSnapshot_AfterPartialSlice_ReportsRemainder()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(200).Build();
        for (var i = 0; i < 200; i++)
        {
            pipeline.TryWrite(i);
        }

        pipeline.SwapProducer();

        var spin = new SpinKernel();
        var first = pipeline.ExecuteSlice(ref spin, new FrameBudget(1d));

        first.Status.Should().Be(FrameBudgetStatus.BudgetExceeded);
        var snapshot = pipeline.GetSnapshot();
        snapshot.PendingItems.Should().Be(first.RemainingCount);
        snapshot.HasRemainingWork.Should().BeTrue();
        snapshot.CalibratedStride.Should().BeInRange(
            TimeSlicedPipeline<int>.MinStride, TimeSlicedPipeline<int>.MaxStride);
        snapshot.DroppedItems.Should().Be(0);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var pipeline = TimeSlicedPipeline.Create<int>(4).Build();

        pipeline.Dispose();
        var act = () => pipeline.Dispose();

        act.Should().NotThrow();
    }

    private struct SpinKernel : IBatchKernel<int>
    {
        public void Execute(Span<int> batch) => Thread.SpinWait(20000);
    }
}
