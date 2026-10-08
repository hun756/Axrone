using System;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;

namespace Axrone.Geometry.Tests;

public class GeometryJsonTests
{
    [Fact]
    public void Context_ExposesMetadata_ForAllShapes()
    {
        AabbJsonContext.Default.Aabb3D.Should().NotBeNull();
        AabbJsonContext.Default.Aabb2D.Should().NotBeNull();
        AabbJsonContext.Default.Ray3D.Should().NotBeNull();
        AabbJsonContext.Default.Triangle3.Should().NotBeNull();
        AabbJsonContext.Default.TriangleHit3.Should().NotBeNull();
        AabbJsonContext.Default.SweepHit3.Should().NotBeNull();
        AabbJsonContext.Default.BoundingSphere3.Should().NotBeNull();
        AabbJsonContext.Default.ContainmentType.Should().NotBeNull();
        AabbJsonContext.Default.Axis.Should().NotBeNull();
        AabbJsonContext.Default.SpatialItemId.Should().NotBeNull();
        AabbJsonContext.Default.RayHit.Should().NotBeNull();
        AabbJsonContext.Default.SweepResult.Should().NotBeNull();
        AabbJsonContext.Default.RayIntersection.Should().NotBeNull();
    }

    [Fact]
    public void SpatialItemId_RoundTrips()
    {
        var id = new SpatialItemId(42);
        string json = JsonSerializer.Serialize(id, AabbJsonContext.Default.SpatialItemId);
        JsonSerializer.Deserialize(json, AabbJsonContext.Default.SpatialItemId).Should().Be(id);
    }

    [Fact]
    public void RayHit_RoundTrips()
    {
        var hit = new RayHit(new SpatialItemId(7), 1.5f);
        string json = JsonSerializer.Serialize(hit, AabbJsonContext.Default.RayHit);
        JsonSerializer.Deserialize(json, AabbJsonContext.Default.RayHit).Should().Be(hit);
    }

    [Fact]
    public void ContainmentType_RoundTrips()
    {
        string json = JsonSerializer.Serialize(ContainmentType.Intersects, AabbJsonContext.Default.ContainmentType);
        JsonSerializer.Deserialize(json, AabbJsonContext.Default.ContainmentType).Should().Be(ContainmentType.Intersects);
    }

    [Fact]
    public void Axis_RoundTrips()
    {
        string json = JsonSerializer.Serialize(Axis.Z, AabbJsonContext.Default.Axis);
        JsonSerializer.Deserialize(json, AabbJsonContext.Default.Axis).Should().Be(Axis.Z);
    }
}
