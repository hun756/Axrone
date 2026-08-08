namespace Axrone.Memory.Tests.Memory;

public sealed class AllocationStrategyTests
{
    [Fact]
    public void PowerOfTwo_RentsRoundUpToNearestPowerOfTwo()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>()
                .WithAllocationStrategy(AllocationStrategy.PowerOfTwo)
                .WithMinBufferSize(16)
                .WithMaxBufferSize(1024));

        using var owner = pool.Rent(100);

        owner.Memory.Length.Should().Be(128);
    }

    [Fact]
    public void PowerOfTwo_ExactPowerOfTwo_ReturnsExactSize()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>()
                .WithAllocationStrategy(AllocationStrategy.PowerOfTwo)
                .WithMinBufferSize(16)
                .WithMaxBufferSize(1024));

        using var owner = pool.Rent(64);

        owner.Memory.Length.Should().Be(64);
    }

    [Fact]
    public void Fibonacci_RentsRoundUpToNearestFibonacci()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>()
                .WithAllocationStrategy(AllocationStrategy.Fibonacci)
                .WithMinBufferSize(16)
                .WithMaxBufferSize(1024));

        using var owner = pool.Rent(20);

        // Fibonacci from 16: 16, 32, 48, 80, 128, ...
        owner.Memory.Length.Should().Be(32);
    }

    [Fact]
    public void Chunked_RentsRoundUpToNearestChunk()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>()
                .WithAllocationStrategy(AllocationStrategy.Chunked)
                .WithMinBufferSize(16)
                .WithMaxBufferSize(1024)
                .WithChunkSize(64));

        using var owner = pool.Rent(100);

        // 100 → next chunk boundary at 128 (64*2)
        owner.Memory.Length.Should().Be(128);
    }

    [Fact]
    public void Chunked_ExactChunk_ReturnsExactSize()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>()
                .WithAllocationStrategy(AllocationStrategy.Chunked)
                .WithMinBufferSize(16)
                .WithMaxBufferSize(1024)
                .WithChunkSize(64));

        using var owner = pool.Rent(64);

        owner.Memory.Length.Should().Be(64);
    }

    [Fact]
    public void Exact_OnlyCreatesMinMaxDefaultBuckets()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>()
                .WithAllocationStrategy(AllocationStrategy.Exact)
                .WithMinBufferSize(16)
                .WithDefaultBufferSize(256)
                .WithMaxBufferSize(1024));

        // Requesting min → gets min bucket
        using var minOwner = pool.Rent(16);
        minOwner.Memory.Length.Should().Be(16);

        // Requesting default → gets default bucket
        using var defOwner = pool.Rent(256);
        defOwner.Memory.Length.Should().Be(256);

        // Requesting max → gets max bucket
        using var maxOwner = pool.Rent(1024);
        maxOwner.Memory.Length.Should().Be(1024);
    }

    [Fact]
    public void DefaultStrategy_IsPowerOfTwo()
    {
        using var pool = new MemoryPool<byte>();

        // Default should use PowerOfTwo — 100 → 128
        using var owner = pool.Rent(100);
        owner.Memory.Length.Should().Be(128);
    }

    [Fact]
    public void Pool_ReusesBufferFromCorrectBucket()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>()
                .WithAllocationStrategy(AllocationStrategy.PowerOfTwo)
                .WithClearMode(ClearMode.Never));

        // Rent and return a 64-byte buffer
        using (var owner = pool.Rent(64))
        {
            owner.Memory.Span[0] = 0xAB;
            owner.Memory.Length.Should().Be(64);
        }

        // Next rent of similar size should reuse the same buffer
        using var reused = pool.Rent(64);
        reused.Memory.Length.Should().Be(64);
        reused.Memory.Span[0].Should().Be(0xAB);
    }
}
