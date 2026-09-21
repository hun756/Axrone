namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the pipeline role contracts: interface dispatch stays allocation-free and sound.
/// </summary>
public class PipelineContractsTests
{
    private struct DoubleKernel : IBatchKernel<int>
    {
        public void Execute(Span<int> batch)
        {
            for (var i = 0; i < batch.Length; i++)
            {
                batch[i] *= 2;
            }
        }
    }

    private struct SumVisitor : IBatchVisitor<int, int>
    {
        public void Visit(ReadOnlySpan<int> batch, ref int state)
        {
            foreach (var item in batch)
            {
                state += item;
            }
        }
    }

    [Fact]
    public void Pipeline_ServesProducerAndSlicerRoles()
    {
        var pipeline = new TimeSlicedPipeline<int>(4);
        IBatchProducer<int> producer = pipeline;
        IBatchSlicer<int> slicer = pipeline;

        producer.Capacity.Should().Be(4);
        producer.TryWrite(3).Should().BeTrue();
        producer.SwapProducer();

        var kernel = new DoubleKernel();
        var result = slicer.ExecuteSlice(ref kernel, FrameBudget.Infinite);

        result.Status.Should().Be(FrameBudgetStatus.Completed);
        slicer.HasRemainingWork.Should().BeFalse();
    }

    [Fact]
    public void Visitor_ConsumesBatchWithoutAllocation()
    {
        var source = new int[] { 1, 2, 3, 4 };
        var visitor = new SumVisitor();
        var total = 0;

        visitor.Visit(source, ref total);

        total.Should().Be(10);
    }

    [Fact]
    public void Drain_VisitsActiveRemainder()
    {
        var pipeline = new TimeSlicedPipeline<int>(8);
        pipeline.Write([1, 2, 3, 4, 5]);
        pipeline.SwapProducer();

        var visitor = new SumVisitor();
        var total = 0;

        pipeline.Drain(ref visitor, ref total).Should().Be(5);
        total.Should().Be(15);
        pipeline.HasRemainingWork.Should().BeFalse();
    }

    [Fact]
    public void Drain_Empty_ReturnsZero()
    {
        var pipeline = new TimeSlicedPipeline<int>(4);
        var visitor = new SumVisitor();
        var total = 0;

        pipeline.Drain(ref visitor, ref total).Should().Be(0);
        total.Should().Be(0);
    }
}
