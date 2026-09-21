namespace Axrone.Batching.Tests;

using Axrone.Utility.Builders;

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

    [Fact]
    public void TryBuild_BadCapacity_ReportsDiagnostic()
    {
        var builder = TimeSlicedPipeline.Create<int>(0);

        builder.TryBuild(out var pipeline, out BuilderDiagnostic diagnostic).Should().BeFalse();
        pipeline.Should().BeNull();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
        diagnostic.Message.Should().Contain("Capacity must be positive");
    }

    [Fact]
    public void State_ValidatesWithoutBuilder()
    {
        var state = PipelineState<int>.Default;
        state.Capacity = -4;

        PipelineState<int>.TryValidate(in state, out var diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
    }

    [Fact]
    public void Fork_DivergesIndependently()
    {
        var original = TimeSlicedPipeline.Create<int>(8);
        var fork = original.Fork().WithCapacity(64);

        original.Build().Capacity.Should().Be(8);
        fork.Build().Capacity.Should().Be(64);
    }

    [Fact]
    public void Reset_ClearsToPristine()
    {
        var builder = TimeSlicedPipeline.Create<int>(8).WithStride(BatchStride.LowLatency);
        builder.Reset();

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
    }

    [Fact]
    public void TryBuild_Valid_ReportsOk()
    {
        var builder = TimeSlicedPipeline.Create<int>(8);

        builder.TryBuild(out var pipeline, out BuilderDiagnostic diagnostic).Should().BeTrue();
        pipeline.Should().NotBeNull();
        diagnostic.Should().Be(BuilderDiagnostic.Ok);
    }
}
