namespace Axrone.Event.Tests;

/// <summary>
/// Concurrency conformance: multi-publisher fan-out must deliver every envelope exactly once
/// per active subscriber under contention, and failures must land in dead letters without
/// disturbing healthy subscribers.
/// </summary>
public class EventStressTests
{
    private const int Publishers = 4;
    private const int PerPublisher = 2500;
    private const int Total = Publishers * PerPublisher;

    private sealed class Collector
    {
        private readonly object _gate = new();
        private readonly HashSet<long> _sequences = new();
        private readonly HashSet<int> _payloads = new();
        private int _count;

        public void Add(EventEnvelope<int> envelope)
        {
            lock (_gate)
            {
                _count++;
                _sequences.Add(envelope.Metadata.Sequence);
                _payloads.Add(envelope.Payload);
            }
        }

        public (int Count, int UniqueSequences, int UniquePayloads) Snapshot()
        {
            lock (_gate)
            {
                return (_count, _sequences.Count, _payloads.Count);
            }
        }
    }

    [Fact]
    public async Task FanOut_DeliversExactlyOncePerSubscriber()
    {
        using var router = new EventRouter<int>(16384);
        var first = new Collector();
        var second = new Collector();
        using var subA = router.Subscribe((envelope, _) => first.Add(envelope));
        using var subB = router.Subscribe((envelope, _) => second.Add(envelope));

        var tasks = new Task[Publishers];
        for (int p = 0; p < Publishers; p++)
        {
            int offset = p * PerPublisher;
            tasks[p] = Task.Run(() =>
            {
                for (int i = 0; i < PerPublisher; i++)
                {
                    router.Publish(offset + i);
                }
            });
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));
        SpinWait.SpinUntil(() => first.Snapshot().Count == Total && second.Snapshot().Count == Total, TimeSpan.FromSeconds(15))
            .Should().BeTrue();

        first.Snapshot().Should().Be((Total, Total, Total));
        second.Snapshot().Should().Be((Total, Total, Total));
    }

    [Fact]
    public void PoisonSubscriber_DeadLettersWithoutDisturbingHealthy()
    {
        const int Count = 500;
        using var router = new EventRouter<int>(1024);
        var healthy = 0;
        using var bad = router.Subscribe((_, _) => throw new InvalidOperationException("poison"));
        using var good = router.Subscribe((_, _) => Interlocked.Increment(ref healthy));

        for (int i = 0; i < Count; i++)
        {
            router.Publish(i);
        }

        SpinWait.SpinUntil(() => router.DeadLetterCount == Count, TimeSpan.FromSeconds(15)).Should().BeTrue();
        SpinWait.SpinUntil(() => Volatile.Read(ref healthy) == Count, TimeSpan.FromSeconds(15)).Should().BeTrue();

        var dead = router.DrainDeadLetters();
        dead.Should().HaveCount(Count);
        dead.Should().OnlyContain(e => e.Reason == DeadLetterReason.HandlerException);
    }

    [Fact]
    public void UnsubscribeMidStream_StopsDelivery()
    {
        using var router = new EventRouter<int>(1024);
        var count = 0;
        var sub = router.Subscribe((_, _) => Interlocked.Increment(ref count));

        for (int i = 0; i < 100; i++)
        {
            router.Publish(i);
        }

        SpinWait.SpinUntil(() => Volatile.Read(ref count) == 100, TimeSpan.FromSeconds(15)).Should().BeTrue();
        sub.Dispose();

        for (int i = 0; i < 100; i++)
        {
            router.Publish(i);
        }

        Thread.Sleep(200);
        Volatile.Read(ref count).Should().Be(100);
    }
}
