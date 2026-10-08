using System;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;

namespace Axrone.Geometry.Tests;

public class TreeBuilderTests
{
    [Fact]
    public void Build_AfterStrategy_ReturnsUsableTree()
    {
        var tree = DynamicAabbTreeBuilder<string, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink, StageUnconfigured>
            .Create()
            .WithStrategy<SurfaceAreaHeuristicStrategy>()
            .Build();

        tree.Count.Should().Be(0);
        SpatialItemId id = tree.Insert(new Aabb3D(new Vec3(0, 0, 0), new Vec3(1, 1, 1)), "a");
        id.IsValid.Should().BeTrue();
        tree.Count.Should().Be(1);
    }

    [Fact]
    public void Build_FullyConfigured_RespectsOptions()
    {
        var options = new TreeOptions(0.5f, 3.0f, new TreeCapacity(256));
        var tree = DynamicAabbTreeBuilder<string, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink, StageUnconfigured>
            .Create()
            .WithStrategy<SurfaceAreaHeuristicStrategy>()
            .WithMetrics<NullSpatialMetricsSink>()
            .WithOptions(options)
            .Build();

        tree.FatteningMargin.Should().Be(0.5f);
        tree.VelocityMultiplier.Should().Be(3.0f);
    }

    [Fact]
    public void Build_WithMetrics_KeepsStage()
    {
        var tree = DynamicAabbTreeBuilder<string, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink, StageUnconfigured>
            .Create()
            .WithStrategy<SurfaceAreaHeuristicStrategy>()
            .WithMetrics<NullSpatialMetricsSink>()
            .Build();

        tree.Should().NotBeNull();
        tree.Count.Should().Be(0);
    }
}
