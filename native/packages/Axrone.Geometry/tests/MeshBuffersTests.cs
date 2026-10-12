using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;

namespace Axrone.Geometry.Tests;

public class MeshBuffersTests
{
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
