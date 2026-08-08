using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

/// <summary>
/// Tests for VectorizedOperations — SIMD-accelerated clear and copy.
/// </summary>
[Collection("MemoryPool")]
public sealed class VectorizedOperationsTests
{
    [Fact]
    public unsafe void ClearMemory_SmallBuffer_ZeroesAll()
    {
        var buffer = new byte[32];
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = 0xFF;

        fixed (byte* ptr = buffer)
        {
            VectorizedOperations.ClearMemory(ptr, buffer.Length);
        }

        buffer.Should().AllSatisfy(b => b.Should().Be(0));
    }

    [Fact]
    public unsafe void ClearMemory_LargeBuffer_ZeroesAll()
    {
        var buffer = new byte[1024];
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = 0xFF;

        fixed (byte* ptr = buffer)
        {
            VectorizedOperations.ClearMemory(ptr, buffer.Length);
        }

        buffer.Should().AllSatisfy(b => b.Should().Be(0));
    }

    [Fact]
    public unsafe void ClearMemory_MediumBuffer_ZeroesAll()
    {
        var buffer = new byte[64];
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = 0xAA;

        fixed (byte* ptr = buffer)
        {
            VectorizedOperations.ClearMemory(ptr, buffer.Length);
        }

        buffer.Should().AllSatisfy(b => b.Should().Be(0));
    }

    [Fact]
    public unsafe void ClearMemory_NonAlignedSize_ZeroesAll()
    {
        var buffer = new byte[100];
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = 0x55;

        fixed (byte* ptr = buffer)
        {
            VectorizedOperations.ClearMemory(ptr, buffer.Length);
        }

        buffer.Should().AllSatisfy(b => b.Should().Be(0));
    }

    [Fact]
    public unsafe void CopyMemory_SmallBuffer_CopiesCorrectly()
    {
        var source = new byte[32];
        var dest = new byte[32];
        for (int i = 0; i < source.Length; i++)
            source[i] = (byte)i;

        fixed (byte* src = source)
        fixed (byte* dst = dest)
        {
            VectorizedOperations.CopyMemory(src, dst, source.Length);
        }

        dest.Should().Equal(source);
    }

    [Fact]
    public unsafe void CopyMemory_LargeBuffer_CopiesCorrectly()
    {
        var source = new byte[2048];
        var dest = new byte[2048];
        for (int i = 0; i < source.Length; i++)
            source[i] = (byte)(i & 0xFF);

        fixed (byte* src = source)
        fixed (byte* dst = dest)
        {
            VectorizedOperations.CopyMemory(src, dst, source.Length);
        }

        dest.Should().Equal(source);
    }

    [Fact]
    public unsafe void CopyMemory_MediumBuffer_CopiesCorrectly()
    {
        var source = new byte[64];
        var dest = new byte[64];
        for (int i = 0; i < source.Length; i++)
            source[i] = (byte)(i * 2);

        fixed (byte* src = source)
        fixed (byte* dst = dest)
        {
            VectorizedOperations.CopyMemory(src, dst, source.Length);
        }

        dest.Should().Equal(source);
    }

    [Fact]
    public unsafe void CopyMemory_NonAlignedSize_CopiesCorrectly()
    {
        var source = new byte[100];
        var dest = new byte[100];
        for (int i = 0; i < source.Length; i++)
            source[i] = (byte)(i % 256);

        fixed (byte* src = source)
        fixed (byte* dst = dest)
        {
            VectorizedOperations.CopyMemory(src, dst, source.Length);
        }

        dest.Should().Equal(source);
    }

    [Fact]
    public unsafe void ClearMemory_ZeroSize_DoesNotThrow()
    {
        var buffer = new byte[16];

        fixed (byte* ptr = buffer)
        {
            VectorizedOperations.ClearMemory(ptr, 0);
        }
    }

    [Fact]
    public unsafe void CopyMemory_ZeroSize_DoesNotThrow()
    {
        var source = new byte[16];
        var dest = new byte[16];

        fixed (byte* src = source)
        fixed (byte* dst = dest)
        {
            VectorizedOperations.CopyMemory(src, dst, 0);
        }
    }
}
