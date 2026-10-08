namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;

public class SurfaceAreaHeuristicStrategyTests
{
    [Fact]
    public void Sah_ComputeCost_SumsAreas()
    {
        Aabb3D first = Aabb3D.UnitCube;
        Aabb3D second = Aabb3D.UnitCube;

        SurfaceAreaHeuristicStrategy.ComputeCost(in first, in second).Should().Be(12F);
    }

    [Fact]
    public void Sah_ChooseSubtree_PicksSmallerGrowth()
    {
        Aabb3D left = new(0F, 0F, 0F, 1F, 1F, 1F);
        Aabb3D right = new(10F, 10F, 10F, 11F, 11F, 11F);
        Aabb3D newBox = new(1F, 0F, 0F, 2F, 1F, 1F);

        Aabb3D.CreateMerged(in left, in newBox).SurfaceArea().Should().Be(10F);
        Aabb3D.CreateMerged(in right, in newBox).SurfaceArea().Should().Be(682F);

        SurfaceAreaHeuristicStrategy.ChooseSubtree(in left, in right, in newBox).Should().Be(0);
    }

    [Fact]
    public void Sah_ChooseSubtree_PicksRightChild_WhenItGrowsLess()
    {
        Aabb3D left = new(0F, 0F, 0F, 1F, 1F, 1F);
        Aabb3D right = new(10F, 10F, 10F, 11F, 11F, 11F);
        Aabb3D newBox = new(9F, 10F, 10F, 10F, 11F, 11F);

        Aabb3D.CreateMerged(in left, in newBox).SurfaceArea().Should().Be(682F);
        Aabb3D.CreateMerged(in right, in newBox).SurfaceArea().Should().Be(10F);

        SurfaceAreaHeuristicStrategy.ChooseSubtree(in left, in right, in newBox).Should().Be(1);
    }
}

public class NullSpatialMetricsSinkTests
{
    [Fact]
    public void NullSink_Calls_DoNotThrow()
    {
        NullSpatialMetricsSink.OnNodeVisited();
        NullSpatialMetricsSink.OnIntersectionTested(true);
        NullSpatialMetricsSink.OnIntersectionTested(false);
        NullSpatialMetricsSink.OnItemInserted();
        NullSpatialMetricsSink.OnItemRemoved();
    }
}

public class SpatialVisitorContractTests
{
    [Fact]
    public void VisitorConstraints_Compile()
    {
        Aabb3D box = new(0F, 0F, 0F, 2F, 2F, 2F);

        var classContext = new object();
        CountingVisitor visitor = default;
        ProbeOverlap("payload", in box, ref visitor, ref classContext).Should().BeTrue();

        Span<int> spanContext = stackalloc int[4];
        SpanVisitor visitor2 = default;
        ProbeOverlap("payload", in box, ref visitor2, ref spanContext).Should().BeTrue();
    }

    private static bool ProbeOverlap<TVisitor, TUserData, TContext>(
        TUserData userData,
        in Aabb3D box,
        ref TVisitor visitor,
        ref TContext context)
        where TVisitor : struct, ISpatialVisitor<TUserData, TContext>
        where TContext : allows ref struct
    {
        return visitor.OnOverlap(new(7U), userData, in box, ref context);
    }
}

internal readonly struct CountingVisitor : ISpatialVisitor<string, object>
{
    public bool OnOverlap(SpatialItemId itemId, string userData, in Aabb3D box, ref object context) => true;
}

internal readonly struct SpanVisitor : ISpatialVisitor<string, Span<int>>
{
    public bool OnOverlap(SpatialItemId itemId, string userData, in Aabb3D box, ref Span<int> context) => true;
}
