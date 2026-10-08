using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;
using FluentAssertions;
using Axrone.Geometry;
using Axrone.Numeric;

namespace Axrone.Geometry.Tests;

public class DynamicTreeQueryTests
{
    private readonly struct CollectVisitor : ISpatialVisitor<string, List<SpatialItemId>>
    {
        public bool OnOverlap(SpatialItemId itemId, string userData, in Aabb3D box, ref List<SpatialItemId> context)
        {
            context.Add(itemId);
            return true;
        }
    }

    private readonly struct StopAfterFirstVisitor : ISpatialVisitor<string, int>
    {
        public bool OnOverlap(SpatialItemId itemId, string userData, in Aabb3D box, ref int context)
        {
            context++;
            return false;
        }
    }

    private readonly struct ShrinkRayVisitor : IRayHitVisitor<string, List<SpatialItemId>>
    {
        public bool OnHit(SpatialItemId itemId, string userData, in Ray3D ray, ref float maxDistance, ref List<SpatialItemId> context)
        {
            context.Add(itemId);
            maxDistance = 0.5f;
            return true;
        }
    }

    private readonly struct CollectVisibleVisitor : IFrustumVisitor<string, List<SpatialItemId>>
    {
        public bool OnVisible(SpatialItemId itemId, string userData, in Aabb3D box, ref List<SpatialItemId> context)
        {
            context.Add(itemId);
            return true;
        }
    }

    private readonly struct CollectPairVisitor : IPairVisitor<string, List<(uint, uint)>>
    {
        public bool OnPair(SpatialItemId idA, string dataA, SpatialItemId idB, string dataB, ref List<(uint, uint)> context)
        {
            uint a = idA.Value;
            uint b = idB.Value;
            context.Add(a < b ? (a, b) : (b, a));
            return true;
        }
    }

    private static DynamicAabbTree<string, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink> NewTree() =>
        new(new TreeOptions());

