using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;

namespace Axrone.Geometry.Tests;

public class MeshBuffersTests
{
    [Fact]
    public void NativeBuffer_Appends_AndGrows()
    {
        using NativeBuffer<int> buffer = new(2);

        for (int i = 0; i < 100; i++)
        {
            buffer.Append(i);
        }

        buffer.Length.Should().Be((nuint)100);
        buffer.Capacity.Should().BeGreaterThanOrEqualTo((nuint)100);
        buffer.AsRef(99).Should().Be(99);
        buffer.AsSpan().ToArray().Should().Equal(Enumerable.Range(0, 100).ToArray());
    }

    [Fact]
    public void NativeBuffer_AsRef_OutOfRange_Throws()
    {
        using NativeBuffer<int> buffer = new(4);
        buffer.Append(1);

        Action act = () => buffer.AsRef(7);

        act.Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void NativeBuffer_SetLength_AndClear()
    {
        using NativeBuffer<int> buffer = new(4);

        buffer.SetLength(64);
        buffer.Length.Should().Be((nuint)64);
        buffer.Capacity.Should().BeGreaterThanOrEqualTo((nuint)64);

        buffer.Clear();
        buffer.Length.Should().Be((nuint)0);
    }

    [Fact]
    public void EdgeTable_DeduplicatesEdges_RegardlessOfOrder()
    {
        using UnmanagedEdgeTable table = new(16);

        table.TryGetOrAdd(3, 7, 100, out uint first).Should().BeFalse();
        first.Should().Be(100u);
        table.TryGetOrAdd(7, 3, 999, out uint second).Should().BeTrue();
        second.Should().Be(100u);
    }

    [Fact]
    public void EdgeTable_GrowsPastInitialCapacity()
    {
        using UnmanagedEdgeTable table = new(16);

        for (uint i = 0; i < 500; i++)
        {
            table.TryGetOrAdd(i, i + 100000, i, out _).Should().BeFalse();
        }

        for (uint i = 0; i < 500; i++)
        {
            table.TryGetOrAdd(i + 100000, i, 99999, out uint existing).Should().BeTrue();
            existing.Should().Be(i);
        }
    }
}
