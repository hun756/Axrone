namespace Axrone.Execution.Tests;

using System.Runtime.CompilerServices;
using Xunit;
using FluentAssertions;
using Axrone.Execution;
using Axrone.Utility.Backoff;
using Axrone.Utility.Backoff.SpinPolicies;

public class CommandPumpTests
{
    private static CancellationTokenSource? s_contentionCts;

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
        public static long Contentions;
        public static long Faults;
        public static Exception? LastFault;

        public static void Reset()
        {
            Enqueued = 0;
            Dequeued = 0;
            Saturated = 0;
            Contentions = 0;
            Faults = 0;
            LastFault = null;
        }

        public static void ItemEnqueued() => Interlocked.Increment(ref Enqueued);
        public static void ItemDequeued() => Interlocked.Increment(ref Dequeued);
        public static void QueueSaturated() => Interlocked.Increment(ref Saturated);
        public static void ContentionDetected() => Interlocked.Increment(ref Contentions);

        public static void FaultEncountered(Exception exception)
        {
            Volatile.Write(ref LastFault, exception);
            Interlocked.Increment(ref Faults);
        }
    }

    /// <summary>Backoff that cancels the blocking enqueue on its first step, so the
    /// contention round is observed exactly once without a timing dependency.</summary>
    private readonly struct CancelOnFirstBackoff : ISpinBackoff
    {
        public static void Initialize(out int state) => state = 0;

        public static void Advance(ref int state)
        {
            state++;
            s_contentionCts?.Cancel();
        }

        public static void Reset(ref int state) => state = 0;
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
    public void ProcessorFault_FaultsPump_RethrowsWithStack_AndRefusesWork()
    {
        CountingTelemetry.Reset();
        using var pump = new CommandPump<int, SumContext, FaultProcessor, AggressiveSpinBackoff, CountingTelemetry>(
            new ExecutorOptions { Capacity = 4 });
        var context = new SumContext();
        pump.TryEnqueue(1);

        Action pumpIt = () => pump.PumpAll(ref context);
        InvalidOperationException caught = pumpIt.Should().Throw<InvalidOperationException>().Which;

        caught.Message.Should().Be("fault");
        caught.StackTrace.Should().Contain(nameof(FaultProcessor.ThrowFault));
        pump.State.Should().Be(PumpState.Faulted);
        pump.TryEnqueue(2).Status.Should().Be(EnqueueStatus.Closed);
        pump.Enqueue(2).Status.Should().Be(EnqueueStatus.Closed);
        CountingTelemetry.Faults.Should().Be(1);
        CountingTelemetry.LastFault.Should().BeSameAs(caught);
    }

    [Fact]
    public void BlockingEnqueue_AgainstFullRing_ReportsContention()
    {
        CountingTelemetry.Reset();
        using var pump = new CommandPump<int, SumContext, SumProcessor, CancelOnFirstBackoff, CountingTelemetry>(
            new ExecutorOptions { Capacity = 2 });
        pump.TryEnqueue(1).IsEnqueued.Should().BeTrue();
        pump.TryEnqueue(2).IsEnqueued.Should().BeTrue();

        using var cts = new CancellationTokenSource();
        s_contentionCts = cts;

        Action enqueue = () => pump.Enqueue(3, cts.Token);
        enqueue.Should().Throw<OperationCanceledException>();

        CountingTelemetry.Saturated.Should().Be(1);
        CountingTelemetry.Contentions.Should().Be(1);
        s_contentionCts = null;
    }

    [Fact]
    public void FaultCall_TransitionsStateAndReportsTelemetry_WithoutThrowing()
    {
        CountingTelemetry.Reset();
        using var pump = new CommandPump<int, SumContext, SumProcessor, AggressiveSpinBackoff, CountingTelemetry>(
            new ExecutorOptions { Capacity = 4 });
        var fault = new InvalidOperationException("host fault");

        pump.Fault(fault);

        pump.State.Should().Be(PumpState.Faulted);
        pump.TryEnqueue(1).Status.Should().Be(EnqueueStatus.Closed);
        CountingTelemetry.Faults.Should().Be(1);
        CountingTelemetry.LastFault.Should().BeSameAs(fault);

        var nullFault = () => pump.Fault(null!);
        nullFault.Should().Throw<ArgumentNullException>();
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
            ThrowFault();

        // Deep throw site: a stack-preserving rethrow keeps this frame, a reset one loses it.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowFault() => throw new InvalidOperationException("fault");
    }
}
