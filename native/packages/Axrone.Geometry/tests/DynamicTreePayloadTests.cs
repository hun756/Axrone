namespace Axrone.Geometry.Tests;

using Axrone.Geometry;
using Axrone.Numeric;

using FluentAssertions;
using Xunit;

public class DynamicTreePayloadTests
{
    private readonly struct MapVisitor : ISpatialVisitor<string, Dictionary<uint, string>>
    {
        public bool OnOverlap(SpatialItemId itemId, string userData, in Aabb3D box, ref Dictionary<uint, string> context)
        {
            context[itemId.RawValue] = userData;
            return true;
        }
    }

    private readonly struct MapRayVisitor : IRayHitVisitor<string, Dictionary<uint, string>>
    {
        public bool OnHit(SpatialItemId itemId, string userData, in Ray3D ray, ref float maxDistance, ref Dictionary<uint, string> context)
        {
            context[itemId.RawValue] = userData;
            return true;
        }
    }

    private readonly struct MapFrustumVisitor : IFrustumVisitor<string, Dictionary<uint, string>>
    {
        public bool OnVisible(SpatialItemId itemId, string userData, in Aabb3D box, ref Dictionary<uint, string> context)
        {
            context[itemId.RawValue] = userData;
            return true;
        }
    }

    private readonly struct CheckPairVisitor : IPairVisitor<string, Dictionary<uint, string>>
    {
        public bool OnPair(SpatialItemId idA, string dataA, SpatialItemId idB, string dataB, ref Dictionary<uint, string> context)
        {
            dataA.Should().Be(context[idA.RawValue]);
            dataB.Should().Be(context[idB.RawValue]);
            return true;
        }
    }

    private static DynamicAabbTree<string, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink> CreateTree() =>
        new(new TreeOptions());

    private static DynamicAabbTree<string, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink> CreateChain(out SpatialItemId[] ids)
    {
        var tree = CreateTree();
        ids = new SpatialItemId[5];
        for (int i = 0; i < 5; i++)
        {
            ids[i] = tree.Insert(
                new Aabb3D(new Vec3(i, 0, 0), new Vec3(i + 1, 1, 1)),
                $"p{i}");
        }

        return tree;
    }

    private static Dictionary<uint, string> Truth(
        DynamicAabbTree<string, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink> tree,
        SpatialItemId[] ids)
    {
        var truth = new Dictionary<uint, string>();
        foreach (SpatialItemId id in ids)
        {
            truth[id.RawValue] = tree.GetUserData(id);
        }

        return truth;
    }

    [Fact]
    public void OverlapVisitor_PayloadMatchesId()
    {
        using var tree = CreateChain(out SpatialItemId[] ids);
        var truth = Truth(tree, ids);

        var seen = new Dictionary<uint, string>();
        var visitor = new MapVisitor();
        int hits = tree.QueryOverlaps(
            new Aabb3D(new Vec3(-1, -1, -1), new Vec3(6, 2, 2)),
            ref visitor,
            ref seen);

        hits.Should().Be(5);
        seen.Should().BeEquivalentTo(truth);
    }

    [Fact]
    public void RayVisitor_PayloadMatchesId()
    {
        using var tree = CreateChain(out SpatialItemId[] ids);
        var truth = Truth(tree, ids);

        var seen = new Dictionary<uint, string>();
        var visitor = new MapRayVisitor();
        var ray = new Ray3D(new Vec3(-1, 0.5f, 0.5f), new Vec3(1, 0, 0));
        float maxDistance = float.PositiveInfinity;
        tree.RayCast(in ray, maxDistance, ref visitor, ref seen);

        seen.Should().BeEquivalentTo(truth);
    }

    [Fact]
    public void FrustumVisitor_PayloadMatchesId()
    {
        using var tree = CreateChain(out SpatialItemId[] ids);
        var truth = Truth(tree, ids);

        var frustum = new Frustum3(
            new Plane(new Vec3(0, 0, 1), 1),
            new Plane(new Vec3(0, 0, -1), 2),
            new Plane(new Vec3(1, 0, 0), 1),
            new Plane(new Vec3(-1, 0, 0), 6),
            new Plane(new Vec3(0, -1, 0), 2),
            new Plane(new Vec3(0, 1, 0), 1));

        var seen = new Dictionary<uint, string>();
        var visitor = new MapFrustumVisitor();
        tree.FrustumCull(in frustum, ref visitor, ref seen);

        seen.Should().BeEquivalentTo(truth);
    }

    [Fact]
    public void PairVisitor_PayloadMatchesIds()
    {
        using var tree = CreateChain(out SpatialItemId[] ids);
        var truth = Truth(tree, ids);

        var visitor = new CheckPairVisitor();
        tree.FindPairs(ref visitor, ref truth);
    }

    [Fact]
    public void Knn_PayloadMatchesIds()
    {
        using var tree = CreateChain(out SpatialItemId[] ids);

        KnnResult<string>[] results = new KnnResult<string>[5];
        int count = tree.QueryKNearest(new Vec3(0.5f, 0.5f, 0.5f), results);

        count.Should().Be(5);
        foreach (KnnResult<string> result in results[..count].ToArray())
        {
            result.UserData.Should().Be(tree.GetUserData(result.ItemId));
        }
    }
}
