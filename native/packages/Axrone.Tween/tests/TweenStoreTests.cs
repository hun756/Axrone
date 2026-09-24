namespace Axrone.Tween.Tests;

using System.Numerics;
using System.Runtime.Intrinsics;

public class TweenStoreTests
{
    private static void InitializeLane(TweenStore store, uint index)
    {
        store.Initialize(
            index,
            Vector128.Create(0f, 0f, 0f, 0f),
            Vector128.Create(1f, 1f, 1f, 1f),
            DurationNs.FromSeconds(1f),
            DurationNs.Zero,
            DurationNs.Zero,
            EasingKind.Linear,
            null,
            PlaybackMode.Once,
            1,
            1f,
            1,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    [Fact]
    public void AllocateInitializeFree_RoundTrips()
    {
        using var store = new TweenStore(8);

        store.TryAllocate(out uint index).Should().BeTrue();
        InitializeLane(store, index);

        store.ActiveCount.Should().Be(1);
        store.GetState((int)index).Should().Be(TweenState.Playing);
        store.Validate(new TweenId(index, 1)).Should().BeTrue();

        store.Free(index);
        store.ActiveCount.Should().Be(0);
        store.Validate(new TweenId(index, 1)).Should().BeFalse();
    }

    [Fact]
    public void Exhaustion_FailsAllocation()
    {
        using var store = new TweenStore(2);

        store.TryAllocate(out _).Should().BeTrue();
        store.TryAllocate(out _).Should().BeTrue();
        store.TryAllocate(out _).Should().BeFalse();
    }

    [Fact]
    public void DoubleFree_IsSafeNoOp()
    {
        using var store = new TweenStore(4);

        store.TryAllocate(out uint index).Should().BeTrue();
        InitializeLane(store, index);
        store.Free(index);
        store.Free(index);

        store.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        var act = () => new TweenStore(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ConcurrentAllocate_RecyclerKeepsCount()
    {
        const int Capacity = 64;
        const int Threads = 8;
        const int Iterations = 1000;
        using var store = new TweenStore(Capacity);

        var tasks = new Task[Threads];
        for (int t = 0; t < Threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < Iterations; i++)
                {
                    if (store.TryAllocate(out uint index))
                    {
                        InitializeLane(store, index);
                        store.Free(index);
                    }
                }
            });
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));

        int drained = 0;
        while (store.TryAllocate(out _))
        {
            drained++;
        }

        drained.Should().Be(Capacity);
    }
}
