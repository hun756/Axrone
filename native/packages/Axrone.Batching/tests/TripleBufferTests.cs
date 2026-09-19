namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the triple buffer: handoff, bounds, staleness.
/// </summary>
public class TripleBufferTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        var act = () => new TripleBuffer<int>(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Acquire_BeforePublish_IsEmpty()
    {
        var buffer = new TripleBuffer<int>(4);

        var snapshot = buffer.Acquire();

        snapshot.Items.Length.Should().Be(0);
    }

    [Fact]
    public void WriteSwapAcquire_PublishesItems()
    {
        var buffer = new TripleBuffer<int>(4);
        buffer.TryWrite(10).Should().BeTrue();
        buffer.TryWrite(20).Should().BeTrue();
        buffer.SwapProducer();

        var snapshot = buffer.Acquire();

        snapshot.Items.ToArray().Should().Equal(10, 20);
        buffer.IsStillValid(in snapshot).Should().BeTrue();
    }

    [Fact]
    public void TryWrite_FullSlot_ReturnsFalse()
    {
        var buffer = new TripleBuffer<int>(2);
        buffer.TryWrite(1).Should().BeTrue();
        buffer.TryWrite(2).Should().BeTrue();

        buffer.TryWrite(3).Should().BeFalse();
    }

    [Fact]
    public void WriteRange_TruncatesAtCapacity()
    {
        var buffer = new TripleBuffer<int>(3);

        var accepted = buffer.WriteRange(new int[] { 1, 2, 3, 4, 5 });

        accepted.Should().Be(3);
        buffer.SwapProducer();
        buffer.Acquire().Items.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void SecondAcquire_SameSnapshot_StaysValid()
    {
        var buffer = new TripleBuffer<int>(4);
        buffer.TryWrite(7);
        buffer.SwapProducer();

        var first = buffer.Acquire();
        var second = buffer.Acquire();

        second.Items.ToArray().Should().Equal(7);
        buffer.IsStillValid(in first).Should().BeTrue();
    }

    [Fact]
    public void Acquire_AfterNewPublish_InvalidatesOldSnapshot()
    {
        var buffer = new TripleBuffer<int>(4);
        buffer.TryWrite(1);
        buffer.SwapProducer();
        var old = buffer.Acquire();

        buffer.TryWrite(2);
        buffer.SwapProducer();
        var fresh = buffer.Acquire();

        fresh.Items.ToArray().Should().Equal(2);
        buffer.IsStillValid(in old).Should().BeFalse();
        buffer.IsStillValid(in fresh).Should().BeTrue();
    }

    [Fact]
    public void SwapProducer_ClearsNextWriteSlot()
    {
        var buffer = new TripleBuffer<int>(2);
        buffer.TryWrite(1);
        buffer.TryWrite(2);
        buffer.SwapProducer();

        // Producer slot is fresh; it accepts a full capacity again.
        buffer.WriteRange(new int[] { 3, 4 }).Should().Be(2);
        buffer.SwapProducer();
        buffer.Acquire().Items.ToArray().Should().Equal(3, 4);
    }
}
