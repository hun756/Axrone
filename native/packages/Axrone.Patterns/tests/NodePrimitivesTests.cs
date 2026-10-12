using System;
using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using Axrone.Patterns;

namespace Axrone.Patterns.Tests;

public class NodeTypeIdTests
{
    [Fact]
    public void Invalid_IsNotValid()
    {
        NodeTypeId.Invalid.IsValid.Should().BeFalse();
        new NodeTypeId(-5).IsValid.Should().BeFalse();
        new NodeTypeId(0).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Ordering_FollowsValue()
    {
        (new NodeTypeId(1) < new NodeTypeId(2)).Should().BeTrue();
        (new NodeTypeId(2) > new NodeTypeId(1)).Should().BeTrue();
        (new NodeTypeId(2) <= new NodeTypeId(2)).Should().BeTrue();
        (new NodeTypeId(2) >= new NodeTypeId(2)).Should().BeTrue();
        new NodeTypeId(3).CompareTo(new NodeTypeId(4)).Should().BeNegative();
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        (new NodeTypeId(7) == new NodeTypeId(7)).Should().BeTrue();
        (new NodeTypeId(7) != new NodeTypeId(8)).Should().BeTrue();
        new NodeTypeId(7).GetHashCode().Should().Be(7);
    }

    [Fact]
    public void ToString_UsesNamedForm()
    {
        new NodeTypeId(42).ToString().Should().Be("Type(42)");
        NodeTypeId.Invalid.ToString().Should().Be("Type(Invalid)");
    }
}

public class NodeHandleTests
{
    [Fact]
    public void Invalid_IsNotValid()
    {
        NodeHandle.Invalid.IsValid.Should().BeFalse();
        new NodeHandle(0).IsValid.Should().BeTrue();
    }

    [Fact]
    public void OrderingAndEquality_FollowValue()
    {
        (new NodeHandle(1) < new NodeHandle(2)).Should().BeTrue();
        (new NodeHandle(2) > new NodeHandle(1)).Should().BeTrue();
        (new NodeHandle(5) == new NodeHandle(5)).Should().BeTrue();
        (new NodeHandle(5) != new NodeHandle(6)).Should().BeTrue();
        new NodeHandle(9).GetHashCode().Should().Be(9);
    }

    [Fact]
    public void ToString_UsesNamedForm()
    {
        new NodeHandle(17).ToString().Should().Be("Handle(17)");
        NodeHandle.Invalid.ToString().Should().Be("Handle(Invalid)");
    }
}

public class NodeRangeTests
{
    [Fact]
    public void Empty_IsEmpty()
    {
        NodeRange.Empty.IsEmpty.Should().BeTrue();
        new NodeRange(3, 0).IsEmpty.Should().BeTrue();
        new NodeRange(3, 2).IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Contains_CoversHalfOpenInterval()
    {
        var range = new NodeRange(10, 5);

        range.Contains(9).Should().BeFalse();
        range.Contains(10).Should().BeTrue();
        range.Contains(14).Should().BeTrue();
        range.Contains(15).Should().BeFalse();
        range.Contains(-1).Should().BeFalse();
        range.Contains(int.MaxValue).Should().BeFalse();
    }
}

public class UnitTests
{
    [Fact]
    public void BehavesAsSingleValue()
    {
        Unit.Value.CompareTo(Unit.Value).Should().Be(0);
        Unit.Value.Should().Be(Unit.Value);
        Unit.Value.ToString().Should().Be("()");
    }
}

public class InlineBufferTests
{
    [Fact]
    public void Buffer128_RoundTripsLanes()
    {
        InlineBuffer128<int> buffer = default;
        buffer[0] = 11;
        buffer[127] = 77;

        buffer[0].Should().Be(11);
        buffer[127].Should().Be(77);
        for (int i = 0; i < 128; i++)
        {
            buffer[i] = i;
        }
        buffer[64].Should().Be(64);
    }

    [Fact]
    public void Buffer256_RoundTripsLanes()
    {
        InlineBuffer256<NodeHandle> buffer = default;
        buffer[0] = new NodeHandle(3);
        buffer[255] = NodeHandle.Invalid;

        buffer[0].Should().Be(new NodeHandle(3));
        buffer[255].Should().Be(NodeHandle.Invalid);
        for (int i = 0; i < 256; i++)
        {
            buffer[i] = new NodeHandle(i);
        }
        buffer[200].Should().Be(new NodeHandle(200));
    }

    [Fact]
    public void Counters_AreCacheLineIsolated()
    {
        Marshal.SizeOf<CachePaddedAtomicCounters>().Should().Be(128);
        Marshal.OffsetOf<CachePaddedAtomicCounters>(nameof(CachePaddedAtomicCounters.Value)).ToInt64().Should().Be(0);
        Marshal.OffsetOf<CachePaddedAtomicCounters>(nameof(CachePaddedAtomicCounters.LockSignal)).ToInt64().Should().Be(64);
    }
}
