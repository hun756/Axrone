using System.Buffers;

namespace Axrone.Memory.Tests.Memory;

[Collection("MemoryPool")]
public sealed class SpecializedPoolTests
{
    // ──────────────────────────────────────────────
    //  ByteMemoryPool
    // ──────────────────────────────────────────────

    [Fact]
    public void ByteMemoryPool_Shared_IsNotNull()
    {
        ByteMemoryPool.Shared.Should().NotBeNull();
    }

    [Fact]
    public void ByteMemoryPool_Shared_IsSameInstance()
    {
        var a = ByteMemoryPool.Shared;
        var b = ByteMemoryPool.Shared;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void ByteMemoryPool_ImplementsIMemoryPoolOfByte()
    {
        using var pool = new ByteMemoryPool();
        pool.Should().BeAssignableTo<IMemoryPool<byte>>();
    }

    [Fact]
    public void ByteMemoryPool_Rent_ReturnsBufferWithAtLeastRequestedSize()
    {
        using var pool = new ByteMemoryPool();
        using var owner = pool.Rent(128);

        owner.Should().NotBeNull();
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(128);
    }

    [Fact]
    public void ByteMemoryPool_RentAndReturn_WorksCorrectly()
    {
        using var pool = new ByteMemoryPool();
        using var owner = pool.Rent(64);
        owner.Memory.Span[0] = 0xAB;
        owner.Memory.Span[0].Should().Be(0xAB);
    }

    [Fact]
    public void ByteMemoryPool_GetMetrics_ReturnsValidSnapshot()
    {
        using var pool = new ByteMemoryPool();
        using var owner = pool.Rent(64);

        var metrics = pool.GetMetrics();
        metrics.Should().NotBeNull();
        metrics.ActiveBuffers.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void ByteMemoryPool_GetDiagnostics_ReturnsValidSnapshot()
    {
        using var pool = new ByteMemoryPool();
        using var owner = pool.Rent(64);

        var diag = pool.GetDiagnostics();
        diag.Should().NotBeNull();
    }

    [Fact]
    public void ByteMemoryPool_TryRent_Succeeds()
    {
        using var pool = new ByteMemoryPool();
        pool.TryRent(64, out var owner).Should().BeTrue();
        owner.Should().NotBeNull();
        owner!.Dispose();
    }

    [Fact]
    public void ByteMemoryPool_MaxBufferSize_IsPositive()
    {
        using var pool = new ByteMemoryPool();
        pool.MaxBufferSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ByteMemoryPool_ConstructorWithOptions_Works()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithMaxBufferSize(16384);
        using var pool = new ByteMemoryPool(options);

        pool.MaxBufferSize.Should().Be(16384);
    }

    [Fact]
    public void ByteMemoryPool_GetWriter_ReturnsByteMemoryWriter()
    {
        using var pool = new ByteMemoryPool();
        using var writer = pool.GetWriter();

        writer.Should().NotBeNull();
        writer.Should().BeAssignableTo<IBufferWriter<byte>>();
        writer.Written.Should().Be(0);
    }

    // ──────────────────────────────────────────────
    //  CharMemoryPool
    // ──────────────────────────────────────────────

    [Fact]
    public void CharMemoryPool_Shared_IsNotNull()
    {
        CharMemoryPool.Shared.Should().NotBeNull();
    }

    [Fact]
    public void CharMemoryPool_Shared_IsSameInstance()
    {
        var a = CharMemoryPool.Shared;
        var b = CharMemoryPool.Shared;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void CharMemoryPool_ImplementsIMemoryPoolOfChar()
    {
        using var pool = new CharMemoryPool();
        pool.Should().BeAssignableTo<IMemoryPool<char>>();
    }

    [Fact]
    public void CharMemoryPool_Rent_ReturnsBufferWithAtLeastRequestedSize()
    {
        using var pool = new CharMemoryPool();
        using var owner = pool.Rent(128);

        owner.Should().NotBeNull();
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(128);
    }

    [Fact]
    public void CharMemoryPool_GetMetrics_ReturnsValidSnapshot()
    {
        using var pool = new CharMemoryPool();
        using var owner = pool.Rent(64);

        var metrics = pool.GetMetrics();
        metrics.Should().NotBeNull();
        metrics.ActiveBuffers.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CharMemoryPool_GetDiagnostics_ReturnsValidSnapshot()
    {
        using var pool = new CharMemoryPool();
        using var owner = pool.Rent(64);

        var diag = pool.GetDiagnostics();
        diag.Should().NotBeNull();
    }

    [Fact]
    public void CharMemoryPool_GetWriter_ReturnsCharMemoryWriter()
    {
        using var pool = new CharMemoryPool();
        using var writer = pool.GetWriter();

        writer.Should().NotBeNull();
        writer.Should().BeAssignableTo<IBufferWriter<char>>();
        writer.Written.Should().Be(0);
    }

    // ──────────────────────────────────────────────
    //  ByteMemoryWriter
    // ──────────────────────────────────────────────

    [Fact]
    public void ByteMemoryWriter_InitializesWithZeroWritten()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.Written.Should().Be(0);
        writer.Capacity.Should().BeGreaterThan(0);
        writer.FreeCapacity.Should().Be(writer.Capacity);
    }

    [Fact]
    public void ByteMemoryWriter_Write_SingleByte()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.Write(0x42);

        writer.Written.Should().Be(1);
        writer.WrittenSpan[0].Should().Be(0x42);
    }

    [Fact]
    public void ByteMemoryWriter_Write_Span()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        byte[] data = [1, 2, 3, 4, 5];
        writer.Write(data.AsSpan());

        writer.Written.Should().Be(5);
        writer.WrittenSpan.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public void ByteMemoryWriter_WriteInt32_WritesFourBytes()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.WriteInt32(0x01020304);

        writer.Written.Should().Be(4);
        var result = BitConverter.ToInt32(writer.WrittenSpan);
        result.Should().Be(0x01020304);
    }

    [Fact]
    public void ByteMemoryWriter_WriteInt16_WritesTwoBytes()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.WriteInt16(12345);

        writer.Written.Should().Be(2);
        var result = BitConverter.ToInt16(writer.WrittenSpan);
        result.Should().Be(12345);
    }

