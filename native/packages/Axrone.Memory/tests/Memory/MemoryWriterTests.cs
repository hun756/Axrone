using System.Buffers;
using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

/// <summary>
/// Tests for the expected API contract on MemoryWriter&lt;T&gt;.
/// These tests define the API that MemoryWriter&lt;T&gt; should satisfy.
/// They will not compile until the type is implemented (TDD red phase).
/// </summary>
[Collection("MemoryPool")]
public sealed class MemoryWriterTests
{
    [Fact]
    public void Constructor_InitializesWithZeroWritten()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);

        writer.WrittenCount.Should().Be(0);
        writer.RemainingCapacity.Should().Be(owner.Memory.Length);
    }

    [Fact]
    public void Advance_MovesCursor()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);
        int initialLength = owner.Memory.Length;

        writer.Advance(10);

        writer.WrittenCount.Should().Be(10);
        writer.RemainingCapacity.Should().Be(initialLength - 10);
    }

    [Fact]
    public void GetSpan_ReturnsCorrectSlice()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);
        int initialLength = owner.Memory.Length;

        writer.Advance(10);
        Span<int> span = writer.GetSpan();

        span.Length.Should().Be(initialLength - 10);
    }

    [Fact]
    public void GetMemory_ReturnsCorrectSlice()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);
        int initialLength = owner.Memory.Length;

        writer.Advance(10);
        Memory<int> memory = writer.GetMemory();

        memory.Length.Should().Be(initialLength - 10);
    }

    [Fact]
    public void WriteData_VisibleInOwner()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);

        Span<int> span = writer.GetSpan();
        span[0] = 42;
        span[1] = 99;
        span[2] = 7;
        writer.Advance(3);

        owner.Memory.Span[0].Should().Be(42);
        owner.Memory.Span[1].Should().Be(99);
        owner.Memory.Span[2].Should().Be(7);
    }

    [Fact]
    public void AdvanceBeyondCapacity_Throws()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);
        int capacity = owner.Memory.Length;

        var act = () => writer.Advance(capacity + 1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AdvanceNegative_Throws()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);

        var act = () => writer.Advance(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WrittenSpan_ReturnsOnlyWrittenPortion()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);

        Span<int> span = writer.GetSpan();
        span[0] = 10;
        span[1] = 20;
        span[2] = 30;
        span[3] = 40;
        span[4] = 50;
        writer.Advance(5);

        Span<int> written = writer.WrittenSpan;
        written.Length.Should().Be(5);
        written[0].Should().Be(10);
        written[1].Should().Be(20);
        written[2].Should().Be(30);
        written[3].Should().Be(40);
        written[4].Should().Be(50);
    }

    [Fact]
    public void WrittenMemory_ReturnsOnlyWrittenPortion()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);

        Span<int> span = writer.GetSpan();
        span[0] = 10;
        span[1] = 20;
        span[2] = 30;
        span[3] = 40;
        span[4] = 50;
        writer.Advance(5);

        Memory<int> written = writer.WrittenMemory;
        written.Length.Should().Be(5);
        written.Span[0].Should().Be(10);
        written.Span[1].Should().Be(20);
        written.Span[2].Should().Be(30);
        written.Span[3].Should().Be(40);
        written.Span[4].Should().Be(50);
    }

    [Fact]
    public void Dispose_ReturnsOwnerToPool()
    {
        using var pool = new MemoryPool<int>();
        var owner = pool.Rent(256);
        var writer = new MemoryWriter<int>(owner);

        Span<int> span = writer.GetSpan();
        span[0] = 42;
        writer.Advance(1);

        writer.Dispose();

        var metrics = pool.GetMetrics();
        metrics.PooledBuffers.Should().Be(1);
        metrics.ActiveBuffers.Should().Be(0);
    }

    [Fact]
    public void SizeHint_ReservesMinimumSpace()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);

        Span<int> span = writer.GetSpan(100);

        span.Length.Should().BeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public void SizeHint_ExceedsRemaining_Throws()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);
        int remaining = owner.Memory.Length;

        var act = () => { _ = writer.GetSpan(remaining + 100); };

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MultipleAdvanceAccumulate()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);

        writer.Advance(10);
        writer.Advance(20);

        writer.WrittenCount.Should().Be(30);
    }

    [Fact]
    public void ImplementsIBufferWriter()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(256);
        using var writer = new MemoryWriter<int>(owner);

        writer.Should().BeAssignableTo<IBufferWriter<int>>();
    }
}
