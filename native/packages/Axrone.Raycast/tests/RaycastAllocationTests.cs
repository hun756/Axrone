namespace Axrone.Raycast.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class RaycastAllocationTests
{
    [Fact]
    public void Nearest2DQueryDoesNotAllocate()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        var query = new RaycastQuery2D(new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX), 20f);

        var hits = 0;
        for (var index = 0; index < 1000; index++)
        {
            hits += engine.Raycast(in query, out _) ? 1 : 0;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            hits += engine.Raycast(in query, out _) ? 1 : 0;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        hits.Should().Be(2000);
    }

    [Fact]
    public void AllHitsAndAny2DDoNotAllocate()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        _ = engine.Register(RaycastShape2D.CreateCircle(new Vec2(5f, 0f), 1f));
        var query = new RaycastQuery2D(
            new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX),
            20f,
            uint.MaxValue,
            RaycastFlags.AllHits | RaycastFlags.SortByDistance,
            4);
        Span<RaycastHit2D> results = stackalloc RaycastHit2D[4];

        var count = 0;
        for (var index = 0; index < 1000; index++)
        {
            count = engine.Raycast(in query, results);
            _ = engine.RaycastAny(in query);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            count = engine.Raycast(in query, results);
            _ = engine.RaycastAny(in query);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        count.Should().Be(2);
    }

    [Fact]
    public void NearestAndAllHits3DDoNotAllocate()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        _ = engine.Register(RaycastShape3D.CreateSphere(new Vec3(5f, 0f, 0f), 1f));
        var query = new RaycastQuery3D(new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX), 30f);
        var allQuery = query with
        {
            Flags = RaycastFlags.AllHits | RaycastFlags.SortByDistance,
            MaxHits = 4,
        };
        Span<RaycastHit3D> results = stackalloc RaycastHit3D[4];

        var hits = 0;
        var count = 0;
        for (var index = 0; index < 1000; index++)
        {
            hits += engine.Raycast(in query, out _) ? 1 : 0;
            count = engine.Raycast(in allQuery, results);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            hits += engine.Raycast(in query, out _) ? 1 : 0;
            count = engine.Raycast(in allQuery, results);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        hits.Should().Be(2000);
        count.Should().Be(2);
    }

    [Fact]
    public void ShapeCastAndSphereSweepDoNotAllocate()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        var ray = new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX);
        RaycastShape3D volume = RaycastShape3D.CreateSphere(new Vec3(-5f, 0f, 0f), 1f);
        var sweep = new RaycastSweepQuery3D(new Vec3(-5f, 0f, 0f), new Vec3(5f, 0f, 0f));
        var raycaster = new ContinuousRaycaster3D(engine);

        var hits = 0;
        for (var index = 0; index < 200; index++)
        {
            hits += engine.ShapeCast(in ray, in volume, 30f, out _) ? 1 : 0;
            hits += raycaster.SweepSphere(in sweep, 1f).Hit ? 1 : 0;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 200; index++)
        {
            hits += engine.ShapeCast(in ray, in volume, 30f, out _) ? 1 : 0;
            hits += raycaster.SweepSphere(in sweep, 1f).Hit ? 1 : 0;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        hits.Should().Be(800);
    }

    [Fact]
    public void PointSweepDoesNotAllocate()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        var raycaster = new ContinuousRaycaster3D(engine);
        var query = new RaycastSweepQuery3D(new Vec3(-5f, 0f, 0f), new Vec3(5f, 0f, 0f));

        var hits = 0;
        for (var index = 0; index < 100; index++)
        {
            hits += raycaster.SweepTest(in query).Hit ? 1 : 0;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
        {
            hits += raycaster.SweepTest(in query).Hit ? 1 : 0;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        hits.Should().Be(200);
    }

    [Fact]
    public void BatchQueriesDoNotAllocate()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        RaycastQuery2D[] queries =
        [
            new RaycastQuery2D(new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX), 20f),
            new RaycastQuery2D(new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX), 3f),
        ];
        Span<RaycastHit2D> results = stackalloc RaycastHit2D[2];
        Span<bool> flags = stackalloc bool[2];

        for (var index = 0; index < 1000; index++)
        {
            engine.RaycastBatch(queries, results);
            engine.RaycastBatchAny(queries, flags);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            engine.RaycastBatch(queries, results);
            engine.RaycastBatchAny(queries, flags);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
    }

    [Fact]
    public void GridDdaAndCacheLookupsDoNotAllocate()
    {
        var grid = new SpatialHashGrid2D<int>(1f);
        var bounds = new Aabb2D(5f, 0f, 5.5f, 0.5f);
        grid.Insert(1, in bounds);
        var ray = new Ray2D(new Vec2(0f, 0.25f), Vec2.UnitX);

        var cache = new RaycastCache2D(8);
        var query = new RaycastQuery2D(new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX), 20f);
        var hit = new RaycastHit2D(new RaycastShapeId(1), new Vec2(-1f, 0f), new Vec2(-1f, 0f), 4f, 0.2f, 1u);
        cache.Set(in query, in hit);

        var count = 0;
        for (var index = 0; index < 1000; index++)
        {
            var visitor = new CountingVisitor();
            grid.Raycast(in ray, 10f, ref visitor);
            count += visitor.Count;
            count += cache.TryGet(in query, out _) ? 1 : 0;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            var visitor = new CountingVisitor();
            grid.Raycast(in ray, 10f, ref visitor);
            count += visitor.Count;
            count += cache.TryGet(in query, out _) ? 1 : 0;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        count.Should().Be(4000);
    }

    private ref struct CountingVisitor : ISpatialHashItemVisitor<int>
    {
        public int Count;

        public bool OnItem(int item)
        {
            Count++;
            return true;
        }
    }
}
