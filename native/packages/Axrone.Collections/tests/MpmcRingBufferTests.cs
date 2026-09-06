using Axrone.Collections;

namespace Axrone.Collections.Tests;

public class MpmcRingBufferTests
{
    [Fact]
    public void Constructor_RoundsUpToPowerOfTwo()
    {
        var buffer = new MpmcRingBuffer<int>(100);
        buffer.Capacity.Should().Be(128);
    }

    [Fact]
    public void Constructor_MinimumCapacity_IsTwo()
    {
        var buffer = new MpmcRingBuffer<int>(2);
        buffer.Capacity.Should().Be(2);
    }

    [Fact]
    public void Constructor_InvalidCapacity_Throws()
    {
        var act = () => new MpmcRingBuffer<int>(1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryEnqueue_TryDequeue_BasicFlow()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        buffer.TryEnqueue(42).Should().BeTrue();
        buffer.TryDequeue(out int item).Should().BeTrue();
        item.Should().Be(42);
    }

    [Fact]
    public void TryDequeue_Empty_ReturnsFalse()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        buffer.TryDequeue(out int _).Should().BeFalse();
    }

    [Fact]
    public void TryEnqueue_Full_ReturnsFalse()
    {
        var buffer = new MpmcRingBuffer<int>(4);
        buffer.TryEnqueue(1).Should().BeTrue();
        buffer.TryEnqueue(2).Should().BeTrue();
        buffer.TryEnqueue(3).Should().BeTrue();
        buffer.TryEnqueue(4).Should().BeTrue();
        buffer.TryEnqueue(5).Should().BeFalse();
    }

    [Fact]
    public void Count_ReflectsEnqueueDequeue()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        buffer.Count.Should().Be(0);
        buffer.IsEmpty.Should().BeTrue();

        buffer.TryEnqueue(1);
        buffer.Count.Should().Be(1);
        buffer.IsEmpty.Should().BeFalse();

        buffer.TryDequeue(out _);
        buffer.Count.Should().Be(0);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsFull_WhenAtCapacity()
    {
        var buffer = new MpmcRingBuffer<int>(4);
        for (int i = 0; i < 4; i++) buffer.TryEnqueue(i);
        buffer.IsFull.Should().BeTrue();
    }

