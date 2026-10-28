namespace Axrone.Raycast.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class RaycastEngine2DTests
{
    private static readonly Ray2D AlongPositiveX = new(new Vec2(-5f, 0f), Vec2.UnitX);

    [Fact]
    public void ClosestReturnsNearestShape()
    {
        using var engine = new RaycastEngine2D();
        RaycastShapeId near = engine.Register(RaycastShape2D.CreateCircle(new Vec2(0f, 0f), 1f));
        RaycastShapeId far = engine.Register(RaycastShape2D.CreateCircle(new Vec2(5f, 0f), 1f));

        var query = new RaycastQuery2D(AlongPositiveX, 20f);
        engine.Raycast(in query, out RaycastHit2D hit).Should().BeTrue();

        hit.Shape.Should().Be(near);
        hit.Shape.Should().NotBe(far);
        hit.Distance.Should().BeApproximately(4f, 1e-4f);
        engine.Statistics.TotalQueries.Should().Be(1);
        engine.Statistics.TotalHits.Should().Be(1);
    }

    [Fact]
    public void AllHitsCollectAndSortByDistance()
    {
        using var engine = new RaycastEngine2D();
        RaycastShapeId near = engine.Register(RaycastShape2D.CreateCircle(new Vec2(0f, 0f), 1f));
        RaycastShapeId far = engine.Register(RaycastShape2D.CreateCircle(new Vec2(5f, 0f), 1f));
        var query = new RaycastQuery2D(
            AlongPositiveX,
            20f,
            uint.MaxValue,
            RaycastFlags.AllHits | RaycastFlags.SortByDistance,
            8);

        Span<RaycastHit2D> results = stackalloc RaycastHit2D[8];
        int count = engine.Raycast(in query, results);

        count.Should().Be(2);
        results[0].Shape.Should().Be(near);
        results[0].Distance.Should().BeApproximately(4f, 1e-4f);
        results[1].Shape.Should().Be(far);
        results[1].Distance.Should().BeApproximately(9f, 1e-4f);
    }

    [Fact]
    public void MaxHitsCapsCollectedContacts()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(new Vec2(0f, 0f), 1f));
        _ = engine.Register(RaycastShape2D.CreateCircle(new Vec2(5f, 0f), 1f));
        var query = new RaycastQuery2D(
            AlongPositiveX,
            20f,
            uint.MaxValue,
            RaycastFlags.AllHits | RaycastFlags.SortByDistance,
            1);

        Span<RaycastHit2D> results = stackalloc RaycastHit2D[8];
        int count = engine.Raycast(in query, results);

        count.Should().Be(1);
        results[0].Distance.Should().BeApproximately(4f, 1e-4f);
    }

    [Fact]
    public void LayerMaskFiltersShapes()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(new Vec2(0f, 0f), 1f, (uint)RaycastLayer.Dynamic));

        var wrongLayer = new RaycastQuery2D(AlongPositiveX, 20f, (uint)RaycastLayer.Static);
        engine.Raycast(in wrongLayer, out _).Should().BeFalse();

        var rightLayer = new RaycastQuery2D(AlongPositiveX, 20f, (uint)RaycastLayer.Dynamic);
        engine.Raycast(in rightLayer, out RaycastHit2D hit).Should().BeTrue();
        hit.LayerMask.Should().Be((uint)RaycastLayer.Dynamic);
    }

    [Fact]
    public void IgnoreTriggersSkipsTriggerFlagAndLayer()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(new Vec2(0f, 0f), 1f, 1u, trigger: true));
        _ = engine.Register(
            RaycastShape2D.CreateCircle(new Vec2(5f, 0f), 1f, (uint)RaycastLayer.Trigger));

        var all = new RaycastQuery2D(
            AlongPositiveX,
            20f,
            uint.MaxValue,
            RaycastFlags.AllHits | RaycastFlags.SortByDistance,
            8);
        Span<RaycastHit2D> results = stackalloc RaycastHit2D[8];
        engine.Raycast(in all, results).Should().Be(2);

        var filtered = new RaycastQuery2D(
            AlongPositiveX,
            20f,
            uint.MaxValue,
            RaycastFlags.AllHits | RaycastFlags.IgnoreTriggers,
            8);
        engine.Raycast(in filtered, results).Should().Be(0);
    }

    [Fact]
    public void InactiveShapesNeedTheIncludeInactiveFlag()
    {
        using var engine = new RaycastEngine2D();
        RaycastShapeId id = engine.Register(RaycastShape2D.CreateCircle(new Vec2(0f, 0f), 1f));
        var query = new RaycastQuery2D(AlongPositiveX, 20f);

        engine.SetActive(id, false).Should().BeTrue();
        engine.SetActive(id, false).Should().BeFalse();
        engine.Raycast(in query, out _).Should().BeFalse();

        var inclusive = query with { Flags = query.Flags | RaycastFlags.IncludeInactive };
        engine.Raycast(in inclusive, out RaycastHit2D hit).Should().BeTrue();
        hit.Shape.Should().Be(id);

        engine.SetActive(id, true).Should().BeTrue();
        engine.Raycast(in query, out _).Should().BeTrue();
    }

    [Fact]
    public void IgnoreBackfacesRejectsExitsFromInside()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(Vec2.Zero, 2f));
        var inside = new Ray2D(Vec2.Zero, Vec2.UnitX);
        var query = new RaycastQuery2D(inside, 10f);

        engine.Raycast(in query, out RaycastHit2D exit).Should().BeTrue();
        exit.Normal.X.Should().BeApproximately(1f, 1e-4f);

        var culled = query with { Flags = query.Flags | RaycastFlags.IgnoreBackfaces };
        engine.Raycast(in culled, out _).Should().BeFalse();
    }

    [Fact]
    public void UnregisterRemovesShapeAndValidationRejectsBadIds()
    {
        using var engine = new RaycastEngine2D();
        var id = new RaycastShapeId(42);
        engine.Register(id, RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        engine.ShapeCount.Should().Be(1);
        engine.TryGetShape(id, out RaycastShape2D shape).Should().BeTrue();
        shape.Kind.Should().Be(RaycastShapeKind.Sphere);

        Action duplicate = () => engine.Register(id, RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        duplicate.Should().Throw<InvalidOperationException>();

        engine.Unregister(id).Should().BeTrue();
        engine.Unregister(id).Should().BeFalse();
        engine.TryGetShape(id, out _).Should().BeFalse();

        var query = new RaycastQuery2D(AlongPositiveX, 20f);
        engine.Raycast(in query, out _).Should().BeFalse();

        Action invalid = () => engine.Register(RaycastShapeId.Invalid, RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        invalid.Should().Throw<RaycastException>()
            .Which.Code.Should().Be(RaycastErrorCode.InvalidQuery);

        engine.Register(id, RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        engine.ShapeCount.Should().Be(1);
        engine.Raycast(in query, out _).Should().BeTrue();
    }

    [Fact]
    public void AnyReportsPresenceAndLayerFiltering()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(new Vec2(0f, 0f), 1f));

        var query = new RaycastQuery2D(AlongPositiveX, 20f);
        engine.RaycastAny(in query).Should().BeTrue();

        var wrongLayer = query with { LayerMask = (uint)RaycastLayer.Water };
        engine.RaycastAny(in wrongLayer).Should().BeFalse();

        var sideways = new RaycastQuery2D(new Ray2D(new Vec2(-5f, 0f), Vec2.UnitY), 20f);
        engine.RaycastAny(in sideways).Should().BeFalse();
    }

    [Fact]
    public void CacheServesHitsForOneFrameAndInvalidatesOnChange()
    {
        using var engine = new RaycastEngine2D { EnableCache = true };
        RaycastShapeId id = engine.Register(RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        var query = new RaycastQuery2D(AlongPositiveX, 20f);

        engine.Raycast(in query, out _).Should().BeTrue();
        engine.Raycast(in query, out RaycastHit2D cached).Should().BeTrue();
        cached.Distance.Should().BeApproximately(4f, 1e-4f);
        engine.Statistics.TotalQueries.Should().Be(1);
        engine.Statistics.CacheHits.Should().Be(1);

        engine.AdvanceFrame();
        engine.Raycast(in query, out _).Should().BeTrue();
        engine.Statistics.TotalQueries.Should().Be(2);

        engine.SetActive(id, false);
        engine.Raycast(in query, out _).Should().BeFalse();
        engine.Statistics.TotalQueries.Should().Be(3);
        engine.Statistics.CacheHits.Should().Be(1);
    }

    [Fact]
    public void BatchWritesOneSlotPerQuery()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(Vec2.Zero, 1f));

        RaycastQuery2D[] queries =
        [
            new RaycastQuery2D(AlongPositiveX, 20f),
            new RaycastQuery2D(new Ray2D(new Vec2(-5f, 0f), Vec2.UnitY), 20f),
        ];
        Span<RaycastHit2D> results = stackalloc RaycastHit2D[2];
        results.Clear();
        engine.RaycastBatch(queries, results);
        results[0].Shape.IsValid.Should().BeTrue();
        results[1].Shape.IsValid.Should().BeFalse();

        Span<bool> flags = stackalloc bool[2];
        engine.RaycastBatchAny(queries, flags);
        flags[0].Should().BeTrue();
        flags[1].Should().BeFalse();

        Action mismatch = () => engine.RaycastBatch(queries, new RaycastHit2D[1]);
        mismatch.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClearRemovesEveryShape()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        engine.Clear();
        engine.ShapeCount.Should().Be(0);

        var query = new RaycastQuery2D(AlongPositiveX, 20f);
        engine.Raycast(in query, out _).Should().BeFalse();
    }

    [Fact]
    public void ValidationRejectsInvalidQueries()
    {
        using var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(Vec2.Zero, 1f));

        var zeroDistance = new RaycastQuery2D(AlongPositiveX, 0f);
        Action badDistance = () => engine.Raycast(in zeroDistance, out _);
        badDistance.Should().Throw<RaycastException>()
            .Which.Code.Should().Be(RaycastErrorCode.InvalidQuery);

        var allHitsWithoutLimit = new RaycastQuery2D(
            AlongPositiveX,
            20f,
            uint.MaxValue,
            RaycastFlags.AllHits,
            0);
        Action badLimit = () => engine.Raycast(in allHitsWithoutLimit, out _);
        badLimit.Should().Throw<RaycastException>()
            .Which.Code.Should().Be(RaycastErrorCode.InvalidQuery);

        Action emptyResults = () => engine.Raycast(in zeroDistance, Array.Empty<RaycastHit2D>());
        emptyResults.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DisposeRejectsFurtherQueries()
    {
        var engine = new RaycastEngine2D();
        _ = engine.Register(RaycastShape2D.CreateCircle(Vec2.Zero, 1f));
        engine.Dispose();

        var query = new RaycastQuery2D(AlongPositiveX, 20f);
        Action act = () => engine.Raycast(in query, out _);
        act.Should().Throw<ObjectDisposedException>();
    }
}
