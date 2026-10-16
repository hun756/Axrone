using System.Buffers;
using Axrone.Memory;

namespace Axrone.Memory.Tests;

public sealed class BlockAllocatorTests
{
    [Fact]
    public void ManagedAllocator_RoundTrips_ValueAndReferenceTypes()
    {
        Memory<int> values = ManagedArrayBlockAllocator<int>.Instance.Allocate(16, out object? token);
        token.Should().BeNull();
        values.Length.Should().Be(16);
        values.Span.Fill(7);
        values.Span[3].Should().Be(7);
        ManagedArrayBlockAllocator<int>.Instance.Free(values, token);

        Memory<string> refs = ManagedArrayBlockAllocator<string>.Instance.Allocate(4, out token);
        refs.Length.Should().Be(4);
        ManagedArrayBlockAllocator<string>.Instance.Free(refs, token);
    }

    [Fact]
    public void PinnedAllocator_ReturnsAddressableMemory()
    {
        Memory<byte> memory = PinnedHeapBlockAllocator<byte>.Instance.Allocate(64, out object? token);
        token.Should().BeNull();
        memory.Length.Should().Be(64);
        using MemoryHandle handle = memory.Pin();
        unsafe
        {
            ((byte*)handle.Pointer)[0] = 0xAB;
        }

        memory.Span[0].Should().Be(0xAB);
        PinnedHeapBlockAllocator<byte>.Instance.Free(memory, token);
    }

    [Fact]
    public unsafe void NativeAlignedAllocator_RespectsCacheLineAlignment()
    {
        var allocator = NativeAlignedBlockAllocator<float>.Instance;
        Memory<float> memory = allocator.Allocate(32, out object? token);
        try
        {
            memory.Length.Should().Be(32);
            using MemoryHandle handle = memory.Pin();
            ((nuint)handle.Pointer % 64).Should().Be(0);
            memory.Span.Fill(1.5f);
            memory.Span[31].Should().Be(1.5f);
        }
        finally
        {
            allocator.Free(memory, token);
        }
    }

    [Fact]
    public void ComputeByteSize_MatchesElementLayout()
    {
        ManagedArrayBlockAllocator<long>.Instance.ComputeByteSize(8).Should().Be(64);
        PinnedHeapBlockAllocator<long>.Instance.ComputeByteSize(8).Should().Be(64);
        NativeAlignedBlockAllocator<long>.Instance.ComputeByteSize(8).Should().Be(64);
    }

    [Fact]
    public void ComputeByteSize_Widens_To64Bit()
    {
        ManagedArrayBlockAllocator<long>.Instance.ComputeByteSize(int.MaxValue).Should().Be(17179869176L);
        NativeAlignedBlockAllocator<long>.Instance.ComputeByteSize(int.MaxValue).Should().Be(17179869176L);
    }
}
