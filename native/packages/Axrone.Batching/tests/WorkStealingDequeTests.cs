namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the work-stealing deque: ordering, bounds, last-item race, concurrent steal.
/// </summary>
public class WorkStealingDequeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Constructor_RejectsNonPowerOfTwo(int capacity)
    {
        var act = () => new WorkStealingDeque<int>(capacity);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PushPop_IsLifo()
    {
        var deque = new WorkStealingDeque<int>(8);
        deque.TryPush(1).Should().BeTrue();
        deque.TryPush(2).Should().BeTrue();
        deque.TryPush(3).Should().BeTrue();

        deque.TryPopBottom(out var third).Should().BeTrue();
        deque.TryPopBottom(out var second).Should().BeTrue();
        deque.TryPopBottom(out var first).Should().BeTrue();

        third.Should().Be(3);
        second.Should().Be(2);
        first.Should().Be(1);
        deque.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Steal_IsFifo()
    {
        var deque = new WorkStealingDeque<int>(8);
        deque.TryPush(1);
        deque.TryPush(2);
        deque.TryPush(3);

        deque.TrySteal(out var first).Should().BeTrue();
        deque.TrySteal(out var second).Should().BeTrue();

        first.Should().Be(1);
        second.Should().Be(2);
        deque.Count.Should().Be(1);
    }

    [Fact]
    public void Push_Full_ReturnsFalse()
    {
        var deque = new WorkStealingDeque<int>(2);
        deque.TryPush(1).Should().BeTrue();
        deque.TryPush(2).Should().BeTrue();

        deque.TryPush(3).Should().BeFalse();
        deque.DroppedItems.Should().Be(0);
    }

    [Fact]
    public void Push_RejectPolicy_NeverDrops()
    {
        var deque = new WorkStealingDeque<int>(2);
        deque.TryPush(1);
        deque.TryPush(2);

        deque.TryPush<RejectNewEvictionPolicy>(3).Should().BeFalse();
        deque.Count.Should().Be(2);
    }

    [Fact]
    public void Push_DropOldestPolicy_EvictsOldest()
    {
        var deque = new WorkStealingDeque<int>(4);
        deque.TryPush(1);
        deque.TryPush(2);
        deque.TryPush(3);
        deque.TryPush(4);

        deque.TryPush<DropOldestEvictionPolicy>(5).Should().BeTrue();
        deque.DroppedItems.Should().Be(1);
        deque.Count.Should().Be(4);

        deque.TrySteal(out var oldest).Should().BeTrue();
        oldest.Should().Be(2);
    }

    [Fact]
    public void PopBottom_Empty_ReturnsFalse()
    {
        var deque = new WorkStealingDeque<int>(4);

        deque.TryPopBottom(out _).Should().BeFalse();
        deque.TrySteal(out _).Should().BeFalse();
    }

    [Fact]
    public void LastItem_PopAndSteal_HandOutExactlyOnce()
    {
        // One item, both sides race: exactly one side wins, never both, never none-with-item.
        for (var round = 0; round < 200; round++)
        {
            var deque = new WorkStealingDeque<int>(4);
            deque.TryPush(42);

            var popped = deque.TryPopBottom(out var poppedValue);
            var stolen = deque.TrySteal(out var stolenValue);

            (popped ^ stolen).Should().BeTrue($"round {round}");
            var winner = popped ? poppedValue : stolenValue;
            winner.Should().Be(42);
            deque.IsEmpty.Should().BeTrue();
        }
    }

    [Fact]
    public void ConcurrentSteal_EveryItemRetrievedExactlyOnce()
    {
        const int items = 1024;
        var deque = new WorkStealingDeque<int>(2048);
        for (var i = 0; i < items; i++)
        {
            deque.TryPush(i).Should().BeTrue();
        }

        var seen = new int[items];
        var threads = new Thread[Environment.ProcessorCount];
        for (var t = 0; t < threads.Length; t++)
        {
            threads[t] = new Thread(() =>
            {
                while (deque.TrySteal(out var value))
                {
                    Interlocked.Increment(ref seen[value]);
                }
            });
            threads[t].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        seen.Should().OnlyContain(count => count == 1);
        deque.IsEmpty.Should().BeTrue();
    }
}
