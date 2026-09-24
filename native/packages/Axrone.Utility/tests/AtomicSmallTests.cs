namespace Axrone.Utility.Tests.Concurrency;

using Axrone.Utility.Concurrency;

public class AtomicSmallTests
{
    [Fact]
    public void Boolean_RoundTripsWithCas()
    {
        var flag = new AtomicBoolean(false);

        flag.Load().Should().BeFalse();
        flag.Exchange(true).Should().BeFalse();
        flag.Value.Should().BeTrue();

        bool expected = false;
        flag.CompareExchange(ref expected, false).Should().BeFalse();
        expected.Should().BeTrue();

        expected = true;
        flag.CompareExchange(ref expected, false).Should().BeTrue();
        flag.Value.Should().BeFalse();
        ((bool)flag).Should().BeFalse();
    }

    [Fact]
    public void Flag_TestAndSetClearCycle()
    {
        var flag = new AtomicFlag();

        flag.TestAndSet().Should().BeFalse();
        flag.TestAndSet().Should().BeTrue();
        flag.Test().Should().BeTrue();
        flag.Clear();
        flag.Test().Should().BeFalse();
        flag.TestAndSet().Should().BeFalse();
    }

    private sealed class Box
    {
        public int Value;
        public Box(int value) => Value = value;
    }

    [Fact]
    public void Reference_CasAndUpdate()
    {
        var first = new Box(1);
        var second = new Box(2);
        var atomic = new AtomicReference<Box>(first);

        atomic.Load().Should().BeSameAs(first);
        atomic.Exchange(second).Should().BeSameAs(first);

        Box? expected = first;
        atomic.CompareExchange(ref expected, first).Should().BeFalse();
        expected.Should().BeSameAs(second);

        Box? updated = atomic.UpdateAndGet(current => new Box(current!.Value + 10));
        updated!.Value.Should().Be(12);
        atomic.Value.Should().BeSameAs(updated);
        ((Box?)atomic).Should().BeSameAs(updated);
    }

    [Fact]
    public async Task Reference_ContendedUpdates_AreLossless()
    {
        var atomic = new AtomicReference<Box>(new Box(0));
        const int Threads = 4;
        const int PerThread = 500;
        var tasks = new Task[Threads];
        for (int t = 0; t < Threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < PerThread; i++)
                {
                    atomic.UpdateAndGet(current => new Box(current!.Value + 1));
                }
            });
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));
        atomic.Value!.Value.Should().Be(Threads * PerThread);
    }
}
