namespace Axrone.Utility.Tests.Concurrency;

using System.Runtime.InteropServices;
using Axrone.Utility.Concurrency;

public class AtomicsTests
{
    private struct AddConstant : IAtomicMutator<int>
    {
        public static int Mutate(in int current, in int argument) => current + argument;
    }

    private struct NextOdd : IAtomicTransformer<int, int>
    {
        public static int Transform(in int current) => current % 2 == 0 ? current + 1 : current;
    }

    [Fact]
    public void Atomic_LoadStoreExchange_RoundTrips()
    {
        var atomic = new Atomic<int>(5);

        atomic.Load().Should().Be(5);
        atomic.Exchange(10).Should().Be(5);
        atomic.Value.Should().Be(10);
        atomic.IsLockFree.Should().BeTrue();
        ((int)atomic).Should().Be(10);
    }

    [Fact]
    public void Atomic_CompareExchange_UpdatesOnMatch()
    {
        var atomic = new Atomic<long>(1L);
        long expected = 1L;

        atomic.CompareExchange(ref expected, 2L).Should().BeTrue();
        atomic.Value.Should().Be(2L);
    }

    [Fact]
    public void Atomic_MutateAndGet_CombinesAtomically()
    {
        var atomic = new Atomic<int>(10);

        atomic.MutateAndGet<AddConstant>(5).Should().Be(15);
        atomic.Value.Should().Be(15);
    }

    [Fact]
    public void Atomic_TransformAndGet_ComputesAtomically()
    {
        var atomic = new Atomic<int>(4);

        atomic.TransformAndGet<NextOdd>().Should().Be(5);
    }

    [Fact]
    public async Task Atomic_ContendedMutations_AreLossless()
    {
        var atomic = new Atomic<int>(0);
        const int Threads = 4;
        const int PerThread = 2500;
        var tasks = new Task[Threads];
        for (int t = 0; t < Threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < PerThread; i++)
                {
                    atomic.MutateAndGet<AddConstant>(1);
                }
            });
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));
        atomic.Value.Should().Be(Threads * PerThread);
    }

    [Fact]
    public void AtomicRef_AliasesSharedField()
    {
        int field = 0;
        var first = new AtomicRef<int>(ref field);
        var second = new AtomicRef<int>(ref field);

        first.Store(7);
        second.Load().Should().Be(7);

        int expected = 7;
        second.CompareExchange(ref expected, 9, MemoryOrder.SequentiallyConsistent, MemoryOrder.Relaxed).Should().BeTrue();
        first.Load().Should().Be(9);
        first.Exchange(11).Should().Be(9);
        field.Should().Be(11);
        first.IsLockFree.Should().BeTrue();

        int single = 11;
        first.CompareExchange(ref single, 12).Should().BeTrue();
        field.Should().Be(12);
    }

    private sealed class IntHolder
    {
        public int Field;
    }

    [Fact]
    public async Task AtomicRef_WaitsAndNotifies()
    {
        var holder = new IntHolder();
        GCHandle pin = GCHandle.Alloc(holder, GCHandleType.Pinned);
        try
        {
            var waiter = new AtomicRef<int>(ref holder.Field).WaitAsync(0).AsTask();
            await Task.Delay(50);

            var notifier = new AtomicRef<int>(ref holder.Field);
            notifier.Store(1);
            notifier.NotifyOne();

            await waiter.WaitAsync(TimeSpan.FromSeconds(15));
            holder.Field.Should().Be(1);
        }
        finally
        {
            pin.Free();
        }
    }
}
