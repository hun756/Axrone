using Axrone.Memory;

namespace Axrone.Memory.Tests;

public sealed class MonotonicArenaBufferGapTests : IDisposable
{
    private readonly MonotonicArenaBuffer _arena = new(4096);

    public void Dispose()
    {
        _arena.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AllocateSpan_ReturnsWritableMemory()
    {
        Span<int> span = _arena.AllocateSpan<int>(16);
        span.Length.Should().Be(16);
        span.Fill(42);
        span[15].Should().Be(42);
    }

    [Fact]
    public void AllocateMemory_SurvivesReset_OnlyThroughNewAllocations()
    {
        Memory<byte> first = _arena.AllocateMemory<byte>(32);
        first.Span.Fill(1);
        _arena.Reset();
        Memory<byte> second = _arena.AllocateMemory<byte>(32);
        second.Length.Should().Be(32);
        second.Span.Fill(2);
        second.Span[0].Should().Be(2);
    }

    [Fact]
    public void Overflow_GrowsIntoNewSegment()
    {
        Span<byte> big = _arena.AllocateSpan<byte>(8192);
        big.Length.Should().Be(8192);
        Span<byte> after = _arena.AllocateSpan<byte>(8);
        after.Length.Should().Be(8);
    }

    [Fact]
    public void AfterDispose_Throws()
    {
        var arena = new MonotonicArenaBuffer(1024);
        arena.Dispose();
        Action allocate = () => _ = arena.AllocateSpan<byte>(8);
        allocate.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void InvalidCapacity_Throws()
    {
        Action create = () => _ = new MonotonicArenaBuffer(0);
        create.Should().Throw<ArgumentOutOfRangeException>();
    }
}

public sealed class GlobalMemoryPoolsGapTests
{
    [Fact]
    public void Shared_Pools_RentAndReturn()
    {
        using var owner = GlobalMemoryPools.Shared.Rent(128);
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(128);

        using var native = GlobalMemoryPools.SharedNative.Rent(128);
        native.Memory.Length.Should().BeGreaterThanOrEqualTo(128);
    }

    [Fact]
    public void Factories_CreateUsableInstances()
    {
        using var pool = GlobalMemoryPools.CreatePartitionedPool<byte>();
        using var owner = pool.Rent(64);
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(64);

        using var nativePool = GlobalMemoryPools.CreateNativePool<int>();
        using var native = nativePool.Rent(16);
        native.Memory.Length.Should().BeGreaterThanOrEqualTo(16);

        using var slab = GlobalMemoryPools.CreateSlabPool<float>(16, 4);
        using var rented = slab.Rent();
        rented.Memory.Length.Should().Be(16);

        using var arena = GlobalMemoryPools.CreateArena(4096);
        arena.AllocateSpan<long>(4).Length.Should().Be(4);

        using var writer = GlobalMemoryPools.CreateWriter(GlobalMemoryPools.Shared);
        Span<byte> span = writer.GetSpan(3);
        span[0] = 1;
        span[1] = 2;
        span[2] = 3;
        writer.Advance(3);
        writer.WrittenCount.Should().Be(3);
    }
}

public sealed class ContiguousSlabPoolGapTests : IDisposable
{
    private readonly ContiguousSlabPool<int> _slab = new(16, 4);

    public void Dispose()
    {
        _slab.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Rent_ReturnsFixedBlocks_UntilExhausted()
    {
        using var a = _slab.Rent();
        using var b = _slab.Rent();
        a.Memory.Length.Should().Be(16);
        b.Memory.Length.Should().Be(16);
    }

    [Fact]
    public void Oversized_Rent_Throws()
    {
        Action rent = () => _slab.Rent(64);
        rent.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RentLease_ExposesRequestedLength()
    {
        using ValueMemoryLease<int> lease = _slab.RentLease(8);
        lease.Length.Should().Be(8);
        lease.IsActive.Should().BeTrue();
    }

    [Fact]
    public void InvalidConstruction_Throws()
    {
        Action zeroBlock = () => _ = new ContiguousSlabPool<int>(0, 4);
        Action zeroCount = () => _ = new ContiguousSlabPool<int>(16, 0);
        zeroBlock.Should().Throw<ArgumentOutOfRangeException>();
        zeroCount.Should().Throw<ArgumentOutOfRangeException>();
    }
}
