namespace Axrone.Raycast.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class RaycastBroadphaseTests
{
    [Fact]
    public void SpatialHash2DInsertsUpdatesAndRemoves()
    {
        var grid = new SpatialHashGrid2D<int>(1f);
        var bounds = new Aabb2D(0.1f, 0.1f, 0.9f, 0.9f);
        grid.Insert(1, in bounds);
        grid.ItemCount.Should().Be(1);
        grid.CellCount.Should().Be(1);
        grid.Contains(1).Should().BeTrue();

        Span<int> results = stackalloc int[4];
        var query = new Aabb2D(0.1f, 0.1f, 0.2f, 0.2f);
        grid.Query(in query, results).Should().Be(1);

        var moved = new Aabb2D(10.1f, 10.1f, 10.9f, 10.9f);
        grid.Update(1, in moved);
        grid.Query(in query, results).Should().Be(0);
        var movedQuery = new Aabb2D(10.1f, 10.1f, 10.2f, 10.2f);
        grid.Query(in movedQuery, results).Should().Be(1);

        grid.Remove(1).Should().BeTrue();
        grid.Remove(1).Should().BeFalse();
        grid.Contains(1).Should().BeFalse();
        grid.ItemCount.Should().Be(0);
        grid.CellCount.Should().Be(0);
    }

