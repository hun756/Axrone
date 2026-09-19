namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the pipeline builder.
/// </summary>
public class PipelineBuilderTests
{
    private struct NoopKernel : IBatchKernel<int>
    {
        public void Execute(Span<int> batch)
        {
        }
    }

    [Fact]
    public void Build_Defaults_ServeWork()
    {
        var pipeline = TimeSlicedPipeline.Create<int>(8).Build();

        pipeline.Capacity.Should().Be(8);
        pipeline.TryWrite(1).Should().BeTrue();
        pipeline.SwapProducer();

        var kernel = new NoopKernel();
        pipeline.ExecuteSlice(ref kernel, FrameBudget.Infinite).Status
            .Should().Be(FrameBudgetStatus.Completed);
    }

    [Fact]
    public void Build_CustomStride_ClampsCalibration()
    {
        var pipeline = TimeSlicedPipeline.Create<int>(512)
            .WithCapacity(256)
            .WithStride(new BatchStride(32, 64))
            .Build();

        pipeline.Capacity.Should().Be(256);
    }

    [Fact]
    public void Build_BadCapacity_ThrowsFromPipelineGuard()
    {
        var act = () => TimeSlicedPipeline.Create<int>(0).Build();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
