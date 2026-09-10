using Axrone.Collections;

namespace Axrone.Collections.Tests;

public class AlignedMemoryTests
{
    [Fact]
    public void AlignedMemoryBlock_Allocation_Succeeds()
    {
        using var block = new AlignedMemoryBlock<int>(new BatchCapacity(64), MemoryAlignment.CacheLine64);

        block.Alignment.Should().Be(64);
        block.Capacity.Value.Should().Be(64);
    }

    [Fact]
    public unsafe void AlignedMemoryBlock_PointerIsAligned()
    {
        using var block = new AlignedMemoryBlock<int>(new BatchCapacity(64), MemoryAlignment.CacheLine64);

        nuint ptr = (nuint)block.RawPointer;
        (ptr % 64).Should().Be(0);
    }

    [Fact]
    public unsafe void AlignedMemoryBlock_CacheLine128_Aligned()
    {
        using var block = new AlignedMemoryBlock<long>(new BatchCapacity(32), MemoryAlignment.CacheLine128);

        nuint ptr = (nuint)block.RawPointer;
        (ptr % 128).Should().Be(0);
    }

    [Fact]
    public void AlignedMemoryBlock_SpanIsAccessible()
    {
        using var block = new AlignedMemoryBlock<int>(new BatchCapacity(16), MemoryAlignment.CacheLine64);

        Span<int> span = block.Span;
        span.Length.Should().Be(16);

        // Memory should be zero-initialized
        for (int i = 0; i < span.Length; i++)
            span[i].Should().Be(0);
    }

    [Fact]
    public void AlignedMemoryBlock_SpanReadWrite_WorksCorrectly()
    {
        using var block = new AlignedMemoryBlock<int>(new BatchCapacity(16), MemoryAlignment.CacheLine64);

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
        var block = new AlignedMemoryBlock<int>(new BatchCapacity(16), MemoryAlignment.CacheLine64);
        block.Dispose();

        var act = () => { _ = block.Span; };
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AlignedMemoryBlock_Dispose_IsIdempotent()
    {
        var block = new AlignedMemoryBlock<int>(new BatchCapacity(16), MemoryAlignment.CacheLine64);
        block.Dispose();
        block.Dispose();
    }

    // --- AlignedCounter tests ---

    [Fact]
    public void AlignedCounter_DefaultInitialValue_IsZero()
    {
        using var counter = new AlignedCounter();
        counter.Value.Should().Be(0);
    }

    [Fact]
    public void AlignedCounter_CustomInitialValue()
    {
        using var counter = new AlignedCounter(100);
        counter.Value.Should().Be(100);
    }

    [Fact]
    public void AlignedCounter_Increment_ReturnsNewValue()
    {
        using var counter = new AlignedCounter();

        counter.Increment().Should().Be(1);
        counter.Increment().Should().Be(2);
        counter.Increment().Should().Be(3);
        counter.Value.Should().Be(3);
    }

    [Fact]
    public void AlignedCounter_Add_ReturnsNewValue()
    {
        using var counter = new AlignedCounter(10);

        counter.Add(5).Should().Be(15);
        counter.Add(-3).Should().Be(12);
        counter.Value.Should().Be(12);
    }

    [Fact]
    public void AlignedCounter_CompareExchange_SucceedsWhenExpected()
    {
        using var counter = new AlignedCounter(10);

        counter.CompareExchange(10, 20).Should().BeTrue();
        counter.Value.Should().Be(20);
    }

    [Fact]
    public void AlignedCounter_CompareExchange_FailsWhenUnexpected()
    {
        using var counter = new AlignedCounter(10);

        counter.CompareExchange(99, 20).Should().BeFalse();
        counter.Value.Should().Be(10);
    }

    [Fact]
    public void AlignedCounter_Dispose_IsIdempotent()
    {
        var counter = new AlignedCounter();
        counter.Dispose();
        counter.Dispose();
    }
}
