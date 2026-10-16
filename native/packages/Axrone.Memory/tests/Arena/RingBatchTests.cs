using Axrone.Memory.Arena;
using Axrone.Utility.Backoff.SpinPolicies;

namespace Axrone.Memory.Tests;

public sealed class RingBatchTests : IDisposable
{
    private readonly ArenaMemoryRing<int, AdaptiveSpinBackoff> _ring =
        new(new ArenaCapacity(16));

    public void Dispose()
    {
        _ring.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WriteReservation_Commits_VisibleToReaders()
    {
        _ring.Batch.TryReserveWrite(4, out BatchWriteReservation<int> reservation).Should().BeTrue();
        reservation.TotalReserved.Should().Be(4);
        reservation.IsCommitted.Should().BeFalse();

        reservation.FirstSegment.Length.Should().Be(4);
        for (int i = 0; i < 4; i++)
        {
            reservation.FirstSegment[i] = 100 + i;
        }

        reservation.Commit();
        reservation.IsCommitted.Should().BeTrue();

        for (int i = 0; i < 4; i++)
        {
            _ring.Consumer.TryRead(out int value).Should().BeTrue();
            value.Should().Be(100 + i);
        }
    }

    [Fact]
    public void WriteReservation_DoubleCommit_Throws()
    {
        _ring.Batch.TryReserveWrite(2, out BatchWriteReservation<int> reservation).Should().BeTrue();
        reservation.Commit();

        bool threw = false;
        try
        {
            reservation.Commit();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    [Fact]
    public void WriteReservation_Oversized_Throws()
    {
        Action reserve = () => _ring.Batch.TryReserveWrite(64, out _);
        reserve.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReadReservation_Exposes_CommittedRuns()
    {
        for (int i = 0; i < 6; i++)
        {
            _ring.Producer.TryWrite(i).Should().BeTrue();
        }

        _ring.Batch.TryReserveRead(6, out BatchReadReservation<int> reservation).Should().BeTrue();
        (reservation.FirstSegment.Length + reservation.SecondSegment.Length).Should().Be(6);
        reservation.Commit();
    }

    [Fact]
    public void ReadReservation_MoreThanAvailable_ReturnsFalse()
    {
        _ring.Producer.TryWrite(1).Should().BeTrue();
        _ring.Batch.TryReserveRead(4, out BatchReadReservation<int> _).Should().BeFalse();
    }

    [Fact]
    public void PeekAvailable_Views_WithoutConsuming()
    {
        _ring.Batch.PeekAvailable().IsEmpty.Should().BeTrue();

        _ring.Producer.TryWrite(11);
        _ring.Producer.TryWrite(22);

        RingPeekView<int> view = _ring.Batch.PeekAvailable();
        view.IsEmpty.Should().BeFalse();
        view.TotalCount.Should().Be(2);

        int seen = 0;
        foreach (ReadOnlySpan<int> run in view)
        {
            seen += run.Length;
        }

        seen.Should().Be(2);
        _ring.Consumer.TryRead(out _).Should().BeTrue();
    }
}
