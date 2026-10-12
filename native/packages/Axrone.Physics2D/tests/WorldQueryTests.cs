namespace Axrone.Physics2D.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class WorldQueryTests : IDisposable
{
    private const float Tolerance = 1e-4f;

    private readonly World _world;

    public WorldQueryTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private ShapeId AddStaticCircle(Vec2 center, float radius, bool isSensor = false)
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = center });
        return _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, body, new Vec2(0, 0), radius, isSensor: isSensor);
    }

    [Fact]
    public void Raycast_HitsCircle()
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(5, 0) });
        ShapeId shape = _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, body, new Vec2(0, 0), 1f);

        var ray = new Ray2D(new Vec2(0, 0), new Vec2(1, 0));
        var buffer = new RaycastHit2D[8];

        int count = _world.Raycast(ray, 10f, buffer);

        count.Should().Be(1);
        buffer[0].Shape.Should().Be(shape);
        buffer[0].Body.Should().Be(body);
        buffer[0].Distance.Should().BeApproximately(4f, Tolerance);
        buffer[0].Point.X.Should().BeApproximately(4f, Tolerance);
        buffer[0].Point.Y.Should().BeApproximately(0f, Tolerance);
        buffer[0].Normal.Should().Be(new Vec2(-1, 0));
    }

    [Fact]
    public void Raycast_HitsCapsuleCap()
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, 0) });
        ShapeId shape = _world.Shapes.AttachCapsule(_world.Bodies, _world.Broadphase, body, new Vec2(4, 0), new Vec2(6, 0), 1f);

        var ray = new Ray2D(new Vec2(0, 0), new Vec2(1, 0));
        var buffer = new RaycastHit2D[8];

        int count = _world.Raycast(ray, 10f, buffer);

        count.Should().Be(1);
        buffer[0].Shape.Should().Be(shape);
        buffer[0].Distance.Should().BeApproximately(3f, Tolerance);
        buffer[0].Point.X.Should().BeApproximately(3f, Tolerance);
        buffer[0].Point.Y.Should().BeApproximately(0f, Tolerance);
        buffer[0].Normal.Should().Be(new Vec2(-1, 0));
    }

    [Fact]
    public void Raycast_HitsPolygon()
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(5, 0) });
        ShapeId shape = _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, body,
            new Vec2[] { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) });

        var ray = new Ray2D(new Vec2(0, 0), new Vec2(1, 0));
        var buffer = new RaycastHit2D[8];

        int count = _world.Raycast(ray, 10f, buffer);

        count.Should().Be(1);
        buffer[0].Shape.Should().Be(shape);
        buffer[0].Distance.Should().BeApproximately(4f, Tolerance);
        buffer[0].Point.X.Should().BeApproximately(4f, Tolerance);
        buffer[0].Point.Y.Should().BeApproximately(0f, Tolerance);
        buffer[0].Normal.Should().Be(new Vec2(-1, 0));
    }

    [Fact]
    public void Raycast_Miss_ReturnsZero()
    {
        AddStaticCircle(new Vec2(5, 0), 1f);

        var sideways = new Ray2D(new Vec2(0, 0), new Vec2(0, 1));
        var forward = new Ray2D(new Vec2(0, 0), new Vec2(1, 0));
        var buffer = new RaycastHit2D[8];

        _world.Raycast(sideways, 10f, buffer).Should().Be(0);
        _world.Raycast(forward, 2f, buffer).Should().Be(0);
    }

    [Fact]
    public void Raycast_ReportsSensors_And_SortsByDistance()
    {
        ShapeId sensor = AddStaticCircle(new Vec2(5, 0), 1f, isSensor: true);
        ShapeId solid = AddStaticCircle(new Vec2(8, 0), 1f);

        var ray = new Ray2D(new Vec2(0, 0), new Vec2(1, 0));
        var buffer = new RaycastHit2D[8];

        int count = _world.Raycast(ray, 20f, buffer);

        count.Should().Be(2);
        buffer[0].Shape.Should().Be(sensor);
        buffer[0].Distance.Should().BeApproximately(4f, Tolerance);
        buffer[1].Shape.Should().Be(solid);
        buffer[1].Distance.Should().BeApproximately(7f, Tolerance);
    }

    [Fact]
    public void Raycast_EmptySpan_ReturnsZero()
    {
        AddStaticCircle(new Vec2(5, 0), 1f);

        var ray = new Ray2D(new Vec2(0, 0), new Vec2(1, 0));

        _world.Raycast(ray, 10f, Span<RaycastHit2D>.Empty).Should().Be(0);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Raycast_InvalidDistance_Throws(float maxDistance)
    {
        AddStaticCircle(new Vec2(5, 0), 1f);

        var ray = new Ray2D(new Vec2(0, 0), new Vec2(1, 0));
        var buffer = new RaycastHit2D[4];

        Action act = () => _world.Raycast(ray, maxDistance, buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OverlapAabb_FindsShapes_InIdOrder()
    {
        ShapeId first = AddStaticCircle(new Vec2(0, 0), 1f);
        ShapeId second = AddStaticCircle(new Vec2(3, 0), 1f);

        var buffer = new ShapeId[8];

        int near = _world.OverlapAabb(new Aabb2D(new Vec2(-2, -2), new Vec2(1, 2)), buffer);
        near.Should().Be(1);
        buffer[0].Should().Be(first);

        int both = _world.OverlapAabb(new Aabb2D(new Vec2(-2, -2), new Vec2(5, 2)), buffer);
        both.Should().Be(2);
        buffer[0].Should().Be(first);
        buffer[1].Should().Be(second);

        int far = _world.OverlapAabb(new Aabb2D(new Vec2(50, 50), new Vec2(60, 60)), buffer);
        far.Should().Be(0);

        _world.OverlapAabb(new Aabb2D(new Vec2(-2, -2), new Vec2(5, 2)), Span<ShapeId>.Empty).Should().Be(0);
    }
}
