namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for pipeline termination: completion backpressure, fault propagation, drain.
/// </summary>
public class PipelineLifecycleTests
{
    private struct DoubleKernel : IBatchKernel<int>
    {
        public int Total;

        public void Execute(Span<int> batch)
        {
            Total += batch.Length;
            for (var i = 0; i < batch.Length; i++)
            {
                batch[i] *= 2;
            }
        }
    }

    [Fact]
    public void NewPipeline_IsHealthy()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(4).Build();

        pipeline.IsHealthy.Should().BeTrue();
        pipeline.TerminalFault.Should().BeNull();
    }

    [Fact]
    public void Complete_StopsIntake_DrainsRemainder()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(8).Build();
        for (var i = 0; i < 4; i++)
        {
            pipeline.TryWrite(i).Should().BeTrue();
        }

        pipeline.SwapProducer();
        pipeline.Complete();

        pipeline.IsHealthy.Should().BeFalse();
        pipeline.TryWrite(99).Should().BeFalse();
        pipeline.WriteRange(new int[] { 1 }).Should().Be(0);

        var kernel = new DoubleKernel();
        pipeline.Drain(ref kernel);

        kernel.Total.Should().Be(4);
        pipeline.HasRemainingWork.Should().BeFalse();
    }

    [Fact]
    public void Complete_WithFault_PropagatesCause()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(4).Build();
        var cause = new InvalidOperationException("poison batch");

        pipeline.Complete(cause);

        pipeline.IsHealthy.Should().BeFalse();
        pipeline.TerminalFault.Should().BeSameAs(cause);

        var kernel = new DoubleKernel();
        var slice = () => pipeline.ExecuteSlice(ref kernel, FrameBudget.Infinite);
        var drain = () => pipeline.Drain(ref kernel);

        slice.Should().Throw<InvalidOperationException>().WithMessage("poison batch");
        drain.Should().Throw<InvalidOperationException>().WithMessage("poison batch");
    }

    [Fact]
    public void Complete_CleanAfterFault_KeepsFault()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(4).Build();
        var cause = new InvalidOperationException("poison batch");

        pipeline.Complete(cause);
        pipeline.Complete();

        pipeline.TerminalFault.Should().BeSameAs(cause);
        pipeline.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void Drain_Empty_ReturnsImmediately()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(4).Build();
        var kernel = new DoubleKernel();

        pipeline.Drain(ref kernel);

        kernel.Total.Should().Be(0);
    }

    [Fact]
    public void Drain_CancelledToken_Throws()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(8).Build();
        for (var i = 0; i < 8; i++)
        {
            pipeline.TryWrite(i);
        }

        pipeline.SwapProducer();

        var kernel = new DoubleKernel();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = () => pipeline.Drain(ref kernel, source.Token);

        act.Should().Throw<OperationCanceledException>();
    }
}