    [Fact]
    public void EnqueueRange_WritesMultiple()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        Span<int> data = stackalloc int[] { 1, 2, 3, 4, 5 };
        int written = buffer.EnqueueRange(data);
        written.Should().Be(5);
        buffer.Count.Should().Be(5);
    }

    [Fact]
    public void DequeueRange_ReadsMultiple()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        for (int i = 1; i <= 5; i++) buffer.TryEnqueue(i);

        Span<int> dest = stackalloc int[5];
        int read = buffer.DequeueRange(dest);
        read.Should().Be(5);
        dest[0].Should().Be(1);
        dest[4].Should().Be(5);
    }

    [Fact]
    public void DrainTo_DrainsAvailable()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        for (int i = 0; i < 3; i++) buffer.TryEnqueue(i);

        Span<int> dest = stackalloc int[10];
        int drained = buffer.DrainTo(dest);
        drained.Should().Be(3);
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        for (int i = 0; i < 10; i++) buffer.TryEnqueue(i);
        buffer.Clear();
        buffer.Count.Should().Be(0);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Dispose_PreventsFurtherOperations()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        buffer.Dispose();
        buffer.IsDisposed.Should().BeTrue();
        var act1 = () => buffer.TryEnqueue(1);
        act1.Should().Throw<ObjectDisposedException>();
        var act2 = () => buffer.TryDequeue(out _);
        act2.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        buffer.Dispose();
        buffer.Dispose();
    }

    [Fact]
    public void Producer_ReturnsProducerEndpoint()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        var producer = buffer.Producer;
        producer.Should().NotBeNull();
        producer.Capacity.Should().Be(16);
    }

    [Fact]
    public void Consumer_ReturnsConsumerEndpoint()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        var consumer = buffer.Consumer;
        consumer.Should().NotBeNull();
        consumer.Capacity.Should().Be(16);
    }

    [Fact]
    public void Enqueue_WithTimeout_SucceedsImmediately()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        var result = buffer.Enqueue(42, TimeSpan.FromSeconds(1));
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Enqueue_WithTimeout_ReturnsFailureOnTimeout()
    {
        var buffer = new MpmcRingBuffer<int>(new RingBufferOptions { Capacity = 2, WaitStrategy = new BusySpinWaitStrategy() });
        buffer.TryEnqueue(1);
        buffer.TryEnqueue(2);
        var result = buffer.Enqueue(3, TimeSpan.FromMilliseconds(10));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Dequeue_WithTimeout_ReturnsFailureOnTimeout()
    {
        var buffer = new MpmcRingBuffer<int>(new RingBufferOptions { Capacity = 16, WaitStrategy = new BusySpinWaitStrategy() });
        var result = buffer.Dequeue(TimeSpan.FromMilliseconds(10));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Enqueue_WithCancellation_ThrowsOnCancel()
    {
        var buffer = new MpmcRingBuffer<int>(2);
        buffer.TryEnqueue(1);
        buffer.TryEnqueue(2);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => buffer.Enqueue(3, cts.Token);
        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void FIFO_Order_IsPreserved()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        for (int i = 0; i < 10; i++) buffer.TryEnqueue(i);
        for (int i = 0; i < 10; i++)
        {
            buffer.TryDequeue(out int item).Should().BeTrue();
            item.Should().Be(i);
        }
    }

    [Fact]
    public void Wraparound_WorksCorrectly()
    {
        var buffer = new MpmcRingBuffer<int>(4);
        for (int round = 0; round < 5; round++)
        {
            for (int i = 0; i < 4; i++) buffer.TryEnqueue(round * 4 + i);
            for (int i = 0; i < 4; i++)
            {
                buffer.TryDequeue(out int item).Should().BeTrue();
                item.Should().Be(round * 4 + i);
            }
        }
    }
}

public class ConcurrencyRingBufferTests
{
    [Fact]
    public async Task ConcurrentEnqueueDequeue_NoDataLoss()
    {
        var buffer = new MpmcRingBuffer<int>(1024);
        const int itemCount = 10_000;
        var produced = new ConcurrentBag<int>();
        var consumed = new ConcurrentBag<int>();

        var producer = Task.Run(() =>
        {
            for (int i = 0; i < itemCount; i++)
            {
                while (!buffer.TryEnqueue(i)) Thread.SpinWait(1);
                produced.Add(i);
            }
        });

        var consumer = Task.Run(() =>
        {
            for (int i = 0; i < itemCount; i++)
            {
                int item;
                while (!buffer.TryDequeue(out item)) Thread.SpinWait(1);
                consumed.Add(item);
            }
        });

        await Task.WhenAll(producer, consumer);

        consumed.Count.Should().Be(itemCount);
        produced.Count.Should().Be(itemCount);
    }

    [Fact]
    public async Task MultipleProducersConsumers_AllItemsProcessed()
    {
        var buffer = new MpmcRingBuffer<int>(256);
        const int producers = 4;
        const int consumers = 4;
        const int itemsPerProducer = 1000;
        var totalConsumed = new ConcurrentBag<int>();

        var producerTasks = Enumerable.Range(0, producers).Select(p => Task.Run(() =>
        {
            for (int i = 0; i < itemsPerProducer; i++)
            {
                int item = p * itemsPerProducer + i;
                while (!buffer.TryEnqueue(item)) Thread.SpinWait(1);
            }
        })).ToArray();

        var consumerTasks = Enumerable.Range(0, consumers).Select(_ => Task.Run(() =>
        {
            int localCount = 0;
            while (localCount < (producers * itemsPerProducer / consumers))
            {
                if (buffer.TryDequeue(out int item))
                {
                    totalConsumed.Add(item);
                    localCount++;
                }
                else
                {
                    Thread.SpinWait(1);
                }
            }
        })).ToArray();

        await Task.WhenAll(producerTasks);

        foreach (var ct in consumerTasks) await ct;

        totalConsumed.Count.Should().Be(producers * itemsPerProducer);
    }
}

public class WaitStrategyTests
{
    [Fact]
    public void BusySpin_CanBeReset()
    {
        var strategy = new BusySpinWaitStrategy();
        strategy.Reset();
        strategy.Wait();
    }

    [Fact]
    public void Yield_CanBeReset()
    {
        var strategy = new YieldWaitStrategy();
        strategy.Reset();
        strategy.Wait();
    }

    [Fact]
    public void SpinWait_CanBeReset()
    {
        var strategy = new SpinWaitStrategy();
        strategy.Reset();
        strategy.Wait();
    }

    [Fact]
    public void Adaptive_ProgressesThroughPhases()
    {
        var strategy = new AdaptiveWaitStrategy();
        strategy.Reset();
        for (int i = 0; i < 60; i++) strategy.Wait();
    }
}

public class ResultIntegrationTests
{
    [Fact]
    public void Enqueue_ReturnsResultSuccess()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        var result = buffer.Enqueue(42, TimeSpan.FromSeconds(1));
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Dequeue_ReturnsResultWithSuccess()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        buffer.TryEnqueue(42);
        var result = buffer.Dequeue(TimeSpan.FromSeconds(1));
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Dequeue_Empty_ReturnsResultFailure()
    {
        var buffer = new MpmcRingBuffer<int>(16);
        var result = buffer.Dequeue(TimeSpan.Zero);
        result.IsFailure.Should().BeTrue();
    }
}
