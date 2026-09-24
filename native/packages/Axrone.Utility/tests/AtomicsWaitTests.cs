namespace Axrone.Utility.Tests.Concurrency;

using System.Runtime.InteropServices;
using Axrone.Utility.Concurrency;

/// <summary>
/// Address-hashed rendezvous needs a stable address, so the holder is pinned for the test
/// duration (see <see cref="AtomicsWaitAsyncTests"/>).
/// </summary>
public class AtomicsWaitTests
{
    private sealed class IntHolder
    {
        public Atomic<int> Value;
    }

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
        var holder = new IntHolder();
        GCHandle pin = GCHandle.Alloc(holder, GCHandleType.Pinned);
        try
        {
            var waiter = Task.Run(() => holder.Value.Wait(0));
            await Task.Delay(50);

            holder.Value.Store(1);
            holder.Value.NotifyOne();

            await waiter.WaitAsync(TimeSpan.FromSeconds(15));
            holder.Value.Value.Should().Be(1);
        }
        finally
        {
            pin.Free();
        }
    }

    [Fact]
    public async Task NotifyAll_WakesEveryWaiter()
    {
        var holder = new IntHolder();
        GCHandle pin = GCHandle.Alloc(holder, GCHandleType.Pinned);
        try
        {
            const int Waiters = 3;
            var tasks = new Task[Waiters];
            for (int i = 0; i < Waiters; i++)
            {
                tasks[i] = Task.Run(() => holder.Value.Wait(0));
            }

            await Task.Delay(100);
            holder.Value.Store(9);
            holder.Value.NotifyAll();

            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            pin.Free();
        }
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
