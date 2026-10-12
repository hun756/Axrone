using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;

namespace Axrone.Geometry.Tests;

public class MeshPrimitivesTests
{
    [Fact]
    public void Metric_Arithmetic_Works()
    {
        (new Metric(1.5f) + new Metric(2.5f)).Value.Should().Be(4.0f);
        (new Metric(5.0f) - new Metric(2.0f)).Value.Should().Be(3.0f);
        (new Metric(2.0f) * 3.0f).Value.Should().Be(6.0f);
        (new Metric(6.0f) / 2.0f).Value.Should().Be(3.0f);
        Metric.One.Value.Should().Be(1.0f);
        new Metric(1.0f).CompareTo(new Metric(2.0f)).Should().BeNegative();
    }

    [Fact]
    public void SegmentResolution_Clamps()
    {
        SegmentResolution.Clamp(0, 3).Value.Should().Be((uint)3);
        SegmentResolution.Clamp(8, 3).Value.Should().Be((uint)8);
    }

    [Fact]
    public void Position3D_RoundTrips()
    {
        var pos = new Position3D(1.0f, 2.0f, 3.0f);

        pos.ToVec3().Should().Be(new Vec3(1.0f, 2.0f, 3.0f));
        Position3D.FromVec3(new Vec3(4.0f, 5.0f, 6.0f)).Should().Be(new Position3D(4.0f, 5.0f, 6.0f));
        (pos + new Vec3(1.0f, 1.0f, 1.0f)).Should().Be(new Position3D(2.0f, 3.0f, 4.0f));
        (pos - new Vec3(1.0f, 1.0f, 1.0f)).Should().Be(new Position3D(0.0f, 1.0f, 2.0f));
        Position3D.Zero.Should().Be(new Position3D(0.0f, 0.0f, 0.0f));
    }

    [Fact]
    public void Normal3D_Normalizes_AndFallsBack()
    {
        Normal3D.FromVec3(new Vec3(0.0f, 0.0f, 5.0f)).Should().Be(Normal3D.UnitZ);
        Normal3D.FromVec3(Vec3.Zero).Should().Be(Normal3D.UnitY);
        Normal3D.UnitX.ToVec3().Should().Be(new Vec3(1.0f, 0.0f, 0.0f));
    }

    [Fact]
    public void TexCoord_RoundTrips()
    {
        TexCoord.Center.Should().Be(new TexCoord(0.5f, 0.5f));
        new TexCoord(0.25f, 0.75f).ToVec2().Should().Be(new Vec2(0.25f, 0.75f));
        TexCoord.FromVec2(new Vec2(0.1f, 0.2f)).Should().Be(new TexCoord(0.1f, 0.2f));
    }

    [Fact]
    public void Tangent4D_RoundTrips()
    {
        Tangent4D.Default.Should().Be(new Tangent4D(1.0f, 0.0f, 0.0f, 1.0f));
        new Tangent4D(0.0f, 1.0f, 0.0f, -1.0f).ToVec4().Should().Be(new Vec4(0.0f, 1.0f, 0.0f, -1.0f));
        Tangent4D.FromVec4(new Vec4(1.0f, 2.0f, 3.0f, 4.0f)).Should().Be(new Tangent4D(1.0f, 2.0f, 3.0f, 4.0f));
    }

    [Fact]
    public void VertexId_Orders()
    {
        new VertexId(1u).CompareTo(new VertexId(2u)).Should().BeNegative();
        new CapacityAllocation(10u, 30u).Should().Be(new CapacityAllocation(10u, 30u));
    }
}
