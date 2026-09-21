using Axrone.Memory.Arena;
using Axrone.Utility.Alignment;

namespace Axrone.Memory.Tests.Arena;

public class LinearArenaTests : IDisposable
{
    private readonly LinearArena<byte> _arena;

    public LinearArenaTests()
    {
        _arena = new LinearArena<byte>(256);
    }

    [Fact]
    public void Construction_Sets_Capacity()
    {
        _arena.CapacityBytes.Should().Be(256u);
        _arena.UsedBytes.Should().Be(0u);
    }

    [Fact]
    public void Allocate_Returns_Span_Of_Requested_Size()
    {
        var span = _arena.Allocate(32);
        span.Length.Should().Be(32);
        _arena.UsedBytes.Should().Be(32u);
    }

    [Fact]
    public void Allocate_Multiple_Times_Advances_Position()
    {
        _arena.Allocate(10);
        _arena.Allocate(20);
        _arena.UsedBytes.Should().Be(30u);
    }

    [Fact]
    public void Allocate_Exhausted_Throws()
    {
        _arena.Allocate(256);
        Action act = () => _arena.Allocate(1);
        act.Should().Throw<InsufficientMemoryException>();
    }

    [Fact]
    public void AllocateZeroed_Returns_Zeroed_Span()
    {
        var span = _arena.AllocateZeroed(16);
        span.Length.Should().Be(16);
        foreach (var b in span)
        {
            b.Should().Be(0);
        }
    }

    [Fact]
    public void RewindTo_Restores_Position()
    {
        _arena.Allocate(32);
        var marker = _arena.CurrentMarker;
        _arena.Allocate(64);
        _arena.UsedBytes.Should().Be(96u);

        _arena.RewindTo(marker);
        _arena.UsedBytes.Should().Be(32u);
    }

    [Fact]
    public void Reset_Clears_Position()
    {
        _arena.Allocate(100);
        _arena.Reset();
        _arena.UsedBytes.Should().Be(0u);
    }

    [Fact]
    public void Scope_Dispose_Rewinds_If_Not_Committed()
    {
        _arena.Allocate(16);
        var usedBefore = _arena.UsedBytes;

        using (var scope = _arena.BeginScope())
        {
            scope.Allocate(64);
        }

        _arena.UsedBytes.Should().Be(usedBefore);
    }

    [Fact]
    public void Scope_Commit_Preserves_Allocations()
    {
        _arena.Allocate(16);
        using (var scope = _arena.BeginScope())
        {
            scope.Allocate(64);
            scope.Commit();
        }

        _arena.UsedBytes.Should().Be(80u);
    }

    [Fact]
    public void Dispose_Prevents_Further_Allocation()
    {
        _arena.Dispose();
        Action act = () => _arena.Allocate(1);
        act.Should().Throw<ObjectDisposedException>();
    }

    public void Dispose()
    {
        _arena.Dispose();
    }
}
