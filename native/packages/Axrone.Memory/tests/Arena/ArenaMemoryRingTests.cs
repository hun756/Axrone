using Axrone.Memory.Arena;
using Axrone.Utility.Alignment;

namespace Axrone.Memory.Tests.Arena;

public class ArenaMemoryRingTests : IDisposable
{
    private readonly ArenaMemoryRing<int, ProgressiveSpinBackoff> _ring;

    public ArenaMemoryRingTests()
    {
        _ring = new ArenaMemoryRing<int, ProgressiveSpinBackoff>(new ArenaCapacity(16));
    }

    [Fact]
    public void Construction_Sets_Capacity()
    {
        _ring.Capacity.Should().Be(16u);
    }

    [Fact]
    public void Producer_Write_And_Consumer_Read()
    {
        _ring.Producer.TryWrite(42).Should().BeTrue();
        _ring.Consumer.TryRead(out int result).Should().BeTrue();
        result.Should().Be(42);
    }

    [Fact]
    public void Producer_Write_Until_Full_Returns_False()
    {
        for (int i = 0; i < 16; i++)
        {
            _ring.Producer.TryWrite(i).Should().BeTrue();
        }
        _ring.Producer.TryWrite(999).Should().BeFalse();
    }

    [Fact]
    public void Consumer_Read_Empty_Returns_False()
    {
        _ring.Consumer.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void WriteSpinning_Writes_Item()
    {
        _ring.Producer.WriteSpinning(7);
        _ring.Consumer.TryRead(out int result).Should().BeTrue();
        result.Should().Be(7);
    }

    [Fact]
    public async Task WriteAsync_Writes_Item()
    {
        await _ring.Producer.WriteAsync(13);
        _ring.Consumer.TryRead(out int result).Should().BeTrue();
        result.Should().Be(13);
    }

    [Fact]
    public void TryWriteMany_Writes_Batch()
    {
        Span<int> items = stackalloc int[4] { 1, 2, 3, 4 };
        _ring.Producer.TryWriteMany(items).Should().BeTrue();

        for (int i = 1; i <= 4; i++)
        {
            _ring.Consumer.TryRead(out int result).Should().BeTrue();
            result.Should().Be(i);
        }
    }

    [Fact]
    public void TryReadSpinning_Reads_Item()
    {
        _ring.Producer.TryWrite(55).Should().BeTrue();
        _ring.Consumer.TryReadSpinning(out int result).Should().BeTrue();
        result.Should().Be(55);
    }

    [Fact]
    public void Admin_GetSnapshot_Returns_Valid_Data()
    {
        _ring.Producer.TryWrite(1).Should().BeTrue();

        var snapshot = _ring.Admin.GetSnapshot();
        snapshot.Capacity.Should().Be(16u);
        snapshot.CommittedHead.Should().Be(1UL);
        snapshot.CommittedTail.Should().Be(0UL);
    }

    [Fact]
    public void Admin_ExamineHealth_Reports_Active()
    {
        var health = _ring.Admin.ExamineHealth();
        health.IsHealthy.Should().BeTrue();
        health.StatusMessage.Should().Be("Active");
    }

    [Fact]
    public void Admin_Complete_Marks_Completed()
    {
        _ring.Admin.Complete();
        var snapshot = _ring.Admin.GetSnapshot();
        snapshot.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Streamer_WaitToRead_Returns_True_When_Data_Available()
    {
        _ring.Producer.TryWrite(1).Should().BeTrue();
        var result = await _ring.Streamer.WaitToReadAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Streamer_WaitToWrite_Returns_True_When_Space_Available()
    {
        var result = await _ring.Streamer.WaitToWriteAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Streamer_ReadAllAsync_Reads_Until_Completed()
    {
        _ring.Producer.TryWrite(10).Should().BeTrue();
        _ring.Producer.TryWrite(20).Should().BeTrue();
        _ring.Admin.Complete();

        var items = new List<int>();
        await foreach (var item in _ring.Streamer.ReadAllAsync())
        {
            items.Add(item);
        }

        items.Should().BeEquivalentTo([10, 20]);
    }

    [Fact]
    public void Batch_ReserveWrite_And_Commit()
    {
        var batch = (IBatchReservable<int>)_ring.Batch;
        batch.TryReserveWrite(4, out var reservation).Should().BeTrue();

        reservation.FirstSegment[0] = 1;
        reservation.FirstSegment[1] = 2;
        reservation.FirstSegment[2] = 3;
        reservation.FirstSegment[3] = 4;
        reservation.Commit();
        reservation.Dispose();

        for (int i = 1; i <= 4; i++)
        {
            _ring.Consumer.TryRead(out int result).Should().BeTrue();
            result.Should().Be(i);
        }
    }

    [Fact]
    public void Batch_ReserveWrite_Abandon_On_Dispose_Without_Commit()
    {
        var batch = (IBatchReservable<int>)_ring.Batch;
        batch.TryReserveWrite(4, out var reservation).Should().BeTrue();
        reservation.Dispose();

        _ring.Consumer.TryRead(out int result).Should().BeTrue();
        result.Should().Be(0);
    }

    [Fact]
    public void Batch_Double_Commit_Throws()
    {
        var batch = (IBatchReservable<int>)_ring.Batch;
        batch.TryReserveWrite(2, out var reservation).Should().BeTrue();
        reservation.Commit();

        bool threw = false;
        try { reservation.Commit(); }
        catch (InvalidOperationException) { threw = true; }
        threw.Should().BeTrue();
        reservation.Dispose();
    }

    [Fact]
    public void Dispose_Sets_Flag()
    {
        _ring.IsDisposed.Should().BeFalse();
        _ring.Dispose();
        _ring.IsDisposed.Should().BeTrue();
    }

    public void Dispose()
    {
        _ring.Dispose();
    }
}
