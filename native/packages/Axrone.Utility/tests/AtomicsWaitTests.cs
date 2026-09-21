namespace Axrone.Utility.Tests.Concurrency;

using Axrone.Utility.Concurrency;

public class AtomicsWaitTests
{
    [Fact]
    public void Wait_ReturnsImmediatelyWhenChanged()
    {
        var atomic = new Atomic<int>(5);

        var act = () => atomic.Wait(4);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Wait_WakesOnNotify()
    {
        var atomic = new Atomic<int>(0);
        var waiter = Task.Run(() => atomic.Wait(0));
        await Task.Delay(50);

        atomic.Store(1);
        atomic.NotifyOne();

        await waiter.WaitAsync(TimeSpan.FromSeconds(15));
        atomic.Value.Should().Be(1);
    }

    [Fact]
    public async Task NotifyAll_WakesEveryWaiter()
    {
        var atomic = new Atomic<int>(0);
        const int Waiters = 3;
        var tasks = new Task[Waiters];
        for (int i = 0; i < Waiters; i++)
        {
            tasks[i] = Task.Run(() => atomic.Wait(0));
        }

        await Task.Delay(100);
        atomic.Store(9);
        atomic.NotifyAll();

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));
    }

    private sealed class Token
    {
        public int Value;
    }

    [Fact]
    public async Task WaitRef_WakesOnNotify()
    {
        var reference = new AtomicReference<Token>(null);
        var waiter = Task.Run(() => reference.Wait(null));
        await Task.Delay(50);

        reference.Store(new Token { Value = 7 });
        reference.NotifyOne();

        await waiter.WaitAsync(TimeSpan.FromSeconds(15));
        reference.Value!.Value.Should().Be(7);
    }
}
