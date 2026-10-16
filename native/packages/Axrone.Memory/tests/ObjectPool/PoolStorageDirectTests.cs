using Axrone.Memory.ObjectPool;

namespace Axrone.Memory.Tests;

public sealed class PoolStorageDirectTests
{
    private sealed class Item
    {
    }

    private static PooledItem<Item> Pack(Item instance) =>
        new(instance, DateTime.UtcNow, 0, 0, 1);

    [Fact]
    public void CentralizedStorage_Contains_Clear_RoundTrip()
    {
        using var storage = new CentralizedQueuePoolStorage<Item>();
        var kept = new Item();
        var other = new Item();
        storage.Enqueue(Pack(kept));
        storage.Enqueue(Pack(other));

        storage.Count.Should().Be(2);
        storage.Contains(kept).Should().BeTrue();
        storage.Contains(new Item()).Should().BeFalse();

        int cleared = 0;
        storage.Clear(_ => cleared++);
        cleared.Should().Be(2);
        storage.Count.Should().Be(0);
        storage.TryDequeue(out _).Should().BeFalse();
    }

    [Fact]
    public void ShardedBucket_Tracks_Count_AndContains()
    {
        var bucket = new ShardedBucket<Item>();
        var kept = new Item();
        bucket.Enqueue(Pack(kept));
        bucket.Count.Should().Be(1);
        bucket.ContainsInstance(kept).Should().BeTrue();
        bucket.TryDequeue(out PooledItem<Item> item).Should().BeTrue();
        item.Instance.Should().BeSameAs(kept);
        bucket.Count.Should().Be(0);
        bucket.TryDequeue(out _).Should().BeFalse();
    }

    [Fact]
    public void ShardedStorage_Steals_AcrossShards()
    {
        using var storage = new ShardedPoolStorage<Item>(4);
        var kept = new Item();
        storage.Enqueue(Pack(kept));
        storage.Count.Should().Be(1);
        storage.Contains(kept).Should().BeTrue();
        storage.TryDequeue(out PooledItem<Item> item).Should().BeTrue();
        item.Instance.Should().BeSameAs(kept);
    }

    [Fact]
    public void BoundedChannel_DropsOldest_WhenFull()
    {
        using var storage = new BoundedChannelPoolStorage<Item>(2);
        var first = new Item();
        storage.Enqueue(Pack(first));
        storage.Enqueue(Pack(new Item()));
        storage.Enqueue(Pack(new Item()));

        storage.Count.Should().Be(2);
        storage.Contains(first).Should().BeFalse();
    }

    [Fact]
    public async Task BoundedChannel_ClearAsync_Drains()
    {
        using var storage = new BoundedChannelPoolStorage<Item>(4);
        storage.Enqueue(Pack(new Item()));
        int cleared = 0;
        await storage.ClearAsync(_ => { cleared++; return ValueTask.CompletedTask; }, CancellationToken.None);
        cleared.Should().Be(1);
        storage.Count.Should().Be(0);
    }

    [Fact]
    public void LockFreeStorage_Contains_Clear_RoundTrip()
    {
        using var storage = new LockFreePoolStorage<Item>();
        var kept = new Item();
        storage.Enqueue(Pack(kept));
        storage.Contains(kept).Should().BeTrue();
        storage.TryDequeue(out PooledItem<Item> item).Should().BeTrue();
        item.Instance.Should().BeSameAs(kept);
        storage.Contains(kept).Should().BeFalse();
    }

    [Fact]
    public void ThreadLocalStorage_Clear_RemovesAll()
    {
        using var storage = new ThreadLocalPoolStorage<Item>();
        storage.Enqueue(Pack(new Item()));
        storage.Enqueue(Pack(new Item()));
        storage.Count.Should().Be(2);
        int cleared = 0;
        storage.Clear(_ => cleared++);
        cleared.Should().Be(2);
        storage.Count.Should().Be(0);
    }
}
