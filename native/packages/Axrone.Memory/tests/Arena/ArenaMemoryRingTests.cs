using Axrone.Memory.Arena;
using Axrone.Utility.Alignment;

namespace Axrone.Memory.Tests.Arena;

public sealed class ArenaMemoryRingTests : IDisposable
{
    private readonly NativeAlignedArenaStorageBlock<int> _storage;
    private readonly ArenaMemoryRing<int, DefaultNoOpBackoff> _ring;

    public ArenaMemoryRingTests()
    {
        _storage = new NativeAlignedArenaStorageBlock<int>(64, Alignment.CacheLine64);
        _ring = new ArenaMemoryRing<int, DefaultNoOpBackoff>(_storage);
    }

    public void Dispose()
    {
        _ring.Dispose();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_SetsCapacity()
    {
        _ring.Capacity.Should().Be(64);
    }

    [Fact]
    public void NewRing_IsEmpty()
    {
        _ring.IsEmpty.Should().BeTrue();
        _ring.Count.Should().Be(0);
    }

    [Fact]
    public void TryWrite_SingleItem_Succeeds()
    {
        Span<int> data = stackalloc int[] { 42 };
        _ring.TryWrite(data).Should().BeTrue();
        _ring.Count.Should().Be(1);
    }

    [Fact]
    public void TryRead_SingleItem_Succeeds()
    {
        Span<int> writeData = stackalloc int[] { 42 };
        _ring.TryWrite(writeData).Should().BeTrue();

        Span<int> readBuf = stackalloc int[1];
        _ring.TryRead(readBuf).Should().BeTrue();
        readBuf[0].Should().Be(42);
    }

    [Fact]
    public void TryWrite_WhenFull_ReturnsFalse()
    {
        Span<int> data = stackalloc int[64];
        for (int i = 0; i < 64; i++) data[i] = i;

        _ring.TryWrite(data).Should().BeTrue();
        _ring.IsFull.Should().BeTrue();

        Span<int> overflow = stackalloc int[] { 999 };
        _ring.TryWrite(overflow).Should().BeFalse();
    }

    [Fact]
    public void TryRead_WhenEmpty_ReturnsFalse()
    {
        Span<int> buf = stackalloc int[1];
        _ring.TryRead(buf).Should().BeFalse();
    }

    [Fact]
    public void WriteThenRead_PreservesOrder()
    {
        Span<int> data = stackalloc int[1];
        for (int i = 0; i < 32; i++)
        {
            data[0] = i;
            _ring.TryWrite(data).Should().BeTrue();
        }

        Span<int> buf = stackalloc int[1];
        for (int i = 0; i < 32; i++)
        {
            _ring.TryRead(buf).Should().BeTrue();
            buf[0].Should().Be(i);
        }

        _ring.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ReserveWriteBatch_ReturnsCorrectSpan()
    {
        int reserved = _ring.ReserveWriteBatch(4, out Span<int> span);
        reserved.Should().Be(4);
        span.Length.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void CommitWrite_AdvancesSequence()
    {
        long before = _ring.CommittedWriteSequence;
        _ring.ReserveWriteBatch(2, out _);
        _ring.CommitWrite(2);
        _ring.CommittedWriteSequence.Should().Be(before + 2);
    }

    [Fact]
    public void ReserveReadBatch_ReturnsCorrectSpan()
    {
        Span<int> writeData = stackalloc int[] { 1, 2, 3 };
        _ring.TryWrite(writeData);

        int reserved = _ring.ReserveReadBatch(3, out Span<int> span);
        reserved.Should().Be(3);
        span.Length.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Dispose_PreventsFurtherOperations()
    {
        _ring.Dispose();
        _ring.IsDisposed.Should().BeTrue();

        Action act = () => _ring.ReserveWriteBatch(1, out _);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetHealthReport_ReturnsValidReport()
    {
        var report = _ring.GetHealthReport();
        report.IsHealthy.Should().BeTrue();
        report.IsDisposed.Should().BeFalse();
        report.Capacity.Should().Be(64);
    }

    [Fact]
    public void GetMetricsSnapshot_ReturnsValidSnapshot()
    {
        var snapshot = _ring.GetMetricsSnapshot();
        snapshot.TotalWrites.Should().Be(0);
        snapshot.TotalReads.Should().Be(0);
    }

    [Fact]
    public void Concurrent_ProducerConsumer_PreservesAllItems()
    {
        const int itemCount = 10_000;
        var consumed = new ConcurrentQueue<int>();
        var cts = new CancellationTokenSource();

        var producer = Task.Run(() =>
        {
            Span<int> data = stackalloc int[1];
            for (int i = 0; i < itemCount; i++)
            {
                data[0] = i;
                while (!_ring.TryWrite(data))
                {
                    Thread.SpinWait(1);
                }
            }
        });

        var consumer = Task.Run(() =>
        {
            Span<int> buf = stackalloc int[1];
            int received = 0;
            while (received < itemCount)
            {
                if (_ring.TryRead(buf))
                {
                    consumed.Enqueue(buf[0]);
                    received++;
                }
                else
                {
                    Thread.SpinWait(1);
                }
            }
        });

        Task.WaitAll(producer, consumer);

        consumed.Count.Should().Be(itemCount);
    }
}
