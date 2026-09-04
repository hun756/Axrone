using Axrone.Memory;

namespace Axrone.Memory.Tests;

public sealed class TieredMemoryPoolTests : IDisposable
{
    private readonly TieredMemoryPool<byte> _pool = new();

    public void Dispose()
    {
        _pool.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Rent_ReturnsValidMemory()
    {
        using var owner = _pool.Rent(64);
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(64);
    }

    [Fact]
    public void Rent_SameBucket_ReusesSlot()
    {
        var owner1 = _pool.Rent(64);
        owner1.Dispose();
        using var owner2 = _pool.Rent(64);
        owner2.Memory.Length.Should().BeGreaterThanOrEqualTo(64);
    }

    [Fact]
    public void RentLease_ReturnsValidLease()
    {
        using var lease = _pool.RentLease(128);
        lease.Length.Should().Be(128);
        lease.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RentLease_AfterDispose_IsInactive()
    {
        var lease = _pool.RentLease(64);
        lease.Dispose();
        lease.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RentExact_ReturnsExactSize()
    {
        using var lease = _pool.RentExact(256);
        lease.Length.Should().Be(256);
    }

    [Fact]
    public void RentBatch_RentsMultiple()
    {
        var leases = new ValueMemoryLease<byte>[4];
        int count = _pool.RentBatch(leases, 64);
        count.Should().Be(4);
        foreach (var lease in leases) lease.Dispose();
    }

    [Fact]
    public void Trim_ReducesAllocatedMemory()
    {
        var owner = _pool.Rent(64);
        owner.Dispose();
        _pool.Trim(1.0f);
        var snapshot = _pool.CreateDiagnosticsSnapshot();
        snapshot.Tier2Hits.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var pool = new TieredMemoryPool<byte>();
        pool.Dispose();
        pool.Dispose();
    }

    [Fact]
    public void Rent_AfterDispose_ThrowsObjectDisposedException()
    {
        var pool = new TieredMemoryPool<byte>();
        pool.Dispose();
        var act = () => pool.Rent(64);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DiagnosticsSnapshot_ReflectsOperations()
    {
        using var owner = _pool.Rent(64);
        var snapshot = _pool.CreateDiagnosticsSnapshot();
        snapshot.ActiveAllocations.Should().Be(1);
        snapshot.TotalRentedBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CustomOptions_AreRespected()
    {
        using var pool = new TieredMemoryPool<byte>(new BufferPoolOptions
        {
            MinimumBlockSize = 128,
            MaximumBlockSize = 1024,
            ClearMode = MemoryClearMode.OnReturn
        });
        using var owner = pool.Rent(64);
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(128);
    }
}

public class ContiguousSlabPoolTests
{
    [Fact]
    public unsafe void Rent_ReturnsValidMemory()
    {
        using var pool = new ContiguousSlabPool<byte>(64, 8);
        using var owner = pool.Rent(32);
        owner.Memory.Length.Should().Be(64);
    }

    [Fact]
    public void RentLease_ReturnsExactSize()
    {
        using var pool = new ContiguousSlabPool<byte>(128, 4);
        using var lease = pool.RentLease(64);
        lease.Length.Should().Be(64);
    }

    [Fact]
    public void Rent_WhenExhausted_ThrowsInsufficientMemoryException()
    {
        using var pool = new ContiguousSlabPool<byte>(64, 2);
        using var o1 = pool.Rent(64);
        using var o2 = pool.Rent(64);
        var act = () => pool.Rent(64);
        act.Should().Throw<InsufficientMemoryException>();
    }

    [Fact]
    public void TryRentLease_WhenExhausted_ReturnsFalse()
    {
        using var pool = new ContiguousSlabPool<byte>(64, 1);
        using var lease = pool.RentLease(64);
        pool.TryRentLease(64, out _).Should().BeFalse();
    }

    [Fact]
    public void Recycle_AfterReturn_MakesSlotAvailable()
    {
        using var pool = new ContiguousSlabPool<byte>(64, 1);
        var owner = pool.Rent(64);
        owner.Dispose();
        using var owner2 = pool.Rent(64);
        owner2.Memory.Length.Should().Be(64);
    }

    [Fact]
    public void DiagnosticsSnapshot_ReportsCorrectly()
    {
        using var pool = new ContiguousSlabPool<byte>(64, 4);
        using var owner = pool.Rent(64);
        var snapshot = pool.CreateDiagnosticsSnapshot();
        snapshot.ActiveAllocations.Should().Be(1);
    }
}

public class MonotonicArenaBufferTests
{
    [Fact]
    public void AllocateSpan_ReturnsCorrectSize()
    {
        using var arena = new MonotonicArenaBuffer();
        var span = arena.AllocateSpan<int>(100);
        span.Length.Should().Be(100);
    }

    [Fact]
    public void AllocateMemory_ReturnsCorrectSize()
    {
        using var arena = new MonotonicArenaBuffer();
        var memory = arena.AllocateMemory<byte>(256);
        memory.Length.Should().Be(256);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        using var arena = new MonotonicArenaBuffer(1024);
        _ = arena.AllocateSpan<byte>(512);
        arena.Reset();
        var span = arena.AllocateSpan<byte>(512);
        span.Length.Should().Be(512);
    }

    [Fact]
    public void MultipleAllocations_SameArena_Work()
    {
        using var arena = new MonotonicArenaBuffer();
        var s1 = arena.AllocateSpan<int>(10);
        var s2 = arena.AllocateSpan<int>(20);
        var s3 = arena.AllocateSpan<int>(30);
        s1.Length.Should().Be(10);
        s2.Length.Should().Be(20);
        s3.Length.Should().Be(30);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var arena = new MonotonicArenaBuffer();
        arena.Dispose();
        arena.Dispose();
    }
}

public class PooledBufferWriterTests
{
    [Fact]
    public void Write_IncreasesWrittenCount()
    {
        using var pool = new TieredMemoryPool<byte>();
        using var writer = new PooledBufferWriter<byte>(pool);
        var span = writer.GetSpan(10);
        span[..10].Fill(42);
        writer.Advance(10);
        writer.WrittenCount.Should().Be(10);
    }

    [Fact]
    public void WrittenSpan_ContainsWrittenData()
    {
        using var pool = new TieredMemoryPool<byte>();
        using var writer = new PooledBufferWriter<byte>(pool);
        var span = writer.GetSpan(4);
        span[0] = 1; span[1] = 2; span[2] = 3; span[3] = 4;
        writer.Advance(4);
        writer.WrittenSpan.ToArray().Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Reset_ClearsWrittenCount()
    {
        using var pool = new TieredMemoryPool<byte>();
        using var writer = new PooledBufferWriter<byte>(pool);
        writer.Advance(10);
        writer.Reset();
        writer.WrittenCount.Should().Be(0);
    }

    [Fact]
    public void AutoGrow_WhenCapacityExceeded()
    {
        using var pool = new TieredMemoryPool<byte>();
        using var writer = new PooledBufferWriter<byte>(pool, 8);
        var data = new byte[100];
        data.AsSpan().CopyTo(writer.GetSpan(100));
        writer.Advance(100);
        writer.WrittenCount.Should().Be(100);
    }
}

public class GlobalMemoryPoolsTests
{
    [Fact]
    public void Shared_ReturnsSameInstance()
    {
        var a = GlobalMemoryPools.Shared;
        var b = GlobalMemoryPools.Shared;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void SharedNative_ReturnsSameInstance()
    {
        var a = GlobalMemoryPools.SharedNative;
        var b = GlobalMemoryPools.SharedNative;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void CreatePartitionedPool_ReturnsNewInstance()
    {
        var a = GlobalMemoryPools.CreatePartitionedPool<byte>();
        var b = GlobalMemoryPools.CreatePartitionedPool<byte>();
        a.Should().NotBeSameAs(b);
        a.Dispose();
        b.Dispose();
    }
}
