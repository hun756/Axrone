using Axrone.Memory;

namespace Axrone.Memory.Tests;

public sealed class PooledBufferSlotTests
{
    private sealed class StubRegistry : IPoolBucketRegistry<byte>
    {
        public int RecycledCount;
        public int LastBucketIndex = -1;

        public void Recycle(int bucketIndex, PooledBufferSlot<byte> slot)
        {
            RecycledCount++;
            LastBucketIndex = bucketIndex;
        }
    }

    private static PooledBufferSlot<byte> CreateSlot(StubRegistry registry, MemoryClearMode mode = MemoryClearMode.Never)
    {
        Memory<byte> memory = ManagedArrayBlockAllocator<byte>.Instance.Allocate(16, out object? token);
        return new PooledBufferSlot<byte>(memory, token, 3, 16, mode, registry, ManagedArrayBlockAllocator<byte>.Instance);
    }

    [Fact]
    public void Memory_BeforeActivation_Throws()
    {
        var registry = new StubRegistry();
        PooledBufferSlot<byte> slot = CreateSlot(registry);
        Action read = () => _ = slot.Memory;
        read.Should().Throw<InvalidOperationException>();
        slot.FinalizeEviction();
    }

    [Fact]
    public void ActivateLease_EnablesAccess_AndClears_OnRent()
    {
        var registry = new StubRegistry();
        PooledBufferSlot<byte> slot = CreateSlot(registry, MemoryClearMode.OnRent);
        slot.Capacity.Should().Be(16);

        uint lease = slot.ActivateLease();
        slot.IsLeaseValid(lease).Should().BeTrue();
        slot.Span.Fill(0xFF);
        slot.Return(lease + 1000);
        slot.IsLeaseValid(lease).Should().BeTrue();

        uint next = slot.ActivateLease();
        next.Should().NotBe(lease);
        slot.Span.ToArray().Should().OnlyContain(b => b == 0);
        slot.Dispose();
        slot.FinalizeEviction();
    }

    [Fact]
    public void Return_Recycles_AndClears_OnReturn()
    {
        var registry = new StubRegistry();
        PooledBufferSlot<byte> slot = CreateSlot(registry, MemoryClearMode.OnReturn);

        uint lease = slot.ActivateLease();
        slot.Span.Fill(0xFF);
        slot.Return(lease);

        registry.RecycledCount.Should().Be(1);
        registry.LastBucketIndex.Should().Be(3);
        slot.IsLeaseValid(lease).Should().BeFalse();
        slot.FinalizeEviction();
    }

    [Fact]
    public void Dispose_RecyclesOnce_EvenWhenCalledTwice()
    {
        var registry = new StubRegistry();
        PooledBufferSlot<byte> slot = CreateSlot(registry);
        slot.ActivateLease();
        slot.Dispose();
        slot.Dispose();
        registry.RecycledCount.Should().Be(1);
        slot.FinalizeEviction();
    }

    [Fact]
    public void FinalizeEviction_FreesMemory_Idempotently()
    {
        var registry = new StubRegistry();
        PooledBufferSlot<byte> slot = CreateSlot(registry);
        slot.FinalizeEviction();
        slot.FinalizeEviction();
        registry.RecycledCount.Should().Be(0);
    }

    [Fact]
    public void PerThreadPartitionCache_Stores_AndRetrieves()
    {
        var registry = new StubPartitionRegistry();
        Memory<uint> memory = ManagedArrayBlockAllocator<uint>.Instance.Allocate(16, out object? token);
        var slot = new PooledBufferSlot<uint>(memory, token, 3, 16, MemoryClearMode.Never, registry, ManagedArrayBlockAllocator<uint>.Instance);

        PerThreadPartitionCache<uint>.Instance.TryStore(0, slot).Should().BeTrue();
        PerThreadPartitionCache<uint>.Instance.TryRetrieve(0, out PooledBufferSlot<uint>? retrieved).Should().BeTrue();
        retrieved.Should().BeSameAs(slot);
        PerThreadPartitionCache<uint>.Instance.TryRetrieve(0, out _).Should().BeFalse();
        PerThreadPartitionCache<uint>.Instance.TryStore(99, slot).Should().BeFalse();

        slot.FinalizeEviction();
    }

    private sealed class StubPartitionRegistry : IPoolBucketRegistry<uint>
    {
        public void Recycle(int bucketIndex, PooledBufferSlot<uint> slot)
        {
        }
    }

    [Fact]
    public void LeakDiagnostics_Tracks_ActiveTrackers()
    {
        (long active, _) = LeakDiagnostics.GetStats();
        var tracker = LeakDiagnostics.CreateTracker<byte>();
        LeakDiagnostics.GetStats().active.Should().BeGreaterThanOrEqualTo(active);
        LeakDiagnostics.DisposeTracker(tracker);
    }

    [Fact]
    public void PoolCounterStore_Aggregates_Cells()
    {
        PoolCounterStore.Current.Cell.Tier1Hits += 2;
        PoolCounterStore.Current.Cell.ActiveAllocations += 1;
        PoolDiagnosticsSnapshot snapshot = PoolCounterStore.Aggregate();
        snapshot.Tier1Hits.Should().BeGreaterThanOrEqualTo(2);
        snapshot.ActiveAllocations.Should().BeGreaterThanOrEqualTo(1);
        PoolCounterStore.Current.Cell.Tier1Hits -= 2;
        PoolCounterStore.Current.Cell.ActiveAllocations -= 1;
    }
}
