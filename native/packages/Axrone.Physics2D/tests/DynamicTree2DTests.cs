namespace Axrone.Physics2D.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class DynamicTree2DTests
{
    private readonly struct MapVisitor : ISpatialVisitor2D<string, Dictionary<uint, string>>
    {
        public bool OnOverlap(SpatialItemId itemId, string userData, in Aabb2D box, ref Dictionary<uint, string> context)
        {
            context[itemId.Value] = userData;
            return true;
        }
    }

    private readonly struct MapRayVisitor : IRayHitVisitor2D<string, Dictionary<uint, string>>
    {
        public bool OnHit(SpatialItemId itemId, string userData, in Ray2D ray, ref float maxDistance, ref Dictionary<uint, string> context)
        {
            context[itemId.Value] = userData;
            return true;
        }
    }

    private readonly struct ShrinkRayVisitor : IRayHitVisitor2D<string, int>
    {
        public bool OnHit(SpatialItemId itemId, string userData, in Ray2D ray, ref float maxDistance, ref int context)
        {
            context++;
            maxDistance = 0.5f;
            return true;
        }
    }

    private static DynamicAabbTree2D<string, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink> CreateTree() =>
        new(new TreeOptions());

    private static DynamicAabbTree2D<string, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink> CreateChain(out SpatialItemId[] ids)
    {
        var tree = CreateTree();
        ids = new SpatialItemId[5];
        for (int i = 0; i < 5; i++)
        {
            ids[i] = tree.Insert(
                new Aabb2D(new Vec2(i, 0), new Vec2(i + 1, 1)),
                $"p{i}");
        }

        return tree;
    }

    private static Dictionary<uint, string> Truth(
        DynamicAabbTree2D<string, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink> tree,
        SpatialItemId[] ids)
    {
        var truth = new Dictionary<uint, string>();
        foreach (SpatialItemId id in ids)
        {
            truth[id.Value] = tree.GetUserData(id);
        }

        return truth;
    }

    [Fact]
    public void Insert_ReturnsDistinctIds_CountGrows()
    {
        using var tree = CreateChain(out SpatialItemId[] ids);

        ids.Should().OnlyHaveUniqueItems();
        tree.Count.Should().Be(5);
        tree.GetUserData(ids[2]).Should().Be("p2");
    }

    [Fact]
    public void GetFatAabb_CoversInsertedBox()
    {
        using var tree = CreateTree();
        var box = new Aabb2D(new Vec2(0, 0), new Vec2(1, 1));

        SpatialItemId id = tree.Insert(box, "payload");
        Aabb2D fat = tree.GetFatAabb(id);

        fat.Contains(in box).Should().BeTrue();
    }

    [Fact]
    public void Move_AbsorbsStationary_ReinsertsOnTravel()
    {
        using var tree = CreateTree();
        var box = new Aabb2D(new Vec2(0, 0), new Vec2(1, 1));
        SpatialItemId id = tree.Insert(box, "mover");

        tree.Move(id, box, Vec2.Zero).Should().BeFalse();
        tree.Move(id, new Aabb2D(new Vec2(0.05f, 0), new Vec2(1.05f, 1)), new Vec2(0.05f, 0)).Should().BeTrue();
        tree.Move(id, new Aabb2D(new Vec2(5, 5), new Vec2(6, 6)), new Vec2(5, 5)).Should().BeTrue();

        Span<SpatialItemId> results = stackalloc SpatialItemId[4];
        tree.QueryOverlaps(new Aabb2D(new Vec2(4, 4), new Vec2(7, 7)), results).Should().Be(1);
    }

    [Fact]
    public void Update_ReplacesBounds_InPlace()
    {
        using var tree = CreateTree();
        SpatialItemId id = tree.Insert(new Aabb2D(new Vec2(0, 0), new Vec2(1, 1)), "u");

        tree.Update(id, new Aabb2D(new Vec2(0.05f, 0), new Vec2(1.05f, 1)));

        tree.GetUserData(id).Should().Be("u");
        Span<SpatialItemId> results = stackalloc SpatialItemId[4];
        tree.QueryOverlaps(new Aabb2D(new Vec2(-1, -1), new Vec2(2, 2)), results).Should().Be(1);
    }

    [Fact]
    public void Remove_FreesSlot_InvalidIdThrows()
    {
        using var tree = CreateTree();
        SpatialItemId id = tree.Insert(new Aabb2D(new Vec2(0, 0), new Vec2(1, 1)), "gone");

        tree.Remove(id);
        tree.Count.Should().Be(0);

        Action read = () => tree.GetUserData(id);
        read.Should().Throw<InvalidOperationException>();

        Action again = () => tree.Remove(id);
        again.Should().Throw<InvalidOperationException>();

        SpatialItemId next = tree.Insert(new Aabb2D(new Vec2(2, 2), new Vec2(3, 3)), "back");
        tree.Count.Should().Be(1);
        tree.GetUserData(next).Should().Be("back");
    }

    [Fact]
    public void QueryOverlaps_Span_CollectsAndTruncates()
    {
        using var tree = CreateChain(out _);

        Span<SpatialItemId> wide = stackalloc SpatialItemId[8];
        tree.QueryOverlaps(new Aabb2D(new Vec2(-1, -1), new Vec2(6, 2)), wide).Should().Be(5);

        Span<SpatialItemId> narrow = stackalloc SpatialItemId[2];
        tree.QueryOverlaps(new Aabb2D(new Vec2(-1, -1), new Vec2(6, 2)), narrow).Should().Be(2);
    }

    [Fact]
    public void QueryOverlaps_Visitor_PayloadMatchesId()
    {
        using var tree = CreateChain(out SpatialItemId[] ids);
        var truth = Truth(tree, ids);

        var seen = new Dictionary<uint, string>();
        var visitor = new MapVisitor();
        int hits = tree.QueryOverlaps(
            new Aabb2D(new Vec2(-1, -1), new Vec2(6, 2)),
            ref visitor,
            ref seen);

        hits.Should().Be(5);
        seen.Should().BeEquivalentTo(truth);
    }

    [Fact]
    public void RayCast_Span_HitsAll()
    {
        using var tree = CreateChain(out _);

        var ray = new Ray2D(new Vec2(-1, 0.5f), new Vec2(1, 0));
        Span<RayHit> results = stackalloc RayHit[8];
        int count = tree.RayCast(in ray, results);

        count.Should().Be(5);
        float[] distances = new float[count];
        for (int i = 0; i < count; i++)
        {
            distances[i] = results[i].Distance;
        }

        Array.Sort(distances);
        distances[0].Should().BeApproximately(0.9f, 1e-4f);
        distances[4].Should().BeApproximately(4.9f, 1e-4f);
    }

    [Fact]
    public void RayCast_Visitor_PayloadMatchesId_AndShrinks()
    {
        using var tree = CreateChain(out SpatialItemId[] ids);
        var truth = Truth(tree, ids);

        var seen = new Dictionary<uint, string>();
        var visitor = new MapRayVisitor();
        var ray = new Ray2D(new Vec2(-1, 0.5f), new Vec2(1, 0));
        tree.RayCast(in ray, float.PositiveInfinity, ref visitor, ref seen);

        seen.Should().BeEquivalentTo(truth);

        int hits = 0;
        var shrinker = new ShrinkRayVisitor();
        tree.RayCast(in ray, float.PositiveInfinity, ref shrinker, ref hits);

        hits.Should().Be(1);
    }

    [Fact]
    public void FindPairs_ReportsNeighbors_WithPayloads()
    {
        using var tree = CreateChain(out SpatialItemId[] ids);
        var truth = Truth(tree, ids);

        var counter = new CountPairs();
        tree.FindPairs(ref counter, ref truth);

        counter.Count.Should().Be(4);
    }

    private struct CountPairs : IPairVisitor<string, Dictionary<uint, string>>
    {
        private int _count;

        public bool OnPair(SpatialItemId idA, string dataA, SpatialItemId idB, string dataB, ref Dictionary<uint, string> context)
        {
            dataA.Should().Be(context[idA.Value]);
            dataB.Should().Be(context[idB.Value]);
            _count++;
            return true;
        }

        public readonly int Count => _count;
    }

    [Fact]
    public void QueryKNearest_ReturnsAscending_WithPayloads()
    {
        using var tree = CreateChain(out _);

        KnnResult<string>[] results = new KnnResult<string>[5];
        int count = tree.QueryKNearest(new Vec2(0.5f, 0.5f), results);

        count.Should().Be(5);
        for (int i = 0; i < count; i++)
        {
            results[i].UserData.Should().Be(tree.GetUserData(results[i].ItemId));
            if (i > 0)
            {
                ((float)results[i].DistanceSq).Should().BeGreaterThanOrEqualTo((float)results[i - 1].DistanceSq);
            }
        }

        results[0].ItemId.Value.Should().Be(1u);
    }

    [Fact]
    public void Growth_BeyondInitialCapacity_KeepsAllItems()
    {
        using var tree = new DynamicAabbTree2D<string, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink>(
            new TreeOptions(0.1f, 2.0f, new TreeCapacity(16)));

        for (int i = 0; i < 40; i++)
        {
            tree.Insert(new Aabb2D(new Vec2(i * 3, 0), new Vec2(i * 3 + 1, 1)), $"g{i}");
        }

        tree.Count.Should().Be(40);
        tree.Capacity.Should().BeGreaterThan(16);

        Span<SpatialItemId> results = stackalloc SpatialItemId[48];
        tree.QueryOverlaps(new Aabb2D(new Vec2(-1, -1), new Vec2(200, 2)), results).Should().Be(40);
    }

    [Fact]
    public async Task Complete_RejectsMutations_DrainSettles()
    {
        var tree = CreateTree();
        tree.Insert(new Aabb2D(new Vec2(0, 0), new Vec2(1, 1)), "x");

        tree.Complete();

        Action insert = () => tree.Insert(new Aabb2D(new Vec2(2, 2), new Vec2(3, 3)), "y");
        insert.Should().Throw<InvalidOperationException>();

        await tree.DrainAsync();
        tree.Dispose();
    }

    [Fact]
    public void Dispose_PoisonsOperations()
    {
        var tree = CreateTree();
        tree.Insert(new Aabb2D(new Vec2(0, 0), new Vec2(1, 1)), "x");
        tree.Dispose();

        Action insert = () => tree.Insert(new Aabb2D(new Vec2(2, 2), new Vec2(3, 3)), "y");
        insert.Should().Throw<ObjectDisposedException>();
    }
}
