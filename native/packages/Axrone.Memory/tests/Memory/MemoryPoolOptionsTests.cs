namespace Axrone.Memory.Tests.Memory;

using FluentAssertions;
using Xunit;

[Collection("MemoryPool")]
public sealed class MemoryPoolOptionsTests
{
    // ─── Default Values ──────────────────────────────────────────────────

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new MemoryPoolOptions<byte>();

        options.DefaultBufferSize.Should().Be(4096);
        options.MaxBufferSize.Should().Be(1048576);
        options.MinBufferSize.Should().Be(16);
        options.ClearMode.Should().Be(ClearMode.OnReturn);
        options.MaxBuffersPerBucket.Should().Be(32);
        options.AllocationStrategy.Should().Be(AllocationStrategy.PowerOfTwo);
        options.ChunkSize.Should().Be(128);
    }

    [Fact]
    public void DefaultBufferSize_HasExpectedValue()
    {
        new MemoryPoolOptions<byte>().DefaultBufferSize.Should().Be(4096);
    }

    [Fact]
    public void MaxBufferSize_HasExpectedValue()
    {
        new MemoryPoolOptions<byte>().MaxBufferSize.Should().Be(1048576);
    }

    [Fact]
    public void MinBufferSize_HasExpectedValue()
    {
        new MemoryPoolOptions<byte>().MinBufferSize.Should().Be(16);
    }

    [Fact]
    public void ClearMode_HasExpectedValue()
    {
        new MemoryPoolOptions<byte>().ClearMode.Should().Be(ClearMode.OnReturn);
    }

    [Fact]
    public void MaxBuffersPerBucket_HasExpectedValue()
    {
        new MemoryPoolOptions<byte>().MaxBuffersPerBucket.Should().Be(32);
    }

    [Fact]
    public void AllocationStrategy_HasExpectedValue()
    {
        new MemoryPoolOptions<byte>().AllocationStrategy.Should().Be(AllocationStrategy.PowerOfTwo);
    }

    [Fact]
    public void ChunkSize_HasExpectedValue()
    {
        new MemoryPoolOptions<byte>().ChunkSize.Should().Be(128);
    }

    // ─── Fluent Builder Methods ──────────────────────────────────────────

    [Fact]
    public void WithDefaultBufferSize_SetsProperty_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithDefaultBufferSize(8192);

        result.Should().BeSameAs(options);
        options.DefaultBufferSize.Should().Be(8192);
    }

    [Fact]
    public void WithMaxBufferSize_SetsProperty_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithMaxBufferSize(2048);

        result.Should().BeSameAs(options);
        options.MaxBufferSize.Should().Be(2048);
    }

    [Fact]
    public void WithMinBufferSize_SetsProperty_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithMinBufferSize(64);

        result.Should().BeSameAs(options);
        options.MinBufferSize.Should().Be(64);
    }

    [Fact]
    public void WithClearMode_SetsProperty_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithClearMode(ClearMode.Always);

        result.Should().BeSameAs(options);
        options.ClearMode.Should().Be(ClearMode.Always);
    }

    [Fact]
    public void WithMaxBuffersPerBucket_SetsProperty_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithMaxBuffersPerBucket(64);

        result.Should().BeSameAs(options);
        options.MaxBuffersPerBucket.Should().Be(64);
    }

    [Fact]
    public void WithAllocationStrategy_SetsProperty_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithAllocationStrategy(AllocationStrategy.Fibonacci);

        result.Should().BeSameAs(options);
        options.AllocationStrategy.Should().Be(AllocationStrategy.Fibonacci);
    }

    [Fact]
    public void WithChunkSize_SetsProperty_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithChunkSize(256);

        result.Should().BeSameAs(options);
        options.ChunkSize.Should().Be(256);
    }

    // ─── Chaining ────────────────────────────────────────────────────────

    [Fact]
    public void Chaining_MultipleWithMethods_ReturnsSameInstanceEachTime()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options
            .WithDefaultBufferSize(2048)
            .WithMaxBufferSize(65536)
            .WithMinBufferSize(32)
            .WithClearMode(ClearMode.OnRent)
            .WithMaxBuffersPerBucket(16)
            .WithAllocationStrategy(AllocationStrategy.Chunked)
            .WithChunkSize(512);

        result.Should().BeSameAs(options);
    }

    [Fact]
    public void Chaining_SetsAllPropertiesCorrectly()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithDefaultBufferSize(2048)
            .WithMaxBufferSize(65536)
            .WithMinBufferSize(32)
            .WithClearMode(ClearMode.OnRent)
            .WithMaxBuffersPerBucket(16)
            .WithAllocationStrategy(AllocationStrategy.Chunked)
            .WithChunkSize(512);

        options.DefaultBufferSize.Should().Be(2048);
        options.MaxBufferSize.Should().Be(65536);
        options.MinBufferSize.Should().Be(32);
        options.ClearMode.Should().Be(ClearMode.OnRent);
        options.MaxBuffersPerBucket.Should().Be(16);
        options.AllocationStrategy.Should().Be(AllocationStrategy.Chunked);
        options.ChunkSize.Should().Be(512);
    }

    // ─── Clone ───────────────────────────────────────────────────────────

    [Fact]
    public void Clone_CreatesNewInstance_WithIdenticalValues()
    {
        var original = new MemoryPoolOptions<byte>()
            .WithDefaultBufferSize(2048)
            .WithMaxBufferSize(65536)
            .WithMinBufferSize(32)
            .WithClearMode(ClearMode.Always)
            .WithMaxBuffersPerBucket(16)
            .WithAllocationStrategy(AllocationStrategy.Fibonacci)
            .WithChunkSize(256);

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.DefaultBufferSize.Should().Be(original.DefaultBufferSize);
        clone.MaxBufferSize.Should().Be(original.MaxBufferSize);
        clone.MinBufferSize.Should().Be(original.MinBufferSize);
        clone.ClearMode.Should().Be(original.ClearMode);
        clone.MaxBuffersPerBucket.Should().Be(original.MaxBuffersPerBucket);
        clone.AllocationStrategy.Should().Be(original.AllocationStrategy);
        clone.ChunkSize.Should().Be(original.ChunkSize);
    }

    [Fact]
    public void Clone_WithDefaultValues_ProducesIdenticalCopy()
    {
        var original = new MemoryPoolOptions<int>();

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.DefaultBufferSize.Should().Be(4096);
        clone.MaxBufferSize.Should().Be(1048576);
        clone.MinBufferSize.Should().Be(16);
        clone.ClearMode.Should().Be(ClearMode.OnReturn);
        clone.MaxBuffersPerBucket.Should().Be(32);
        clone.AllocationStrategy.Should().Be(AllocationStrategy.PowerOfTwo);
        clone.ChunkSize.Should().Be(128);
    }

    [Fact]
    public void Clone_ModifyingClone_DoesNotAffectOriginal()
    {
        var original = new MemoryPoolOptions<byte>()
            .WithDefaultBufferSize(2048)
            .WithMaxBufferSize(65536)
            .WithMinBufferSize(32)
            .WithClearMode(ClearMode.OnReturn)
            .WithMaxBuffersPerBucket(16)
            .WithAllocationStrategy(AllocationStrategy.PowerOfTwo)
            .WithChunkSize(128);

        var clone = original.Clone();
        clone.WithDefaultBufferSize(999)
             .WithMaxBufferSize(999)
             .WithMinBufferSize(999)
             .WithClearMode(ClearMode.Never)
             .WithMaxBuffersPerBucket(999)
             .WithAllocationStrategy(AllocationStrategy.Exact)
             .WithChunkSize(999);

        original.DefaultBufferSize.Should().Be(2048);
        original.MaxBufferSize.Should().Be(65536);
        original.MinBufferSize.Should().Be(32);
        original.ClearMode.Should().Be(ClearMode.OnReturn);
        original.MaxBuffersPerBucket.Should().Be(16);
        original.AllocationStrategy.Should().Be(AllocationStrategy.PowerOfTwo);
        original.ChunkSize.Should().Be(128);
    }

    // ─── Integration with MemoryPool ─────────────────────────────────────

    [Fact]
    public void Pool_RespectsCustomMinBufferSize()
    {
        var options = new MemoryPoolOptions<byte>().WithMinBufferSize(64);
        using var pool = new MemoryPool<byte>(options);

        using var owner = pool.Rent(1);
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(64);
    }

    [Fact]
    public void Pool_RespectsCustomMaxBufferSize()
    {
        var options = new MemoryPoolOptions<byte>().WithMaxBufferSize(8192);
        using var pool = new MemoryPool<byte>(options);

        pool.MaxBufferSize.Should().Be(8192);
    }

    [Fact]
    public void Pool_RespectsCustomClearMode_Never()
    {
        var options = new MemoryPoolOptions<byte>().WithClearMode(ClearMode.Never);
        using var pool = new MemoryPool<byte>(options);

        using (var owner = pool.Rent(64))
        {
            owner.Memory.Span.Fill(0xCD);
        }

        using var reused = pool.Rent(64);
        reused.Memory.Span[0].Should().Be(0xCD);
    }

    [Fact]
    public void Pool_RespectsCustomClearMode_Always()
    {
        var options = new MemoryPoolOptions<byte>().WithClearMode(ClearMode.Always);
        using var pool = new MemoryPool<byte>(options);

        using (var owner = pool.Rent(64))
        {
            owner.Memory.Span.Fill(0xEF);
        }

        using var reused = pool.Rent(64);
        reused.Memory.Span[0].Should().Be(0);
    }

    [Fact]
    public void Pool_RespectsMultipleCustomOptions()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithMinBufferSize(32)
            .WithMaxBufferSize(4096)
            .WithClearMode(ClearMode.OnRent)
            .WithMaxBuffersPerBucket(8)
            .WithAllocationStrategy(AllocationStrategy.Fibonacci);

        using var pool = new MemoryPool<byte>(options);

        pool.MaxBufferSize.Should().Be(4096);

        using var owner = pool.Rent(1);
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(32);
    }

    // ─── Edge Cases ──────────────────────────────────────────────────────

    [Fact]
    public void MinBufferSize_GreaterThanDefaultBufferSize_PoolClampsDefaultUpToMin()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithMinBufferSize(512)
            .WithDefaultBufferSize(128);

        using var pool = new MemoryPool<byte>(options);

        // Pool clamps: _minBufferSize = 512, _defaultBufferSize = Max(512, Min(128, max)) = 512
        using var owner = pool.Rent();
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(512);
    }

    [Fact]
    public void MaxBufferSize_LessThanDefaultBufferSize_PoolClampsDefaultDownToMax()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithMaxBufferSize(128)
            .WithDefaultBufferSize(4096);

        using var pool = new MemoryPool<byte>(options);

        // Pool clamps: _maxBufferSize = 128, _defaultBufferSize = Max(min, Min(4096, 128)) = 128
        pool.MaxBufferSize.Should().Be(128);

        using var owner = pool.Rent();
        owner.Memory.Length.Should().BeLessThanOrEqualTo(128);
    }

    [Fact]
    public void MinBufferSize_GreaterThanDefaultBufferSize_OptionsRetainOriginalValues()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithMinBufferSize(512)
            .WithDefaultBufferSize(128);

        // The options object itself keeps the values as set — clamping is in the pool.
        options.MinBufferSize.Should().Be(512);
        options.DefaultBufferSize.Should().Be(128);
    }

    [Fact]
    public void MaxBufferSize_LessThanDefaultBufferSize_OptionsRetainOriginalValues()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithMaxBufferSize(128)
            .WithDefaultBufferSize(4096);

        // The options object itself keeps the values as set — clamping is in the pool.
        options.MaxBufferSize.Should().Be(128);
        options.DefaultBufferSize.Should().Be(4096);
    }

    [Theory]
    [InlineData(ClearMode.Never)]
    [InlineData(ClearMode.OnReturn)]
    [InlineData(ClearMode.OnRent)]
    [InlineData(ClearMode.Always)]
    public void WithClearMode_AcceptsAllEnumValues(ClearMode mode)
    {
        var options = new MemoryPoolOptions<byte>().WithClearMode(mode);
        options.ClearMode.Should().Be(mode);
    }

    [Theory]
    [InlineData(AllocationStrategy.PowerOfTwo)]
    [InlineData(AllocationStrategy.Fibonacci)]
    [InlineData(AllocationStrategy.Chunked)]
    [InlineData(AllocationStrategy.Exact)]
    public void WithAllocationStrategy_AcceptsAllEnumValues(AllocationStrategy strategy)
    {
        var options = new MemoryPoolOptions<byte>().WithAllocationStrategy(strategy);
        options.AllocationStrategy.Should().Be(strategy);
    }
}