    [Fact]
    public void ByteMemoryWriter_WriteInt64_WritesEightBytes()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.WriteInt64(0x0102030405060708L);

        writer.Written.Should().Be(8);
        var result = BitConverter.ToInt64(writer.WrittenSpan);
        result.Should().Be(0x0102030405060708L);
    }

    [Fact]
    public void ByteMemoryWriter_AutoResize_WhenExceedingCapacity()
    {
        using var pool = new ByteMemoryPool();
        // Constructor clamps to min 1024, so start small and write past that
        using var writer = new ByteMemoryWriter(pool, initialCapacity: 1024);

        int initialCapacity = writer.Capacity;

        // Write more bytes than initial capacity to force resize
        byte[] chunk = new byte[600];
        writer.Write(chunk.AsSpan());
        writer.Write(chunk.AsSpan());

        writer.Written.Should().Be(1200);
        writer.Capacity.Should().BeGreaterThan(initialCapacity);
    }

    [Fact]
    public void ByteMemoryWriter_AutoResize_PreservesExistingData()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool, initialCapacity: 8);

        byte[] data = [10, 20, 30, 40, 50, 60, 70, 80];
        writer.Write(data.AsSpan());

        // Force resize
        writer.Write(new byte[] { 90, 100, 110, 120, 130 }.AsSpan());

        writer.Written.Should().Be(13);
        writer.WrittenSpan[0].Should().Be(10);
        writer.WrittenSpan[7].Should().Be(80);
        writer.WrittenSpan[8].Should().Be(90);
        writer.WrittenSpan[12].Should().Be(130);
    }

    [Fact]
    public void ByteMemoryWriter_ToArray_ReturnsWrittenBytes()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.Write(new byte[] { 0xAA, 0xBB, 0xCC }.AsSpan());

        writer.ToArray().Should().BeEquivalentTo(new byte[] { 0xAA, 0xBB, 0xCC });
    }

    [Fact]
    public void ByteMemoryWriter_ToArray_EmptyReturnsEmptyArray()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void ByteMemoryWriter_Clear_ResetsWrittenToZero()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.Write(new byte[] { 1, 2, 3 }.AsSpan());
        writer.Written.Should().Be(3);

        writer.Clear();

        writer.Written.Should().Be(0);
        writer.FreeCapacity.Should().Be(writer.Capacity);
    }

    [Fact]
    public void ByteMemoryWriter_CopyTo_CopiesWrittenBytes()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.Write(new byte[] { 10, 20, 30 }.AsSpan());

        var dest = new byte[3];
        writer.CopyTo(dest.AsSpan());

        dest.Should().BeEquivalentTo(new byte[] { 10, 20, 30 });
    }

    [Fact]
    public void ByteMemoryWriter_CopyTo_DestinationTooSmall_Throws()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.Write(new byte[] { 1, 2, 3 }.AsSpan());

        var act = () => writer.CopyTo(new byte[2].AsSpan());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ByteMemoryWriter_WrittenMemory_ReturnsOnlyWrittenPortion()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.Write(new byte[] { 5, 10, 15 }.AsSpan());

        writer.WrittenMemory.Length.Should().Be(3);
        writer.WrittenMemory.Span[1].Should().Be(10);
    }

    [Fact]
    public void ByteMemoryWriter_Dispose_SetsWrittenToZero()
    {
        using var pool = new ByteMemoryPool();
        var writer = new ByteMemoryWriter(pool);
        writer.Write(42);

        writer.Dispose();

        writer.Written.Should().Be(0);
        writer.Capacity.Should().Be(0);
    }

    [Fact]
    public void ByteMemoryWriter_Advance_MovesCursor()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        var span = writer.GetSpan(10);
        span[0] = 0xFF;
        writer.Advance(10);

        writer.Written.Should().Be(10);
    }

    [Fact]
    public void ByteMemoryWriter_AdvanceNegative_Throws()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        var act = () => writer.Advance(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ByteMemoryWriter_ImplementsIBufferWriter()
    {
        using var pool = new ByteMemoryPool();
        using var writer = new ByteMemoryWriter(pool);

        writer.Should().BeAssignableTo<IBufferWriter<byte>>();
        writer.Should().BeAssignableTo<IDisposable>();
    }

    // ──────────────────────────────────────────────
    //  CharMemoryWriter
    // ──────────────────────────────────────────────

    [Fact]
    public void CharMemoryWriter_InitializesWithZeroWritten()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Written.Should().Be(0);
        writer.Capacity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CharMemoryWriter_Write_SingleChar()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Write('A');

        writer.Written.Should().Be(1);
        writer.WrittenSpan[0].Should().Be('A');
    }

    [Fact]
    public void CharMemoryWriter_Write_Span()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Write("Hello".AsSpan());

        writer.Written.Should().Be(5);
        writer.ToString().Should().Be("Hello");
    }

    [Fact]
    public void CharMemoryWriter_Write_String()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Write("World");

        writer.Written.Should().Be(5);
        writer.ToString().Should().Be("World");
    }

    [Fact]
    public void CharMemoryWriter_Write_NullString_WritesNothing()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Write((string?)null);

        writer.Written.Should().Be(0);
    }

    [Fact]
    public void CharMemoryWriter_Write_EmptyString_WritesNothing()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Write(string.Empty);

        writer.Written.Should().Be(0);
    }

    [Fact]
    public void CharMemoryWriter_ToString_ReturnsWrittenContent()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Write("Test");
        writer.Write("123");

        writer.ToString().Should().Be("Test123");
    }

    [Fact]
    public void CharMemoryWriter_ToString_EmptyReturnsEmptyString()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void CharMemoryWriter_AutoResize_WhenExceedingCapacity()
    {
        using var pool = new CharMemoryPool();
        // Constructor clamps to min 512, so start at that minimum
        using var writer = new CharMemoryWriter(pool, initialCapacity: 512);

        int initialCapacity = writer.Capacity;

        // Write more chars than initial capacity to force resize
        string longString = new string('A', 600);
        writer.Write(longString);

        writer.Capacity.Should().BeGreaterThan(initialCapacity);
        writer.ToString().Should().Be(longString);
    }

    [Fact]
    public void CharMemoryWriter_Clear_ResetsWrittenToZero()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Write("Hello");
        writer.Written.Should().Be(5);

        writer.Clear();

        writer.Written.Should().Be(0);
        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void CharMemoryWriter_WrittenMemory_ReturnsOnlyWrittenPortion()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Write("ABC");

        writer.WrittenMemory.Length.Should().Be(3);
        writer.WrittenMemory.Span[0].Should().Be('A');
        writer.WrittenMemory.Span[2].Should().Be('C');
    }

    [Fact]
    public void CharMemoryWriter_Dispose_SetsWrittenToZero()
    {
        using var pool = new CharMemoryPool();
        var writer = new CharMemoryWriter(pool);
        writer.Write('X');

        writer.Dispose();

        writer.Written.Should().Be(0);
        writer.Capacity.Should().Be(0);
    }

    [Fact]
    public void CharMemoryWriter_ImplementsIBufferWriter()
    {
        using var pool = new CharMemoryPool();
        using var writer = new CharMemoryWriter(pool);

        writer.Should().BeAssignableTo<IBufferWriter<char>>();
        writer.Should().BeAssignableTo<IDisposable>();
    }

    // ──────────────────────────────────────────────
    //  MemoryPoolExtensions
    // ──────────────────────────────────────────────

    [Fact]
    public void RentExact_Success_ReturnsBufferOfExactSize()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.RentExact(128);

        owner.Should().NotBeNull();
        owner.Memory.Length.Should().Be(128);
    }

    [Fact]
    public void RentExact_Failure_ThrowsOutOfMemoryException()
    {
        using var pool = new MemoryPool<byte>(
            new MemoryPoolOptions<byte>().WithMaxBufferSize(64));

        var act = () => pool.RentExact(999999);
        act.Should().Throw<OutOfMemoryException>();
    }

    [Fact]
    public void GetWriter_Extension_CreatesByteMemoryWriter()
    {
        IMemoryPool<byte> pool = new ByteMemoryPool();
        using var writer = pool.GetWriter();

        writer.Should().NotBeNull();
        writer.Should().BeAssignableTo<IBufferWriter<byte>>();
        writer.Dispose();
    }

    [Fact]
    public void GetCharWriter_Extension_CreatesCharMemoryWriter()
    {
        IMemoryPool<char> pool = new CharMemoryPool();
        using var writer = pool.GetCharWriter();

        writer.Should().NotBeNull();
        writer.Should().BeAssignableTo<IBufferWriter<char>>();
        writer.Dispose();
    }

    [Fact]
    public void Reset_Extension_TrimsAndResetsStatistics()
    {
        using var pool = new ByteMemoryPool();
        using var owner = pool.Rent(64);

        pool.Reset();

        var metrics = pool.GetMetrics();
        metrics.TotalRentedBuffers.Should().Be(0);
    }

    [Fact]
    public void AsMemory_ReturnsOwnersMemory()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(32);

        var mem = owner.AsMemory();
        mem.Length.Should().Be(owner.Memory.Length);
    }

    [Fact]
    public void AsSpan_ReturnsOwnersSpan()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(32);

        owner.Memory.Span[0] = 42;
        var span = owner.AsSpan();
        span[0].Should().Be(42);
    }

    [Fact]
    public void AsReadOnlyMemory_ReturnsOwnersReadOnlyMemory()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(32);

        owner.Memory.Span[5] = 99;
        var rom = owner.AsReadOnlyMemory();
        rom.Span[5].Should().Be(99);
    }

    [Fact]
    public void AsReadOnlySpan_ReturnsOwnersReadOnlySpan()
    {
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(32);

        owner.Memory.Span[3] = 77;
        var ros = owner.AsReadOnlySpan();
        ros[3].Should().Be(77);
    }

    // ──────────────────────────────────────────────
    //  IPinnedMemoryOwner<T>
    // ──────────────────────────────────────────────

    [Fact]
    public void IPinnedMemoryOwner_IsInterface()
    {
        typeof(IPinnedMemoryOwner<byte>).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IPinnedMemoryOwner_ExtendsIMemoryOwner()
    {
        typeof(IMemoryOwner<byte>).IsAssignableFrom(typeof(IPinnedMemoryOwner<byte>)).Should().BeTrue();
    }

    [Fact]
    public void IPinnedMemoryOwner_HasPointerProperty()
    {
        var prop = typeof(IPinnedMemoryOwner<byte>).GetProperty("Pointer");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be<IntPtr>();
    }

    [Fact]
    public void IPinnedMemoryOwner_HasIsPinnedProperty()
    {
        var prop = typeof(IPinnedMemoryOwner<byte>).GetProperty("IsPinned");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be<bool>();
    }
}
