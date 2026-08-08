namespace Axrone.Memory.Tests.Memory;

public sealed class CorePoolContractTests
{
    [Fact]
    public void Rent_ReturnsOwnerWithAtLeastRequestedSize()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(256);

        owner.Should().NotBeNull();
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(256);
    }

    [Fact]
    public void Rent_DefaultSize_UsesConfiguredDefault()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent();

        owner.Memory.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Rent_SmallRequest_ClampsToMinimumBufferSize()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(1);

        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void RentedBuffer_IsWritableAndReadable()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(128);

        var span = owner.Memory.Span;
        for (int i = 0; i < 128; i++)
            span[i] = (byte)i;

        for (int i = 0; i < 128; i++)
            span[i].Should().Be((byte)i);
    }

    [Fact]
    public void RentMultiple_ReturnsIndependentBuffers()
    {
        using var pool = new MemoryPool<byte>();
        using var a = pool.Rent(64);
        using var b = pool.Rent(64);

        a.Memory.Span[0] = 42;
        b.Memory.Span[0] = 99;

        a.Memory.Span[0].Should().Be(42);
        b.Memory.Span[0].Should().Be(99);
    }

    [Fact]
    public void ClearMode_OnReturn_BufferIsZeroedAfterDispose()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>().WithClearMode(ClearMode.OnReturn));

        using (var owner = pool.Rent(64))
        {
            owner.Memory.Span.Fill(0xFF);
        }

        using var reused = pool.Rent(64);
        reused.Memory.Span[0].Should().Be(0);
    }

    [Fact]
    public void ClearMode_OnRent_BufferIsZeroedWhenRented()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>().WithClearMode(ClearMode.OnRent));

        using (var owner = pool.Rent(64))
        {
            owner.Memory.Span.Fill(0xFF);
        }

        using var reused = pool.Rent(64);
        reused.Memory.Span[0].Should().Be(0);
    }

    [Fact]
    public void ClearMode_Never_BufferRetainsData()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>().WithClearMode(ClearMode.Never));

        using (var owner = pool.Rent(64))
        {
            owner.Memory.Span.Fill(0xAB);
        }

        using var reused = pool.Rent(64);
        reused.Memory.Span[0].Should().Be(0xAB);
    }

    [Fact]
    public void Dispose_ThenRent_ThrowsObjectDisposedException()
    {
        var pool = new MemoryPool<byte>();
        pool.Dispose();

        var act = () => pool.Rent(64);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var pool = new MemoryPool<byte>();
        pool.Dispose();
        var act = () => pool.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Owner_Span_And_Memory_ReturnSameData()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(32);

        owner.Memory.Span[0] = 77;
        owner.Span[0].Should().Be(77);
    }

    [Fact]
    public void Owner_ReadOnlyMemory_Matches_Memory()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(32);

        owner.Memory.Span[5] = 123;
        owner.ReadOnlyMemory.Span[5].Should().Be(123);
    }

    [Fact]
    public void Owner_Length_Matches_MemoryLength()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(100);

        owner.Length.Should().Be(owner.Memory.Length);
    }

    [Fact]
    public void TryRent_ReturnsTrue_WhenPoolHasCapacity()
    {
        using var pool = new MemoryPool<byte>();
        pool.TryRent(64, out var owner).Should().BeTrue();
        owner.Should().NotBeNull();
        owner!.Dispose();
    }

    [Fact]
    public void MaxBufferSize_ReflectsConfiguration()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>().WithMaxBufferSize(8192));

        pool.MaxBufferSize.Should().Be(8192);
    }

    [Fact]
    public void Rent_ExceedingMaxBufferSize_ThrowsOrReturnsFalse()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>().WithMaxBufferSize(256));

        var act = () => pool.Rent(1024);
        act.Should().Throw<OutOfMemoryException>();
    }

    [Fact]
    public void GenericPool_WorksWithInt()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(16);

        owner.Memory.Span[0] = 42;
        owner.Memory.Span[0].Should().Be(42);
    }

    [Fact]
    public void GenericPool_WorksWithStruct()
    {
        using var pool = new MemoryPool<TestStruct>();
        using var owner = pool.Rent(4);

        owner.Memory.Span[0] = new TestStruct { X = 1, Y = 2 };
        owner.Memory.Span[0].X.Should().Be(1);
        owner.Memory.Span[0].Y.Should().Be(2);
    }

    private struct TestStruct
    {
        public int X;
        public int Y;
    }
}
