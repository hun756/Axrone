namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for chunked dispatch: chunk counts, sizes, order, validation.
/// </summary>
public class BatchDispatcherTests
{
    private struct CountingKernel : IBatchKernel<int>
    {
        public int Calls;
        public int Total;
        public int LastChunkLength;

        public void Execute(Span<int> batch)
        {
            Calls++;
            Total += batch.Length;
            LastChunkLength = batch.Length;
            for (var i = 0; i < batch.Length; i++)
            {
                batch[i] *= 2;
            }
        }
    }

    [Fact]
    public void Constructor_RejectsNonPositiveChunk()
    {
        var actZero = () => new BatchDispatcher(0);
        var actNegative = () => new BatchDispatcher(-5);

        actZero.Should().Throw<ArgumentOutOfRangeException>();
        actNegative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Dispatch_EmptyBatch_NeverCallsKernel()
    {
        var dispatcher = new BatchDispatcher(4);
        var kernel = new CountingKernel();

        dispatcher.Dispatch<int, CountingKernel>([], ref kernel);

        kernel.Calls.Should().Be(0);
    }

    [Fact]
    public void Dispatch_ShortBatch_SingleCall()
    {
        var dispatcher = new BatchDispatcher(8);
        var batch = new int[] { 1, 2, 3 };
        var kernel = new CountingKernel();

        dispatcher.Dispatch<int, CountingKernel>(batch, ref kernel);

        kernel.Calls.Should().Be(1);
        kernel.Total.Should().Be(3);
        batch.Should().Equal(2, 4, 6);
    }

    [Fact]
    public void Dispatch_ExactMultiple_EqualChunks()
    {
        var dispatcher = new BatchDispatcher(4);
        var batch = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var kernel = new CountingKernel();

        dispatcher.Dispatch<int, CountingKernel>(batch, ref kernel);

        kernel.Calls.Should().Be(2);
        kernel.Total.Should().Be(8);
        batch.Should().Equal(2, 4, 6, 8, 10, 12, 14, 16);
    }

    [Fact]
    public void Dispatch_WithRemainder_LastChunkIsShort()
    {
        var dispatcher = new BatchDispatcher(4);
        var batch = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var kernel = new CountingKernel();

        dispatcher.Dispatch<int, CountingKernel>(batch, ref kernel);

        kernel.Calls.Should().Be(3);
        kernel.Total.Should().Be(10);
        kernel.LastChunkLength.Should().Be(2);
    }
}
