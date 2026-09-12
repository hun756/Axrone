using Axrone.Collections;
using Axrone.Utility.Alignment;

namespace Axrone.Collections.Tests;

public class AlignedMemoryTests
{
    [Fact]
    public void AlignedMemoryBlock_Allocation_Succeeds()
    {
        using var block = new AlignedMemoryBlock<int>(new BatchCapacity(64), Alignment.CacheLine64);

        block.Alignment.Should().Be(64);
        block.Capacity.Value.Should().Be(64);
    }

    [Fact]
    public unsafe void AlignedMemoryBlock_PointerIsAligned()
    {
        using var block = new AlignedMemoryBlock<int>(new BatchCapacity(64), Alignment.CacheLine64);

        nuint ptr = (nuint)block.RawPointer;
        (ptr % 64).Should().Be(0);
    }

    [Fact]
    public unsafe void AlignedMemoryBlock_CacheLine128_Aligned()
    {
        using var block = new AlignedMemoryBlock<long>(new BatchCapacity(32), Alignment.CacheLine128);

        nuint ptr = (nuint)block.RawPointer;
        (ptr % 128).Should().Be(0);
    }

    [Fact]
    public void AlignedMemoryBlock_SpanIsAccessible()
    {
        using var block = new AlignedMemoryBlock<int>(new BatchCapacity(16), Alignment.CacheLine64);

        Span<int> span = block.Span;
        span.Length.Should().Be(16);

        // Memory should be zero-initialized
        for (int i = 0; i < span.Length; i++)
            span[i].Should().Be(0);
    }

    [Fact]
    public void AlignedMemoryBlock_SpanReadWrite_WorksCorrectly()
    {
        using var block = new AlignedMemoryBlock<int>(new BatchCapacity(16), Alignment.CacheLine64);

        Span<int> span = block.Span;
        span[0] = 42;
        span[15] = 99;

        ReadOnlySpan<int> roSpan = block.ReadOnlySpan;
        roSpan[0].Should().Be(42);
        roSpan[15].Should().Be(99);
    }

    [Fact]
    public void AlignedMemoryBlock_Dispose_PreventsAccess()
    {
        var block = new AlignedMemoryBlock<int>(new BatchCapacity(16), Alignment.CacheLine64);
        block.Dispose();

        var act = () => { _ = block.Span; };
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AlignedMemoryBlock_Dispose_IsIdempotent()
    {
        var block = new AlignedMemoryBlock<int>(new BatchCapacity(16), Alignment.CacheLine64);
        block.Dispose();
        block.Dispose();
    }
}
