namespace Axrone.Utility.Tests.Concurrency;

using System.Runtime.InteropServices;
using Axrone.Utility.Concurrency;

/// <summary>
/// Async rendezvous needs a stable address: the waiter and the notifier hash the field
/// address into buckets, so the holder is pinned for the test duration. Locals of async
/// methods live in movable state-machine boxes and must not be used directly.
/// </summary>
public class AtomicsWaitAsyncTests
{
    private sealed class IntHolder
    {
        public Atomic<int> Value;
    }

    [Fact]
    public async Task WaitAsync_ChangedValue_CompletesSynchronously()
    {
        var atomic = new Atomic<int>(5);

        await atomic.WaitAsync(4);
    }

    [Fact]
    public async Task WaitAsync_WakesOnNotify()
    {
        var holder = new IntHolder();
        GCHandle pin = GCHandle.Alloc(holder, GCHandleType.Pinned);
        try
        {
            var waiter = holder.Value.WaitAsync(0).AsTask();
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
    public async Task WaitAsync_NotifyAll_WakesEveryWaiter()
    {
        var holder = new IntHolder();
        GCHandle pin = GCHandle.Alloc(holder, GCHandleType.Pinned);
        try
        {
            var first = holder.Value.WaitAsync(0).AsTask();
            var second = holder.Value.WaitAsync(0).AsTask();
            await Task.Delay(50);

            holder.Value.Store(3);
            holder.Value.NotifyAll();

            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            pin.Free();
        }
    }

    [Fact]
    public async Task WaitAsync_Cancel_UnlinksCleanly()
    {
        var holder = new IntHolder();
        GCHandle pin = GCHandle.Alloc(holder, GCHandleType.Pinned);
        try
        {
            using var cts = new CancellationTokenSource();
            var waiter = holder.Value.WaitAsync(0, cts.Token).AsTask();
            await Task.Delay(50);
            await cts.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => waiter);

            // The lane is reusable: a fresh waiter still wakes.
            var second = holder.Value.WaitAsync(0).AsTask();
            await Task.Delay(20);
            holder.Value.Store(2);
            holder.Value.NotifyOne();
            await second.WaitAsync(TimeSpan.FromSeconds(15));
            holder.Value.Value.Should().Be(2);
        }
        finally
        {
            pin.Free();
        }
    }

    [Fact]
    public async Task WaitAsync_PreCanceled_ReturnsCanceled()
    {
        var atomic = new Atomic<int>(0);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await atomic.WaitAsync(0, cts.Token));
    }
}
