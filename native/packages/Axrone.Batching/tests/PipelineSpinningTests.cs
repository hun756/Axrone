namespace Axrone.Batching.Tests;

using Axrone.Utility.Backoff.SpinPolicies;

/// <summary>
/// Coverage for the spinning execution path.
/// </summary>
public class PipelineSpinningTests
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

    private struct IncrementKernel : IElementKernel<int>
    {
        public void Execute(ref int item) => item++;
    }

    private struct SpinKernel : IBatchKernel<int>
    {
        public void Execute(Span<int> batch) => Thread.SpinWait(20000);
    }

    private static TimeSlicedPipeline<int> Fill(int count)
    {
        var pipeline = new TimeSlicedPipeline<int>(Math.Max(count, 1));
        for (var i = 0; i < count; i++)
        {
            pipeline.TryWrite(1).Should().BeTrue();
        }

        pipeline.SwapProducer();
        return pipeline;
    }

    [Fact]
    public void ExecuteSpinning_DrainsBatch()
    {
        var pipeline = Fill(100);
        var kernel = new DoubleKernel();

        pipeline.ExecuteSpinning<DoubleKernel, ProgressiveSpinBackoff>(ref kernel);

        kernel.Total.Should().Be(100);
        pipeline.HasRemainingWork.Should().BeFalse();
    }

    [Fact]
    public void ExecuteElementsSpinning_DrainsBatch()
    {
        var pipeline = Fill(50);
        var kernel = new IncrementKernel();

        pipeline.ExecuteElementsSpinning<IncrementKernel, ProgressiveSpinBackoff>(ref kernel);

        pipeline.HasRemainingWork.Should().BeFalse();
    }

    [Fact]
    public void ExecuteSpinning_Empty_ReturnsImmediately()
    {
        var pipeline = new TimeSlicedPipeline<int>(4);
        var kernel = new DoubleKernel();

        pipeline.ExecuteSpinning<DoubleKernel, ProgressiveSpinBackoff>(ref kernel);

        pipeline.HasRemainingWork.Should().BeFalse();
    }

    [Fact]
    public void ExecuteSpinning_CancelledToken_Throws()
    {
        var pipeline = Fill(5000);
        var kernel = new SpinKernel();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = () => pipeline.ExecuteSpinning<SpinKernel, ProgressiveSpinBackoff>(ref kernel, source.Token);

        act.Should().Throw<OperationCanceledException>();
    }
}
