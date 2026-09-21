namespace Axrone.Collections.Tests;

/// <summary>
/// Regression coverage for waiter lifecycle: cancel must unlink (exactly-once completion),
/// dispose must tolerate settled waiters, and async streaming must not lose wakeups.
/// </summary>
public class AsyncWaiterLifecycleTests
{
    [Fact]
    public async Task Dispose_WithCanceledWaiter_DoesNotThrow()
    {
        var queue = new VyukovBoundedBatchQueue<int>(new BufferCapacity(8));
        using var cts = new CancellationTokenSource();

        var destination = new int[4];
        ValueTask<int> pending = queue.DequeueBatchAsync(destination, cts.Token);
        pending.IsCompleted.Should().BeFalse();

        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);

        var act = () => queue.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithPendingWaiter_FaultsCleanly()
    {
        var queue = new VyukovBoundedBatchQueue<int>(new BufferCapacity(8));
        var destination = new int[4];
        ValueTask<int> pending = queue.DequeueBatchAsync(destination, CancellationToken.None);
        pending.IsCompleted.Should().BeFalse();
        queue.Dispose();
    }

    [Fact]
    public async Task Cancel_Then_Reuse_Works()
    {
        var queue = new VyukovBoundedBatchQueue<int>(new BufferCapacity(8));
        using var cts = new CancellationTokenSource();

        var destination = new int[4];
        ValueTask<int> pending = queue.DequeueBatchAsync(destination, cts.Token);
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);

        queue.TryEnqueue(42).Should().BeTrue();
        queue.TryDequeue(out int item).Should().BeTrue();
        item.Should().Be(42);

        queue.Dispose();
    }

    [Fact]
    public async Task Dispose_WithPendingWaiter_FaultsWaiterCleanly()
    {
        var queue = new VyukovBoundedBatchQueue<int>(new BufferCapacity(8));

        var destination = new int[4];
        ValueTask<int> pending = queue.DequeueBatchAsync(destination, CancellationToken.None);
        pending.IsCompleted.Should().BeFalse();

        queue.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await pending);
    }

    [Fact]
    public async Task AsyncProducerConsumer_StreamsWithoutLoss()
    {
        const int Count = 2000;
        var queue = new VyukovBoundedBatchQueue<int>(new BufferCapacity(64));

        var consumer = Task.Run(async () =>
        {
            var received = 0;
            var buffer = new int[128];
            while (received < Count)
            {
                received += await queue.DequeueBatchAsync(buffer.AsMemory(), CancellationToken.None);
            }

            return received;
        });

        var producer = Task.Run(async () =>
        {
            var sent = 0;
            var chunk = new int[128];
            for (int i = 0; i < chunk.Length; i++)
            {
                chunk[i] = i;
            }

            while (sent < Count)
            {
                int batch = Math.Min(chunk.Length, Count - sent);
                sent += await queue.EnqueueBatchAsync(chunk.AsMemory(0, batch), CancellationToken.None);
            }

            return sent;
        });

        int[] results = await Task.WhenAll(consumer, producer).WaitAsync(TimeSpan.FromSeconds(30));
        results.Should().Equal(Count, Count);

        queue.Dispose();
    }
}
