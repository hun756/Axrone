namespace Axrone.Physics2D.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class Solver2DTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public Solver2DTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private BodyId AddBox(Vec2 center, Vec2 halfExtents, BodyType type = BodyType.Dynamic, PhysicsMaterial material = default)
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Type = type, Position = center });
        _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, body,
            new Vec2[]
            {
                new(-halfExtents.X, -halfExtents.Y),
                new(halfExtents.X, -halfExtents.Y),
                new(halfExtents.X, halfExtents.Y),
                new(-halfExtents.X, halfExtents.Y),
            },
            material: material.Equals(default) ? PhysicsMaterial.Default : material);
        return body;
    }

    [Fact]
    public void StackedBox_ComesToRest_OnGround()
    {
        AddBox(new Vec2(0, -1), new Vec2(10, 1), BodyType.Static);
        BodyId box = AddBox(new Vec2(0, 0.4f), new Vec2(0.5f, 0.5f));

        for (int i = 0; i < 180; i++)
        {
            _world.Step(Dt);
        }

        float bottom = _world.Bodies.GetTransform(box).Position.Y - 0.5f;
        bottom.Should().BeGreaterThan(-0.06f);
        bottom.Should().BeLessThan(0.05f);
        MathF.Abs(_world.Bodies.GetLinearVelocity(box).Y).Should().BeLessThan(0.6f);
        _world.ContactCount.Should().Be(1);
    }

    [Fact]
    public void BouncyCircle_Rebounds_OffGround()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, -1) });
        _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, ground,
            new Vec2[] { new(-10, -1), new(10, -1), new(10, 1), new(-10, 1) },
            material: new PhysicsMaterial(0f, 1f));

        BodyId ball = _world.Bodies.Create(new BodyDef { Position = new Vec2(0, 0.4f), LinearVelocity = new Vec2(0, -3) });
        _world.Shapes.AttachCircle(
            _world.Bodies, _world.Broadphase, ball, new Vec2(0, 0), 0.5f,
            material: new PhysicsMaterial(0f, 1f));

        for (int i = 0; i < 5; i++)
        {
            _world.Step(Dt);
        }

        _world.Bodies.GetLinearVelocity(ball).Y.Should().BeGreaterThan(1f);
    }

    [Fact]
    public void SlidingBox_FrictionBleedsVelocity()
    {
        var slick = new PhysicsMaterial(0.6f, 0f);
        AddBox(new Vec2(0, -1), new Vec2(10, 1), BodyType.Static, slick);
        BodyId box = AddBox(new Vec2(0, 0.45f), new Vec2(0.5f, 0.5f), BodyType.Dynamic, slick);
        _world.Bodies.SetLinearVelocity(box, new Vec2(5, 0));

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        MathF.Abs(_world.Bodies.GetLinearVelocity(box).X).Should().BeLessThan(1f);
    }

    [Fact]
    public void ZeroIterations_NoResolution()
    {
        var bare = new World(new WorldDef { VelocityIterations = 0, PositionIterations = 0 });
        try
        {
            BodyId ground = bare.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, -1) });
            bare.Shapes.AttachPolygon(
                bare.Bodies, bare.Broadphase, ground,
                new Vec2[] { new(-10, -1), new(10, -1), new(10, 1), new(-10, 1) });
            BodyId box = bare.Bodies.Create(new BodyDef { Position = new Vec2(0, 0.4f) });
            bare.Shapes.AttachPolygon(
                bare.Bodies, bare.Broadphase, box,
                new Vec2[] { new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f) });

            for (int i = 0; i < 60; i++)
            {
                bare.Step(Dt);
            }

            bare.Bodies.GetTransform(box).Position.Y.Should().BeLessThan(-4.5f);
        }
        finally
        {
            bare.Dispose();
        }
    }

    [Fact]
    public void NonDynamicBodies_ReadAsInfiniteMass()
    {
        var bodies = new BodyStore();
        var shapes = new ShapeStore();
        using var tree = new DynamicAabbTree2D<ShapeId, SurfaceAreaHeuristicStrategy2D, NullSpatialMetricsSink>(new TreeOptions());

        BodyId @static = bodies.Create(new BodyDef { Type = BodyType.Static });
        shapes.AttachCircle(bodies, tree, @static, new Vec2(0, 0), 1f);
        bodies.GetMass(@static).Should().BeGreaterThan(0f);
        bodies.GetInverseMass(@static).Should().Be(0f);
        bodies.GetInverseInertia(@static).Should().Be(0f);

        BodyId kinematic = bodies.Create(new BodyDef { Type = BodyType.Kinematic });
        shapes.AttachCircle(bodies, tree, kinematic, new Vec2(0, 0), 1f);
        bodies.GetInverseMass(kinematic).Should().Be(0f);
        bodies.GetInverseInertia(kinematic).Should().Be(0f);

        bodies.SetMassData(@static, 5f, 2f);
        bodies.GetInverseMass(@static).Should().Be(0f);
    }

    [Fact]
    public void WorldDef_NegativeIterations_Throw()
    {
        Action velocity = () => _ = new WorldDef { VelocityIterations = -1 };
        Action position = () => _ = new WorldDef { PositionIterations = -2 };

        velocity.Should().Throw<InvalidOperationException>();
        position.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TwoBoxStack_StaysUp()
    {
        AddBox(new Vec2(0, -1), new Vec2(10, 1), BodyType.Static);
        AddBox(new Vec2(0, 0.4f), new Vec2(0.5f, 0.5f));
        BodyId top = AddBox(new Vec2(0, 1.35f), new Vec2(0.5f, 0.5f));

        for (int i = 0; i < 240; i++)
        {
            _world.Step(Dt);
        }

        float topY = _world.Bodies.GetTransform(top).Position.Y;
        topY.Should().BeGreaterThan(1.2f);
        topY.Should().BeLessThan(1.7f);
    }
}
