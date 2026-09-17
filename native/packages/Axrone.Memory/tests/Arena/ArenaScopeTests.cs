using Axrone.Memory.Arena;
using Axrone.Utility.Alignment;

namespace Axrone.Memory.Tests.Arena;

public sealed class ArenaScopeTests : IDisposable
{
    private readonly NativeAlignedArenaStorageBlock<int> _storage;
    private readonly ArenaMemoryRing<int, DefaultNoOpBackoff> _ring;

    public ArenaScopeTests()
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
    public void Scope_CanWrite()
    {
        using var scope = new ArenaScope<int, DefaultNoOpBackoff>(_ring);
        Span<int> data = stackalloc int[] { 1, 2, 3 };
        scope.TryWrite(data).Should().BeTrue();
    }

    [Fact]
    public void Scope_CanRead()
    {
        using var scope = new ArenaScope<int, DefaultNoOpBackoff>(_ring);
        Span<int> writeData = stackalloc int[] { 42 };
        scope.TryWrite(writeData);

        Span<int> buf = stackalloc int[1];
        scope.TryRead(buf).Should().BeTrue();
        buf[0].Should().Be(42);
    }

    [Fact]
    public void Scope_TakeCheckpoint_DoesNotThrow()
    {
        using var scope = new ArenaScope<int, DefaultNoOpBackoff>(_ring);
        scope.TakeCheckpoint();
    }

    [Fact]
    public void Scope_Rewind_DoesNotThrow()
    {
        using var scope = new ArenaScope<int, DefaultNoOpBackoff>(_ring);
        scope.Rewind();
    }

    [Fact]
    public void Scope_Dispose_PreventsOperations()
    {
        var scope = new ArenaScope<int, DefaultNoOpBackoff>(_ring);
        scope.Dispose();

        Action act = () => scope.TakeCheckpoint();
        act.Should().Throw<ObjectDisposedException>();
    }
}

public sealed class BatchReservationTests : IDisposable
{
    private readonly NativeAlignedArenaStorageBlock<int> _storage;
    private readonly ArenaMemoryRing<int, DefaultNoOpBackoff> _ring;

    public BatchReservationTests()
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
    public void ReserveWriteBatch_ReturnsRequestedCount()
    {
        int reserved = _ring.ReserveWriteBatch(8, out Span<int> span);
        reserved.Should().Be(8);
        span.Length.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void CommitWrite_AdvancesSequence()
    {
        _ring.ReserveWriteBatch(4, out _);
        _ring.CommitWrite(4);
        _ring.CommittedWriteSequence.Should().Be(4);
    }

    [Fact]
    public void ReserveReadBatch_AfterWrite_ReturnsData()
    {
        Span<int> writeData = stackalloc int[] { 10, 20, 30, 40 };
        _ring.TryWrite(writeData);

        int reserved = _ring.ReserveReadBatch(4, out Span<int> span);
        reserved.Should().Be(4);
        span[0].Should().Be(10);
        span[3].Should().Be(40);
    }

    [Fact]
    public void CommitRead_AdvancesSequence()
    {
        Span<int> writeData = stackalloc int[] { 1, 2, 3 };
        _ring.TryWrite(writeData);

        _ring.ReserveReadBatch(3, out _);
        _ring.CommitRead(3);
        _ring.CommittedReadSequence.Should().Be(3);
    }
}
