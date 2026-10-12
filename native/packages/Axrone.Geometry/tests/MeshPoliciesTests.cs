using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;

namespace Axrone.Geometry.Tests;

public class MeshPoliciesTests
{
    [Fact]
    public void Winding_EmitsExpectedOrder()
    {
        Span<int> ccw = [0, 0, 0];
        CounterClockwiseWinding.EmitTriangle(ccw, 0, 1, 2, 3);
        ccw.ToArray().Should().Equal(1, 2, 3);

        Span<int> cw = [0, 0, 0, 0, 0, 0];
        ClockwiseWinding.EmitTriangle(cw, 3, 1, 2, 3);
        cw.ToArray().Should().Equal(0, 0, 0, 1, 3, 2);
    }

    [Fact]
    public void IndexPolicy_Converts_WithOverflowCheck()
    {
        UInt16IndexPolicy.AttributeType.Should().Be(GLAttributeType.UnsignedShort);
        UInt32IndexPolicy.AttributeType.Should().Be(GLAttributeType.UnsignedInt);

        UInt16IndexPolicy.FromUInt32(60000).Should().Be((ushort)60000);
        Action overflow = () => UInt16IndexPolicy.FromUInt32(70000);
        overflow.Should().Throw<OverflowException>();
        UInt16IndexPolicy.ToUInt32(60000).Should().Be(60000u);
        UInt32IndexPolicy.FromUInt32(uint.MaxValue).Should().Be(uint.MaxValue);
    }
}
