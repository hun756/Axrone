namespace Axrone.Geometry.Tests;

using Axrone.Geometry;
using Axrone.Numeric;

using FluentAssertions;
using Xunit;

public class OctreeTests
{
    private static readonly Aabb3D s_root = new(new Vec3(-16f), new Vec3(16f));

    private static Aabb3D Box(float min, float size) =>
        new(new Vec3(min), new Vec3(min + size));

    private static Aabb3D BoxAt(float x, float y, float z, float size) =>
        new(new Vec3(x, y, z), new Vec3(x + size, y + size, z + size));

    private static NativeOctree<int> CreateTree() => new(s_root);

    [Fact]
    public void Insert_QueryBox_FindsOverlaps()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 1), 1);
        tree.Insert(Box(5, 1), 2);
        tree.Insert(Box(-10, 1), 3);

        Span<SpatialItem<int>> buffer = stackalloc SpatialItem<int>[8];
        var visitor = new SpanCollectorVisitor<int>(buffer);
        bool completed = tree.QueryBox(BoxAt(0, 0, 0, 7), ref visitor);

        completed.Should().BeTrue();
        visitor.Count.Should().Be(2);
        tree.Count.Should().Be(3u);
    }

    [Fact]
    public void Insert_OutsideDomain_Throws_And_TryReports()
    {
        using var tree = CreateTree();

        Action insert = () => tree.Insert(Box(100, 1), 1);
        insert.Should().Throw<ArgumentOutOfRangeException>();

        tree.TryInsert(Box(100, 1), 1).Outcome.Should().Be(InsertOutcome.OutOfBounds);
    }

    [Fact]
    public void TryInsert_InvalidDomain_Reports()
    {
        using var tree = CreateTree();
        var inverted = new Aabb3D(new Vec3(2), new Vec3(1));

        tree.TryInsert(inverted, 1).Outcome.Should().Be(InsertOutcome.InvalidDomain);
    }

    [Fact]
    public void Ctor_RejectsBadDomainAndCapacities()
    {
        Action degenerate = () => _ = new NativeOctree<int>(new Aabb3D(new Vec3(1), new Vec3(1)));
        Action nan = () => _ = new NativeOctree<int>(new Aabb3D(new Vec3(float.NaN, 0, 0), new Vec3(1)));
        Action nodes = () => _ = new NativeOctree<int>(s_root, new NodeCapacity(4), new ItemCapacity(16));
        Action items = () => _ = new NativeOctree<int>(s_root, new NodeCapacity(8), new ItemCapacity(8));

        degenerate.Should().Throw<ArgumentException>();
        nan.Should().Throw<ArgumentException>();
        nodes.Should().Throw<ArgumentOutOfRangeException>();
        items.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Remove_RecyclesSlot_And_StaleHandleFails()
    {
        using var tree = CreateTree();
        OctreeHandle handle = tree.Insert(Box(0, 1), 1);

        tree.Remove(handle).Should().BeTrue();
        tree.Remove(handle).Should().BeFalse();

        OctreeHandle next = tree.Insert(Box(5, 1), 2);
        next.Should().NotBe(handle);
        tree.Remove(handle).Should().BeFalse();

        Span<SpatialItem<int>> buffer = stackalloc SpatialItem<int>[8];
        var visitor = new SpanCollectorVisitor<int>(buffer);
        tree.QueryBox(s_root, ref visitor);
        visitor.Count.Should().Be(1);
        buffer[0].Value.Should().Be(2);
    }

    [Fact]
    public void Update_InPlace_WhenCovered()
    {
        using var tree = CreateTree();
        OctreeHandle handle = tree.Insert(Box(0, 1), 1);

        tree.Update(handle, Box(0.5f, 0.4f), 7).Should().BeTrue();

        Span<SpatialItem<int>> buffer = stackalloc SpatialItem<int>[8];
        var visitor = new SpanCollectorVisitor<int>(buffer);
        tree.QueryBox(BoxAt(0.4f, 0.4f, 0.4f, 1.2f), ref visitor);
        visitor.Count.Should().Be(1);
        buffer[0].Value.Should().Be(7);
    }

    [Fact]
    public void Update_Relocates_AcrossSplitTree()
    {
        using var tree = CreateTree();
        var handles = new OctreeHandle[17];
        for (int i = 0; i < handles.Length; i++)
        {
            float x = (i % 5) * 3f - 6f;
            handles[i] = tree.Insert(BoxAt(x, 0, 0, 1), i);
        }

        tree.Update(handles[0], Box(12, 1), 99).Should().BeTrue();
        tree.Count.Should().Be(17u);

        Span<SpatialItem<int>> buffer = stackalloc SpatialItem<int>[4];
        var visitor = new SpanCollectorVisitor<int>(buffer);
        tree.QueryBox(BoxAt(11.5f, 11.5f, 11.5f, 2), ref visitor);
        visitor.Count.Should().Be(1);
        buffer[0].Value.Should().Be(99);
    }

    [Fact]
    public void Update_RejectsBadInput()
    {
        using var tree = CreateTree();
        OctreeHandle handle = tree.Insert(Box(0, 1), 1);

        tree.Update(handle, Box(100, 1), 2).Should().BeFalse();
        tree.Update(OctreeHandle.Invalid, Box(0, 1), 2).Should().BeFalse();
        tree.Update(handle, Box(0, 1), 2).Should().BeTrue();
    }

    [Fact]
    public void QuerySphere_FindsOverlaps()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 1), 1);
        tree.Insert(Box(10, 1), 2);

        var counter = new CounterVisitor<int>();
        bool completed = tree.QuerySphere(new BoundingSphere3(new Vec3(0.5f), 1f), ref counter);

        completed.Should().BeTrue();
        counter.Count.Should().Be(1);
    }

    [Fact]
    public void Raycast_ReportsNearest()
    {
        using var tree = CreateTree();
        tree.Insert(new Aabb3D(new Vec3(5, -1, -1), new Vec3(6, 1, 1)), 1);
        tree.Insert(new Aabb3D(new Vec3(2, -1, -1), new Vec3(3, 1, 1)), 2);

        var visitor = new NearestHitVisitor<int>();
        var ray = new Ray3D(new Vec3(0, 0, 0), new Vec3(1, 0, 0));
        bool completed = tree.Raycast(ray, Distance.PositiveInfinity, ref visitor);

        completed.Should().BeTrue();
        visitor.HasHit.Should().BeTrue();
        visitor.NearestItem.Value.Should().Be(2);
        visitor.Distance.Value.Should().BeApproximately(2f, 1e-4f);
    }

    [Fact]
    public void Raycast_MaxDistance_Prunes()
    {
        using var tree = CreateTree();
        tree.Insert(new Aabb3D(new Vec3(5, -1, -1), new Vec3(6, 1, 1)), 1);

        var visitor = new NearestHitVisitor<int>();
        var ray = new Ray3D(new Vec3(0, 0, 0), new Vec3(1, 0, 0));
        tree.Raycast(ray, new Distance(1f), ref visitor);

        visitor.HasHit.Should().BeFalse();
    }

    [Fact]
    public void QueryBox_EarlyOut_Stops()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 1), 1);
        tree.Insert(Box(2, 1), 2);

        Span<SpatialItem<int>> buffer = stackalloc SpatialItem<int>[1];
        var visitor = new SpanCollectorVisitor<int>(buffer);
        bool completed = tree.QueryBox(s_root, ref visitor);

        completed.Should().BeFalse();
        visitor.Count.Should().Be(1);
    }

    [Fact]
    public void FindNearest_ReturnsClosest()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 1), 1);
        tree.Insert(Box(10, 1), 2);

        NearestNeighborResult<int> result = tree.FindNearest(new Vec3(0.5f), Distance.PositiveInfinity);

        result.HasFound.Should().BeTrue();
        result.Item.Value.Should().Be(1);
        result.DistanceSquared.Should().BeApproximately(0f, 1e-5f);
    }

    [Fact]
    public void FindNearest_RespectsLimit_And_Empty()
    {
        using var tree = CreateTree();
        tree.Insert(Box(10, 1), 2);

        tree.FindNearest(new Vec3(0, 0, 0), new Distance(5f)).HasFound.Should().BeFalse();

        using var empty = CreateTree();
        empty.FindNearest(new Vec3(0, 0, 0), Distance.PositiveInfinity).HasFound.Should().BeFalse();
    }

    [Fact]
    public void Split_And_Growth_KeepAllItems()
    {
        using var tree = new NativeOctree<int>(s_root, new NodeCapacity(8), new ItemCapacity(16));

        for (int i = 0; i < 40; i++)
        {
            float x = (i % 8) * 3.5f - 14f;
            float y = (i / 8) * 3.5f - 7f;
            tree.TryInsert(BoxAt(x, y, 0, 1), i).IsSuccess.Should().BeTrue();
        }

        tree.Count.Should().Be(40u);

        Span<SpatialItem<int>> buffer = stackalloc SpatialItem<int>[48];
        var visitor = new SpanCollectorVisitor<int>(buffer);
        tree.QueryBox(s_root, ref visitor);
        visitor.Count.Should().Be(40);
    }

    [Fact]
    public void Prune_CollapsesEmptiedBranches()
    {
        using var tree = CreateTree();
        var handles = new OctreeHandle[20];
        for (int i = 0; i < handles.Length; i++)
        {
            handles[i] = tree.Insert(BoxAt(0.1f * i, 0.1f * i, 0.1f * i, 0.05f), i);
        }

        foreach (OctreeHandle handle in handles)
        {
            tree.Remove(handle).Should().BeTrue();
        }

        tree.Count.Should().Be(0u);
        tree.Insert(Box(0, 1), 42).Should().NotBe(OctreeHandle.Invalid);
        tree.Count.Should().Be(1u);
    }

    [Fact]
    public void Clear_DropsEverything()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 1), 1);
        tree.Insert(Box(2, 1), 2);

        tree.Clear();

        tree.Count.Should().Be(0u);
        var counter = new CounterVisitor<int>();
        tree.QueryBox(s_root, ref counter);
        counter.Count.Should().Be(0);
    }

    [Fact]
    public void EnumerateBox_WalksItems()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 1), 1);
        tree.Insert(Box(5, 1), 2);
        tree.Insert(Box(-10, 1), 3);

        var seen = new List<int>();
        foreach (SpatialItem<int> item in tree.EnumerateBox(BoxAt(0, -1, -1, 8)))
        {
            seen.Add(item.Value);
        }

        seen.OrderBy(v => v).ToArray().Should().Equal(1, 2);
    }

    [Fact]
    public void Builder_BuildsTrees()
    {
        using var simple = OctreeBuilder.Create().WithDomain(s_root).Build<int>();
        simple.Insert(Box(0, 1), 1).Should().NotBe(OctreeHandle.Invalid);

        using var full = OctreeBuilder.Create()
            .WithDomain(s_root)
            .WithCapacities(new NodeCapacity(64), new ItemCapacity(64))
            .Build<int, DefaultPartitionPolicy, NullOctreeMetrics>();
        full.Insert(Box(0, 1), 2).Should().NotBe(OctreeHandle.Invalid);

        Action domain = () => OctreeBuilder.Create().WithDomain(new Aabb3D(new Vec3(1), new Vec3(1)));
        Action caps = () => OctreeBuilder.Create().WithDomain(s_root).WithCapacities(new NodeCapacity(4), new ItemCapacity(64));
        domain.Should().Throw<ArgumentException>();
        caps.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Facade_DefaultDomain_Works()
    {
        using var tree = new NativeOctree<int>();

        tree.RootBounds.Min.X.Should().Be(-1024f);
        tree.Insert(Box(0, 1), 1);
        tree.Count.Should().Be(1u);
    }

    [Fact]
    public void Complete_And_Dispose_Poison()
    {
        using var tree = CreateTree();
        tree.Insert(Box(0, 1), 1);
        tree.Complete();

        Action insert = () => tree.Insert(Box(2, 1), 2);
        insert.Should().Throw<ObjectDisposedException>();
        tree.TryInsert(Box(2, 1), 2).Outcome.Should().Be(InsertOutcome.Disposed);
    }

    [Fact]
    public async Task DrainAsync_SettlesAndDisposes()
    {
        var tree = CreateTree();
        tree.Insert(Box(0, 1), 1);

        await tree.DrainAsync();

        Action insert = () => tree.Insert(Box(2, 1), 2);
        insert.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var tree = CreateTree();
        tree.Dispose();

        Action again = () => tree.Dispose();
        again.Should().NotThrow();
    }
}
