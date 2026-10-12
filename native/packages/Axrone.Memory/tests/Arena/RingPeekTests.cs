using Axrone.Memory.Arena;

namespace Axrone.Memory.Tests.Arena;

public class RingPeekTests : IDisposable
{
    private readonly ArenaMemoryRing<int, ProgressiveSpinBackoff> _ring;

    public RingPeekTests()
    {
        _ring = new ArenaMemoryRing<int, ProgressiveSpinBackoff>(new ArenaCapacity(16));
    }

    public void Dispose() => _ring.Dispose();

    [Fact]
    public void Peek_EmptyRing_IsEmpty()
    {
        RingPeekView<int> view = _ring.Batch.PeekAvailable();

        view.IsEmpty.Should().BeTrue();
        view.TotalCount.Should().Be(0);
    }

    [Fact]
    public void Peek_ReturnsData_WithoutConsuming()
    {
        _ring.Producer.TryWrite(11).Should().BeTrue();
        _ring.Producer.TryWrite(22).Should().BeTrue();

        RingPeekView<int> view = _ring.Batch.PeekAvailable();

        view.IsEmpty.Should().BeFalse();
        view.TotalCount.Should().Be(2);
        view.First.ToArray().Should().Equal(11, 22);
        view.Second.IsEmpty.Should().BeTrue();

        _ring.Consumer.TryRead(out int first).Should().BeTrue();
        first.Should().Be(11);
        _ring.Consumer.TryRead(out int second).Should().BeTrue();
        second.Should().Be(22);
    }

    [Fact]
    public void Peek_WrappedData_YieldsTwoRuns()
    {
        for (int i = 0; i < 14; i++)
        {
            _ring.Producer.TryWrite(i).Should().BeTrue();
        }
        for (int i = 0; i < 14; i++)
        {
            _ring.Consumer.TryRead(out _).Should().BeTrue();
        }
        for (int i = 100; i < 108; i++)
        {
            _ring.Producer.TryWrite(i).Should().BeTrue();
        }

        RingPeekView<int> view = _ring.Batch.PeekAvailable();

        view.TotalCount.Should().Be(8);
        var runs = new List<int[]>();
        foreach (ReadOnlySpan<int> run in view)
        {
            runs.Add(run.ToArray());
        }
        runs.SelectMany(run => run).Should().Equal(100, 101, 102, 103, 104, 105, 106, 107);
    }
}
