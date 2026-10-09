using System;
using Xunit;
using FluentAssertions;
using Axrone.Patterns;

namespace Axrone.Patterns.Tests;

public sealed class NativeArenaTests : IDisposable
{
    private NativeNodeArena<int, WritablePhase>? _arena;

    public void Dispose() => _arena?.Dispose();

    private NativeNodeArena<int, WritablePhase> NewArena(int capacity = 8)
    {
        _arena?.Dispose();
        _arena = new NativeNodeArenaBuilder<int>().WithCapacity(capacity).Build();
        return _arena;
    }

    [Fact]
    public void Builder_RejectsNonPositiveCapacity()
    {
        Action zero = () => new NativeNodeArenaBuilder<int>().WithCapacity(0);
        Action negative = () => new NativeNodeArenaBuilder<int>().WithCapacity(-4);

        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Allocate_ReturnsSequentialHandles()
    {
        var arena = NewArena();

        NodeHandle a = arena.Allocate(10);
        NodeHandle b = arena.Allocate(20);
        NodeHandle c = arena.Allocate(30);

        a.Should().Be(new NodeHandle(0));
        b.Should().Be(new NodeHandle(1));
        c.Should().Be(new NodeHandle(2));
        arena.Count.Should().Be(3);
        arena.Get(a).Should().Be(10);
        arena.Get(c).Should().Be(30);
    }

    [Fact]
    public void Allocate_GrowsGeometrically_KeepingData()
    {
        var arena = NewArena(capacity: 2);

        for (int i = 0; i < 10; i++)
        {
            arena.Allocate(i * 3);
        }

        arena.Count.Should().Be(10);
        arena.Capacity.Should().BeGreaterThanOrEqualTo(10);
        for (int i = 0; i < 10; i++)
        {
            arena.Get(new NodeHandle(i)).Should().Be(i * 3);
        }
    }

    [Fact]
    public void AllocateBatch_ReturnsContiguousRange()
    {
        var arena = NewArena();
        int[] nodes = [5, 6, 7, 8];

        NodeRange range = arena.AllocateBatch(nodes);

        range.Should().Be(new NodeRange(0, 4));
        arena.AsSpan(range).ToArray().Should().Equal(5, 6, 7, 8);
        arena.AllocateBatch(ReadOnlySpan<int>.Empty).Should().Be(NodeRange.Empty);
    }

    [Fact]
    public void GetMutable_WritesThrough()
    {
        var arena = NewArena();
        NodeHandle handle = arena.Allocate(1);

        arena.GetMutable(handle) = 42;

        arena.Get(handle).Should().Be(42);
    }

    [Fact]
    public void AsMutableSpan_WritesThrough()
    {
        var arena = NewArena();
        arena.AllocateBatch([1, 2, 3]);

        arena.AsMutableSpan(new NodeRange(0, 3))[1] = 99;

        arena.Get(new NodeHandle(1)).Should().Be(99);
    }

    [Fact]
    public void Get_UnknownHandle_Throws()
    {
        var arena = NewArena();
        arena.Allocate(1);

        Action badHandle = () => arena.Get(new NodeHandle(7));
        Action badRange = () => arena.AsSpan(new NodeRange(0, 5));

        badHandle.Should().Throw<IndexOutOfRangeException>();
        badRange.Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void Seal_FreezesReads_AndSpendsWriter()
    {
        var arena = NewArena();
        arena.AllocateBatch([4, 5, 6]);

        var sealedArena = arena.Seal();

        try
        {
            sealedArena.Count.Should().Be(3);
            sealedArena.Get(new NodeHandle(2)).Should().Be(6);
            sealedArena.AsSpan(new NodeRange(0, 3)).ToArray().Should().Equal(4, 5, 6);

            Action afterSeal = () => arena.Allocate(9);
            afterSeal.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            sealedArena.Dispose();
        }
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var arena = NewArena();
        arena.Allocate(1);

        arena.Dispose();
        Action again = () => arena.Dispose();
        Action read = () => arena.Get(new NodeHandle(0));

        again.Should().NotThrow();
        read.Should().Throw<ObjectDisposedException>();
        _arena = null;
    }
}

public sealed class PinnedArenaTests : IDisposable
{
    private PinnedNodeArena<int, WritablePhase>? _arena;

    public void Dispose() => _arena?.Dispose();

    private PinnedNodeArena<int, WritablePhase> NewArena(int capacity = 8)
    {
        _arena?.Dispose();
        _arena = new PinnedNodeArenaBuilder<int>().WithCapacity(capacity).Build();
        return _arena;
    }

    [Fact]
    public void Builder_RejectsNonPositiveCapacity()
    {
        Action zero = () => new PinnedNodeArenaBuilder<int>().WithCapacity(0);

        zero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Allocate_Get_Seal_RoundTrip()
    {
        var arena = NewArena();

        NodeHandle a = arena.Allocate(100);
        arena.GetMutable(a) = 101;

        var sealedArena = arena.Seal();
        try
        {
            sealedArena.Count.Should().Be(1);
            sealedArena.Get(a).Should().Be(101);
        }
        finally
        {
            sealedArena.Dispose();
        }
    }

    [Fact]
    public void Allocate_Grows_KeepingData()
    {
        var arena = NewArena(capacity: 2);

        for (int i = 0; i < 9; i++)
        {
            arena.Allocate(i);
        }

        arena.Count.Should().Be(9);
        arena.Capacity.Should().BeGreaterThanOrEqualTo(9);
        arena.Get(new NodeHandle(8)).Should().Be(8);
    }

    [Fact]
    public void Get_UnknownHandle_Throws()
    {
        var arena = NewArena();

        Action bad = () => arena.Get(new NodeHandle(3));

        bad.Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var arena = NewArena();
        arena.Allocate(1);

        arena.Dispose();
        Action again = () => arena.Dispose();

        again.Should().NotThrow();
        _arena = null;
    }
}
