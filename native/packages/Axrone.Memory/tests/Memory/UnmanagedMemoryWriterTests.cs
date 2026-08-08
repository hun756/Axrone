using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

/// <summary>
/// Tests for UnmanagedMemoryWriter — IBufferWriter&lt;byte&gt; over unmanaged memory.
/// </summary>
[Collection("MemoryPool")]
public sealed class UnmanagedMemoryWriterTests
{
    [Fact]
    public void Constructor_WithPool_CreatesWriter()
    {
        using var pool = new UnmanagedMemoryPool();

        using var writer = new UnmanagedMemoryWriter(pool);

        writer.Written.Should().Be(0);
        writer.Capacity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_NullPool_ThrowsArgumentNullException()
    {
        var action = () => new UnmanagedMemoryWriter(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Write_SingleByte_IncreasesWrittenCount()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        writer.Write(0x42);

        writer.Written.Should().Be(1);
    }

    [Fact]
    public void Write_MultipleBytes_TracksCount()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        writer.Write(1);
        writer.Write(2);
        writer.Write(3);

        writer.Written.Should().Be(3);
    }

    [Fact]
    public void Write_Span_CopiesData()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        var data = new byte[] { 10, 20, 30, 40, 50 };
        writer.Write(data.AsSpan());

        writer.Written.Should().Be(5);
    }

    [Fact]
    public void ToArray_ReturnsWrittenData()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        writer.Write(0xAA);
        writer.Write(0xBB);
        writer.Write(0xCC);

        var array = writer.ToArray();

        array.Should().Equal(0xAA, 0xBB, 0xCC);
    }

    [Fact]
    public void ToArray_EmptyWriter_ReturnsEmptyArray()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        var array = writer.ToArray();

        array.Should().BeEmpty();
    }

    [Fact]
    public void CopyTo_CopiesWrittenData()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        writer.Write(new byte[] { 1, 2, 3, 4 }.AsSpan());

        var dest = new byte[4];
        writer.CopyTo(dest.AsSpan());

        dest.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Clear_ResetsWrittenCount()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        writer.Write(new byte[] { 1, 2, 3 }.AsSpan());
        writer.Clear();

        writer.Written.Should().Be(0);
    }

    [Fact]
    public void GetSpan_ReturnsSpanWithAtLeastRequestedSize()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        var span = writer.GetSpan(100);

        span.Length.Should().BeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public void GetMemory_ReturnsValidMemoryBackedByUnmanagedMemory()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        var memory = writer.GetMemory(100);

        memory.Length.Should().BeGreaterThanOrEqualTo(100);

        // Write through the Memory<byte> and verify via GetSpan
        var span = memory.Span;
        span[0] = 0xAB;
        span[1] = 0xCD;
        writer.Advance(2);

        writer.Written.Should().Be(2);
        var result = writer.ToArray();
        result[0].Should().Be(0xAB);
        result[1].Should().Be(0xCD);
    }

    [Fact]
    public void Advance_MovesWrittenPointer()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        var span = writer.GetSpan(10);
        span[0] = 0xFF;
        writer.Advance(1);

        writer.Written.Should().Be(1);
    }

    [Fact]
    public void Advance_PastCapacity_Throws()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool, initialCapacity: 64);

        var action = () => writer.Advance(writer.Capacity + 1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AutoResize_WhenExceedingCapacity_GrowsBuffer()
    {
        using var pool = new UnmanagedMemoryPool(maxBufferSize: 1024 * 1024);
        using var writer = new UnmanagedMemoryWriter(pool, initialCapacity: 64);

        var data = new byte[200];
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)(i & 0xFF);

        writer.Write(data.AsSpan());

        writer.Written.Should().Be(200);
        writer.Capacity.Should().BeGreaterThanOrEqualTo(200);
    }

    [Fact]
    public void AutoResize_PreservesData()
    {
        using var pool = new UnmanagedMemoryPool(maxBufferSize: 1024 * 1024);
        using var writer = new UnmanagedMemoryWriter(pool, initialCapacity: 32);

        for (int i = 0; i < 100; i++)
            writer.Write((byte)i);

        var array = writer.ToArray();
        for (int i = 0; i < 100; i++)
            array[i].Should().Be((byte)i);
    }

    [Fact]
    public void FreeCapacity_IsCapacityMinusWritten()
    {
        using var pool = new UnmanagedMemoryPool();
        using var writer = new UnmanagedMemoryWriter(pool);

        writer.FreeCapacity.Should().Be(writer.Capacity);

        writer.Write(0x42);

        writer.FreeCapacity.Should().Be(writer.Capacity - 1);
    }

    [Fact]
    public void Dispose_PreventsFurtherWrites()
    {
        using var pool = new UnmanagedMemoryPool();
        var writer = new UnmanagedMemoryWriter(pool);
        writer.Dispose();

        var action = () => writer.Write(0x42);

        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        using var pool = new UnmanagedMemoryPool();
        var writer = new UnmanagedMemoryWriter(pool);

        var action1 = () => writer.Dispose();
        var action2 = () => writer.Dispose();

        action1.Should().NotThrow();
        action2.Should().NotThrow();
    }
}
