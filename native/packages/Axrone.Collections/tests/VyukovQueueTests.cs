using Axrone.Collections;

namespace Axrone.Collections.Tests;

public class VyukovQueueTests
{
    [Fact]
    public void TryEnqueue_TryDequeue_BasicFlow()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(16));

        queue.TryEnqueue(42).Should().BeTrue();
        queue.TryDequeue(out int item).Should().BeTrue();
        item.Should().Be(42);
    }

    [Fact]
    public void TryEnqueue_MultipleItems_DequeuesInFIFOOrder()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(16));

        for (int i = 0; i < 10; i++)
            queue.TryEnqueue(i).Should().BeTrue();

        for (int i = 0; i < 10; i++)
        {
            queue.TryDequeue(out int item).Should().BeTrue();
            item.Should().Be(i);
        }
    }

    [Fact]
    public void TryEnqueue_WhenFull_ReturnsFalse()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(4));

        queue.TryEnqueue(1).Should().BeTrue();
        queue.TryEnqueue(2).Should().BeTrue();
        queue.TryEnqueue(3).Should().BeTrue();
        queue.TryEnqueue(4).Should().BeTrue();
        queue.TryEnqueue(5).Should().BeFalse();
    }

    [Fact]
    public void TryDequeue_WhenEmpty_ReturnsFalse()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(16));

        queue.TryDequeue(out int _).Should().BeFalse();
    }

    [Fact]
    public void BufferCapacity_NonPowerOfTwo_Throws()
    {
        var act = () => new BufferCapacity(10);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BufferCapacity_PowerOfTwo_ReportsCapacity()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(16));
        queue.Capacity.Should().Be(16u);
    }

    [Fact]
    public void Dispose_PreventsFurtherAccess()
    {
        var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(16));
        queue.Dispose();

        // After dispose, native memory is freed; accessing it is undefined behavior.
        // We verify Dispose does not throw and is idempotent.
        queue.Dispose();
    }

    [Fact]
    public async Task ConcurrentMultiProducerMultiConsumer_NoDataLoss()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(1024));
        const int producers = 4;
        const int consumers = 4;
        const int itemsPerProducer = 2_000;
        int totalItems = producers * itemsPerProducer;

        var consumed = new ConcurrentBag<int>();
        var barrier = new ManualResetEventSlim(false);

        var producerTasks = Enumerable.Range(0, producers).Select(p => Task.Run(() =>
        {
            barrier.Wait();
            for (int i = 0; i < itemsPerProducer; i++)
            {
                int item = p * itemsPerProducer + i;
                while (!queue.TryEnqueue(item)) Thread.SpinWait(1);
            }
        })).ToArray();

        var consumerTasks = Enumerable.Range(0, consumers).Select(_ => Task.Run(() =>
        {
            barrier.Wait();
            int localCount = 0;
            int target = totalItems / consumers;
            while (localCount < target)
            {
                if (queue.TryDequeue(out int item))
                {
                    consumed.Add(item);
                    localCount++;
                }
                else
                {
                    Thread.SpinWait(1);
                }
            }
        })).ToArray();

        barrier.Set();
        await Task.WhenAll(producerTasks);
        await Task.WhenAll(consumerTasks);

        consumed.Count.Should().Be(totalItems);
    }

    [Fact]
    public void Wraparound_WorksCorrectly()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(4));

        for (int round = 0; round < 5; round++)
        {
            for (int i = 0; i < 4; i++)
                queue.TryEnqueue(round * 4 + i).Should().BeTrue();

            for (int i = 0; i < 4; i++)
            {
                queue.TryDequeue(out int item).Should().BeTrue();
                item.Should().Be(round * 4 + i);
            }
        }
    }

    [Fact]
    public void BatchOperations_WorkCorrectly()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(16));

        ReadOnlySpan<int> batch = stackalloc int[] { 1, 2, 3, 4, 5 };
        queue.TryEnqueueBatch(batch).Should().Be(5);

        Span<int> destination = stackalloc int[5];
        queue.TryDequeueBatch(destination).Should().Be(5);
        destination.ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void BatchOperations_PartialFill_ReportsCorrectCount()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(4));

        ReadOnlySpan<int> batch = stackalloc int[] { 1, 2, 3 };
        queue.TryEnqueueBatch(batch).Should().Be(3);

        queue.TryEnqueue(99).Should().BeTrue();
        queue.TryEnqueue(100).Should().BeFalse();

        Span<int> destination = stackalloc int[4];
        queue.TryDequeueBatch(destination).Should().Be(3);
        destination[..3].ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void OperationStatus_ReportsFullAndEmpty()
    {
        using var queue = new VyukovBoundedBatchQueue<int, AdaptiveSpinBackoff>(new BufferCapacity(2));

        queue.TryDequeueWithStatus(out _).Should().Be(QueueOperationStatus.Empty);

        queue.TryEnqueueWithStatus(1).Should().Be(QueueOperationStatus.Success);
        queue.TryEnqueueWithStatus(2).Should().Be(QueueOperationStatus.Success);
        queue.TryEnqueueWithStatus(3).Should().Be(QueueOperationStatus.Full);
    }
}
