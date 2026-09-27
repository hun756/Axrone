namespace Axrone.Execution.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Execution;
using Axrone.Utility.Backoff.SpinPolicies;

public class CommandPumpTests
{
    private sealed class SumContext
    {
        public long Total;
    }

    private readonly struct SumProcessor : ICommandProcessor<int, SumContext>
    {
        public static void Process(ref int command, ref SumContext context) =>
            Interlocked.Add(ref context.Total, command);
    }

    private readonly struct CountingTelemetry : IExecutorTelemetry
    {
        public static long Enqueued;
        public static long Dequeued;
        public static long Saturated;

        public static void Reset()
        {
            Enqueued = 0;
            Dequeued = 0;
            Saturated = 0;
        }

        public static void ItemEnqueued() => Interlocked.Increment(ref Enqueued);
        public static void ItemDequeued() => Interlocked.Increment(ref Dequeued);
        public static void QueueSaturated() => Interlocked.Increment(ref Saturated);
    }

    private ref struct ViewContext
    {
        public readonly Span<int> Sink;
        public int Count;

        public ViewContext(Span<int> sink)
        {
            Sink = sink;
            Count = 0;
        }
    }

    private readonly struct ViewProcessor : ICommandProcessor<int, ViewContext>
    {
        public static void Process(ref int command, ref ViewContext context)
        {
            context.Sink[context.Count] = command;
            context.Count++;
        }
    }

    private static CommandPump<int, SumContext, SumProcessor, AggressiveSpinBackoff, NullExecutorTelemetry> CreateSumPump(int capacity = 8) =>
        new(new ExecutorOptions { Capacity = capacity });

    [Fact]
    public void EnqueuePump_PreservesOrderAcrossWraparound()
    {
        using var pump = CreateSumPump(8);
        var context = new SumContext();

        // Ring holds 8; fill+pump in rounds to force multiple wrap cycles.
        long processed = 0;
        for (int round = 0; round < 20; round++)
        {
            for (int i = 0; i < 8; i++)
                pump.TryEnqueue(round * 8 + i).IsEnqueued.Should().BeTrue();
            processed += (long)pump.PumpAll(ref context);
        }

        processed.Should().Be(160);
        context.Total.Should().Be((160L * 159) / 2);
    }

    [Fact]
    public void FullRing_RefusesWithQueueFull()
    {
        using var pump = CreateSumPump(4);
        for (int i = 0; i < 4; i++)
            pump.TryEnqueue(i).IsEnqueued.Should().BeTrue();

        pump.TryEnqueue(99).Status.Should().Be(EnqueueStatus.QueueFull);
        pump.Count.Should().Be((nuint)4);
    }

    [Fact]
    public void Batch_StopsAtFirstRefusal()
    {
        using var pump = CreateSumPump(4);
        int[] items = [1, 2, 3, 4, 5, 6];

        pump.TryEnqueueBatch(items).Should().Be((nuint)4);

        var context = new SumContext();
        pump.PumpAll(ref context).Should().Be((nuint)4);
        context.Total.Should().Be(10);
    }

    [Fact]
    public void Complete_ClosesEnqueue_DrainParksStopped()
    {
        using var pump = CreateSumPump();
        var context = new SumContext();
        pump.TryEnqueue(7);

        pump.Complete();
        pump.State.Should().Be(PumpState.Draining);
        pump.TryEnqueue(8).Status.Should().Be(EnqueueStatus.Closed);

        pump.Drain(ref context).Should().Be((nuint)1);
        context.Total.Should().Be(7);
        pump.State.Should().Be(PumpState.Stopped);
    }

    [Fact]
    public void InvalidCapacity_Throws()
    {
        var three = () => new ExecutorOptions { Capacity = 3 };
        three.Should().Throw<ArgumentOutOfRangeException>();

        var one = () => new ExecutorOptions { Capacity = 1 };
        one.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task MultiProducer_AllItemsProcessedExactlyOnce()
    {
        using var pump = CreateSumPump(1024);
        var context = new SumContext();
        const int producers = 4;
        const int perProducer = 2500;

        var tasks = new Task[producers];
        for (int p = 0; p < producers; p++)
        {
            tasks[p] = Task.Run(() =>
            {
                for (int i = 0; i < perProducer; i++)
                    pump.Enqueue(i).IsEnqueued.Should().BeTrue();
            });
        }

        // Single consumer: the test thread pumps while producers publish.
        Task producerWork = Task.WhenAll(tasks);
        while (!producerWork.IsCompleted)
            pump.Pump(ref context, 256);
        await producerWork;
        pump.Drain(ref context);

        long expected = producers * ((perProducer * (perProducer - 1L)) / 2);
        context.Total.Should().Be(expected);
        pump.Count.Should().Be((nuint)0);
    }

    [Fact]
    public void Enqueue_CancelledToken_Throws()
    {
        using var pump = CreateSumPump(2);
        pump.TryEnqueue(1);
        pump.TryEnqueue(2);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Action enqueue = () => pump.Enqueue(3, cts.Token);
        enqueue.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Telemetry_CountsLifecycle()
    {
        CountingTelemetry.Reset();
        using var pump = new CommandPump<int, SumContext, SumProcessor, AggressiveSpinBackoff, CountingTelemetry>(
            new ExecutorOptions { Capacity = 2 });
        var context = new SumContext();

        pump.TryEnqueue(1);
        pump.TryEnqueue(2);
        pump.TryEnqueue(3); // full
        pump.PumpAll(ref context);

        CountingTelemetry.Enqueued.Should().Be(2);
        CountingTelemetry.Saturated.Should().Be(1);
        CountingTelemetry.Dequeued.Should().Be(2);
    }

    [Fact]
    public void RefStructContext_ThreadsCallerSpan()
    {
        using var pump = new CommandPump<int, ViewContext, ViewProcessor, AggressiveSpinBackoff, NullExecutorTelemetry>(
            new ExecutorOptions { Capacity = 8 });
        int[] sink = new int[4];
        var context = new ViewContext(sink);

        pump.TryEnqueue(10);
        pump.TryEnqueue(20);
        pump.PumpAll(ref context).Should().Be((nuint)2);

        sink[0].Should().Be(10);
        sink[1].Should().Be(20);
        context.Count.Should().Be(2);
    }

    [Fact]
    public void ProcessorFault_PropagatesToPumper()
    {
        using var pump = new CommandPump<int, SumContext, FaultProcessor, AggressiveSpinBackoff, NullExecutorTelemetry>(
            new ExecutorOptions { Capacity = 4 });
        var context = new SumContext();
        pump.TryEnqueue(1);

        Action pumpIt = () => pump.PumpAll(ref context);
        pumpIt.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HotPath_AllocatesNothing()
    {
        using var pump = CreateSumPump(64);
        var context = new SumContext();
        for (int i = 0; i < 64; i++)
            pump.TryEnqueue(i);
        pump.PumpAll(ref context);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            pump.TryEnqueue(i);
        pump.PumpAll(ref context);
        long after = GC.GetAllocatedBytesForCurrentThread();

        (after - before).Should().Be(0);
    }

    private readonly struct FaultProcessor : ICommandProcessor<int, SumContext>
    {
        public static void Process(ref int command, ref SumContext context) =>
            throw new InvalidOperationException("fault");
    }
}