    [Fact]
    public void SpatialHash2DRejectsDuplicateInsertAndMissingUpdate()
    {
        var grid = new SpatialHashGrid2D<int>(1f);
        var bounds = new Aabb2D(0f, 0f, 1f, 1f);
        grid.Insert(1, in bounds);

        Action duplicate = () => grid.Insert(1, in bounds);
        duplicate.Should().Throw<InvalidOperationException>();

        Action missing = () => grid.Update(2, in bounds);
        missing.Should().Throw<InvalidOperationException>();

        Action badCell = () => _ = new SpatialHashGrid2D<int>(0f);
        badCell.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SpanQueryReportsDuplicatesForMultiCellItems()
    {
        var grid = new SpatialHashGrid2D<int>(1f);
        var bounds = new Aabb2D(0.5f, 0.5f, 1.5f, 1.5f);
        grid.Insert(1, in bounds);
        grid.CellCount.Should().Be(4);

        Span<int> results = stackalloc int[8];
        var query = new Aabb2D(0f, 0f, 2f, 2f);
        grid.Query(in query, results).Should().Be(4);
    }

    [Fact]
    public void SpatialHash2DDdaVisitsTheRayCorridor()
    {
        var grid = new SpatialHashGrid2D<int>(1f);
        var bounds = new Aabb2D(5f, 0f, 5.5f, 0.5f);
        grid.Insert(11, in bounds);

        var ray = new Ray2D(new Vec2(0f, 0.25f), Vec2.UnitX);
        var visitor = new CountingVisitor();
        grid.Raycast(in ray, 10f, ref visitor);
        visitor.Count.Should().Be(1);

        var miss = new Ray2D(new Vec2(0f, 0.25f), Vec2.UnitY);
        var missVisitor = new CountingVisitor();
        grid.Raycast(in miss, 10f, ref missVisitor);
        missVisitor.Count.Should().Be(0);
    }

    [Fact]
    public void SpatialHash3DInsertsQueriesAndWalksTheRay()
    {
        var grid = new SpatialHashGrid3D<int>(1f);
        var bounds = new Aabb3D(5f, 0f, 0f, 5.5f, 0.5f, 0.5f);
        grid.Insert(7, in bounds);
        grid.ItemCount.Should().Be(1);

        var ray = new Ray3D(new Vec3(0f, 0.25f, 0.25f), Vec3.UnitX);
        var visitor = new CountingVisitor();
        grid.Raycast(in ray, 10f, ref visitor);
        visitor.Count.Should().Be(1);

        var miss = new Ray3D(new Vec3(0f, 0.25f, 0.25f), Vec3.UnitY);
        var missVisitor = new CountingVisitor();
        grid.Raycast(in miss, 10f, ref missVisitor);
        missVisitor.Count.Should().Be(0);

        var boxQuery = new Aabb3D(4.5f, 0f, 0f, 6f, 1f, 1f);
        var boxVisitor = new CountingVisitor();
        grid.Query(in boxQuery, ref boxVisitor);
        boxVisitor.Count.Should().Be(1);

        Action missing = () => grid.Update(8, in bounds);
        missing.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExpandedRayFindsItemsBesideTheCenterLine()
    {
        var grid = new SpatialHashGrid3D<int>(1f);
        var bounds = new Aabb3D(0f, 2.1f, 0f, 0.5f, 2.4f, 0.5f);
        grid.Insert(3, in bounds);

        var ray = new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX);
        var narrow = new CountingVisitor();
        grid.RaycastExpanded(in ray, 10f, new Vec3(0.5f), ref narrow);
        narrow.Count.Should().Be(0);

        var wide = new CountingVisitor();
        grid.RaycastExpanded(in ray, 10f, new Vec3(1.5f), ref wide);
        wide.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OctreeBroadphaseIndexesAndRemoves()
    {
        using var octree = new OctreeBroadphase3D<int>(new Aabb3D(new Vec3(-10f), new Vec3(10f)));
        var bounds = new Aabb3D(-1f, -1f, -1f, 1f, 1f, 1f);
        octree.Insert(5, in bounds);
        octree.Count.Should().Be(1);
        octree.Contains(5).Should().BeTrue();
        octree.RootBounds.Min.X.Should().Be(-10f);

        Span<int> results = stackalloc int[4];
        var ray = new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX);
        octree.QueryRay(in ray, 20f, results).Should().Be(1);
        results[0].Should().Be(5);

        var box = new Aabb3D(-2f, -2f, -2f, 2f, 2f, 2f);
        octree.QueryBounds(in box, results).Should().Be(1);

        var visitor = new CountingVisitor();
        octree.QueryRay(in ray, 20f, ref visitor);
        visitor.Count.Should().Be(1);

        var boxVisitor = new CountingVisitor();
        octree.QueryBounds(in box, ref boxVisitor);
        boxVisitor.Count.Should().Be(1);

        var moved = new Aabb3D(4f, 4f, 4f, 5f, 5f, 5f);
        octree.Update(5, in moved).Should().BeTrue();
        octree.QueryBounds(in box, results).Should().Be(0);

        octree.Remove(5).Should().BeTrue();
        octree.Remove(5).Should().BeFalse();
        octree.Count.Should().Be(0);
    }

    [Fact]
    public void OctreeRejectsDuplicatesAndOutOfDomain()
    {
        using var octree = new OctreeBroadphase3D<int>(new Aabb3D(new Vec3(-10f), new Vec3(10f)));
        var bounds = new Aabb3D(-1f, -1f, -1f, 1f, 1f, 1f);
        octree.Insert(1, in bounds);

        Action duplicate = () => octree.Insert(1, in bounds);
        duplicate.Should().Throw<InvalidOperationException>();

        var outside = new Aabb3D(50f, 50f, 50f, 51f, 51f, 51f);
        Action outOfDomain = () => octree.Insert(2, in outside);
        outOfDomain.Should().Throw<ArgumentOutOfRangeException>();
        octree.Count.Should().Be(1);
    }

    private ref struct CountingVisitor : ISpatialHashItemVisitor<int>, IRaycastVisitor<int>, IOctreeVisitor<int>
    {
        public int Count;

        public bool OnItem(int item)
        {
            Count++;
            return true;
        }

        public bool OnHit(OctreeHandle handle, in Aabb3D bounds, in int value, Distance distance) => OnItem(value);

        public bool OnVisit(OctreeHandle handle, in Aabb3D bounds, in int value) => OnItem(value);
    }
}