    private static Aabb3D Box(float minX, float minY, float minZ, float maxX, float maxY, float maxZ) =>
        new(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ));

    private static Frustum3 UnitCubeFrustum() => new(
        new Plane(new Vector3(1, 0, 0), 1),
        new Plane(new Vector3(-1, 0, 0), 1),
        new Plane(new Vector3(0, 1, 0), 1),
        new Plane(new Vector3(0, -1, 0), 1),
        new Plane(new Vector3(0, 0, 1), 1),
        new Plane(new Vector3(0, 0, -1), 1));

    [Fact]
    public void OverlapsSpan_ReturnsMatchingIds()
    {
        var tree = NewTree();
        SpatialItemId a = tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(5, 5, 5, 6, 6, 6), "b");
        SpatialItemId c = tree.Insert(Box(0.5f, 0.5f, 0.5f, 2, 2, 2), "c");

        Span<SpatialItemId> results = stackalloc SpatialItemId[8];
        int count = tree.QueryOverlaps(Box(-1, -1, -1, 3, 3, 3), results);

        count.Should().Be(2);
        results[..count].ToArray().ToHashSet().Should().BeEquivalentTo([a, c]);
    }

    [Fact]
    public void OverlapsSpan_EmptyTree_ReturnsZero()
    {
        var tree = NewTree();
        Span<SpatialItemId> results = stackalloc SpatialItemId[8];
        tree.QueryOverlaps(Box(-1, -1, -1, 1, 1, 1), results).Should().Be(0);
    }

    [Fact]
    public void OverlapsVisitor_EarlyExit_StopsTraversal()
    {
        var tree = NewTree();
        tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(0.5f, 0.5f, 0.5f, 2, 2, 2), "c");

        var visitor = new StopAfterFirstVisitor();
        int calls = 0;
        tree.QueryOverlaps(Box(-1, -1, -1, 3, 3, 3), ref visitor, ref calls);

        calls.Should().Be(1);
    }

    [Fact]
    public void EnumerateOverlaps_YieldsSameSetAsSpan()
    {
        var tree = NewTree();
        SpatialItemId a = tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(5, 5, 5, 6, 6, 6), "b");
        SpatialItemId c = tree.Insert(Box(0.5f, 0.5f, 0.5f, 2, 2, 2), "c");

        Aabb3D query = Box(-1, -1, -1, 3, 3, 3);
        Span<NodeIndex> buffer = stackalloc NodeIndex[64];
        var found = new List<SpatialItemId>();
        var enumerator = tree.EnumerateOverlaps(in query, buffer).GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                found.Add(enumerator.Current);
            }
        }
        finally
        {
            enumerator.Dispose();
        }

        found.ToHashSet().Should().BeEquivalentTo([a, c]);
    }

    [Fact]
    public void RayCastSpan_HitDistance()
    {
        var tree = NewTree();
        SpatialItemId a = tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(5, 5, 5, 6, 6, 6), "b");
        tree.Insert(Box(0.5f, 2, 0.5f, 2, 3, 2), "c");

        var ray = new Ray3D(new Vec3(-1, 0.5f, 0.5f), new Vec3(1, 0, 0));
        Span<RayHit> results = stackalloc RayHit[8];
        int count = tree.RayCast(in ray, results);

        count.Should().Be(1);
        results[0].ItemId.Should().Be(a);
        results[0].Distance.Should().BeApproximately(0.9f, 1e-4f);
    }

    [Fact]
    public void RayCastVisitor_ShrinkStopsFurther()
    {
        var tree = NewTree();
        tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(2, 0, 0, 3, 1, 1), "d");

        var ray = new Ray3D(new Vec3(-1, 0.5f, 0.5f), new Vec3(1, 0, 0));
        var visitor = new ShrinkRayVisitor();
        var seen = new List<SpatialItemId>();
        tree.RayCast(in ray, float.PositiveInfinity, ref visitor, ref seen);

        seen.Should().HaveCount(1);
    }

    [Fact]
    public void FrustumCull_SeesInside_HidesOutside()
    {
        var tree = NewTree();
        SpatialItemId inside = tree.Insert(Box(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f), "in");
        tree.Insert(Box(5, 5, 5, 6, 6, 6), "out");

        var frustum = UnitCubeFrustum();
        var visitor = new CollectVisibleVisitor();
        var seen = new List<SpatialItemId>();
        tree.FrustumCull(in frustum, ref visitor, ref seen);

        seen.Should().BeEquivalentTo([inside]);
    }

    [Fact]
    public void FindPairs_ReportsOverlappingPairOnce()
    {
        var tree = NewTree();
        SpatialItemId a = tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");
        tree.Insert(Box(5, 5, 5, 6, 6, 6), "b");
        SpatialItemId c = tree.Insert(Box(0.5f, 0.5f, 0.5f, 2, 2, 2), "c");

        var visitor = new CollectPairVisitor();
        var pairs = new List<(uint, uint)>();
        tree.FindPairs(ref visitor, ref pairs);

        pairs.Should().HaveCount(1);
        uint x = a.Value;
        uint y = c.Value;
        pairs[0].Should().Be(x < y ? (x, y) : (y, x));
    }

    [Fact]
    public void FindPairs_SingleItem_NoPairs()
    {
        var tree = NewTree();
        tree.Insert(Box(0, 0, 0, 1, 1, 1), "a");

        var visitor = new CollectPairVisitor();
        var pairs = new List<(uint, uint)>();
        tree.FindPairs(ref visitor, ref pairs);

        pairs.Should().BeEmpty();
    }

    [Fact]
    public void KNearest_OrderedByDistance()
    {
        var tree = NewTree();
        tree.Insert(Box(10, 0, 0, 11, 1, 1), "far");
        SpatialItemId mid = tree.Insert(Box(4, 0, 0, 5, 1, 1), "mid");
        SpatialItemId near = tree.Insert(Box(1, 0, 0, 2, 1, 1), "near");

        var destination = new KnnResult<string>[2];
        int count = tree.QueryKNearest(new Vec3(0, 0, 0), destination);

        count.Should().Be(2);
        destination[0].ItemId.Should().Be(near);
        destination[1].ItemId.Should().Be(mid);
        destination[0].DistanceSq.Should().BeLessThan(destination[1].DistanceSq);
    }

    [Fact]
    public void KNearest_MaxDistance_Filters()
    {
        var tree = NewTree();
        SpatialItemId near = tree.Insert(Box(1, 0, 0, 2, 1, 1), "near");
        tree.Insert(Box(4, 0, 0, 5, 1, 1), "mid");

        var destination = new KnnResult<string>[4];
        int count = tree.QueryKNearest(new Vec3(0, 0, 0), destination, 3.0f);

        count.Should().Be(1);
        destination[0].ItemId.Should().Be(near);
    }

    [Fact]
    public void KNearest_EmptyDestination_ReturnsZero()
    {
        var tree = NewTree();
        tree.Insert(Box(1, 0, 0, 2, 1, 1), "near");

        int count = tree.QueryKNearest(new Vec3(0, 0, 0), Span<KnnResult<string>>.Empty);

        count.Should().Be(0);
    }
}
