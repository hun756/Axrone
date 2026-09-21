namespace Axrone.Utility.Tests.Concurrency;

using Axrone.Utility.Concurrency;

public class AtomicCoreOpsTests
{
    private struct TwelveBytes
    {
        public long A;
        public int B;
    }

    [Fact]
    public void LoadStore_RoundTripsInt()
    {
        int location = 0;

        AtomicCoreOps.Store(ref location, 42, MemoryOrder.SequentiallyConsistent);

        AtomicCoreOps.Load(ref location, MemoryOrder.Acquire).Should().Be(42);
    }

    [Fact]
    public void LoadStore_RoundTripsSubWords()
    {
        byte b = 0;
        ushort s = 0;

        AtomicCoreOps.Store(ref b, (byte)7, MemoryOrder.SequentiallyConsistent);
        AtomicCoreOps.Store(ref s, (ushort)300, MemoryOrder.SequentiallyConsistent);

        AtomicCoreOps.Load(ref b, MemoryOrder.Acquire).Should().Be(7);
        AtomicCoreOps.Load(ref s, MemoryOrder.Acquire).Should().Be(300);
    }

    [Fact]
    public void LoadStore_Relaxed_SkipsFences()
    {
        long location = 0;

        AtomicCoreOps.Store(ref location, 9L, MemoryOrder.Relaxed);

        AtomicCoreOps.Load(ref location, MemoryOrder.Relaxed).Should().Be(9L);
    }

    [Fact]
    public void Load_RejectsReleaseOrder()
    {
        int location = 0;
        var act = () => AtomicCoreOps.Load(ref location, MemoryOrder.Release);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Exchange_ReturnsPrevious()
    {
        long location = 5L;

        AtomicCoreOps.Exchange(ref location, 10L, MemoryOrder.SequentiallyConsistent).Should().Be(5L);
        location.Should().Be(10L);
    }

    [Fact]
    public void CompareExchange_Success_UpdatesAndReports()
    {
        int location = 1;
        int expected = 1;

        AtomicCoreOps.CompareExchange(ref location, ref expected, 2, MemoryOrder.SequentiallyConsistent, MemoryOrder.Relaxed)
            .Should().BeTrue();
        location.Should().Be(2);
    }

    [Fact]
    public void CompareExchange_Mismatch_RefreshesExpected()
    {
        int location = 1;
        int expected = 99;

        AtomicCoreOps.CompareExchange(ref location, ref expected, 2, MemoryOrder.SequentiallyConsistent, MemoryOrder.Relaxed)
            .Should().BeFalse();
        location.Should().Be(1);
        expected.Should().Be(1);
    }

    [Fact]
    public void CompareExchange_Byte_Works()
    {
        byte location = 3;
        byte expected = 3;

        AtomicCoreOps.CompareExchange(ref location, ref expected, (byte)4, MemoryOrder.SequentiallyConsistent, MemoryOrder.Relaxed)
            .Should().BeTrue();
        location.Should().Be(4);
    }

    [Fact]
    public void LargeStruct_FallsBackToLock()
    {
        var location = new TwelveBytes { A = 1L, B = 2 };
        var expected = new TwelveBytes { A = 1L, B = 2 };

        AtomicCoreOps.CompareExchange(ref location, ref expected, new TwelveBytes { A = 10L, B = 20 }, MemoryOrder.SequentiallyConsistent, MemoryOrder.Relaxed)
            .Should().BeTrue();
        location.A.Should().Be(10L);
        location.B.Should().Be(20);
    }

    [Fact]
    public void RefOperations_RoundTrip()
    {
        string? location = null;
        var first = "a";
        var second = "b";
        string? expected = first;

        AtomicCoreOps.StoreRef(ref location, first, MemoryOrder.Release);
        AtomicCoreOps.LoadRef(ref location, MemoryOrder.Acquire).Should().BeSameAs(first);
        AtomicCoreOps.ExchangeRef(ref location, second, MemoryOrder.SequentiallyConsistent).Should().BeSameAs(first);
        AtomicCoreOps.CompareExchangeRef(ref location, ref expected, first, MemoryOrder.SequentiallyConsistent, MemoryOrder.Relaxed)
            .Should().BeFalse();
        expected.Should().BeSameAs(second);
    }

    [Fact]
    public async Task ConcurrentIncrements_AreLossless()
    {
        int location = 0;
        const int Threads = 4;
        const int PerThread = 2500;
        var tasks = new Task[Threads];
        for (int t = 0; t < Threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < PerThread; i++)
                {
                    int current = AtomicCoreOps.Load(ref location, MemoryOrder.Relaxed);
                    while (!AtomicCoreOps.CompareExchange(ref location, ref current, current + 1, MemoryOrder.SequentiallyConsistent, MemoryOrder.Relaxed))
                    {
                    }
                }
            });
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));
        location.Should().Be(Threads * PerThread);
    }
}
