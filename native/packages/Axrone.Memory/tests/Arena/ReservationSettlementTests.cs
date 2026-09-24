using Axrone.Memory.Arena;

namespace Axrone.Memory.Tests.Arena;

/// <summary>
/// Settlement seal: the cursor position is the lease identity, so a copied or stale handle
/// settling an already-settled range throws instead of hanging the commit spin.
/// </summary>
public class ReservationSettlementTests : IDisposable
{
    private readonly ArenaMemoryRing<int, ProgressiveSpinBackoff> _ring;

    public ReservationSettlementTests()
    {
        _ring = new ArenaMemoryRing<int, ProgressiveSpinBackoff>(new ArenaCapacity(16));
    }

    public void Dispose() => _ring.Dispose();

    [Fact]
    public void CopiedWriteHandle_SecondCommit_Throws()
    {
        var batch = (IBatchReservable<int>)_ring.Batch;
        batch.TryReserveWrite(2, out var reservation).Should().BeTrue();
        var copy = reservation;
        reservation.Commit();

        // Must throw, never spin: without the seal this stalls the commit loop forever.
        bool threw = false;
        try
        {
            copy.Commit();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
        reservation.Dispose();
    }

    [Fact]
    public void CopiedReadHandle_SecondCommit_Throws()
    {
        var batch = (IBatchReservable<int>)_ring.Batch;
        batch.TryReserveWrite(2, out var write).Should().BeTrue();
        write.Commit();
        write.Dispose();

        batch.TryReserveRead(2, out var reservation).Should().BeTrue();
        var copy = reservation;
        reservation.Commit();

        bool threw = false;
        try
        {
            copy.Commit();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
        reservation.Dispose();
    }

    [Fact]
    public void AbandonAfterCommit_Throws()
    {
        var batch = (IBatchReservable<int>)_ring.Batch;
        batch.TryReserveWrite(2, out var reservation).Should().BeTrue();
        var copy = reservation;
        reservation.Commit();

        bool threw = false;
        try
        {
            copy.Dispose();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
        reservation.Dispose();
    }

    [Fact]
    public void NormalWriteRead_StillFlows()
    {
        var batch = (IBatchReservable<int>)_ring.Batch;
        batch.TryReserveWrite(2, out var reservation).Should().BeTrue();
        reservation.FirstSegment[0] = 7;
        reservation.FirstSegment[1] = 8;
        reservation.Commit();
        reservation.Dispose();

        _ring.Consumer.TryRead(out int first).Should().BeTrue();
        first.Should().Be(7);
        _ring.Consumer.TryRead(out int second).Should().BeTrue();
        second.Should().Be(8);
    }
}
