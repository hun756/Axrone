namespace Axrone.Physics2D.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class EdgeTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public EdgeTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private BodyId AddGround(Vec2 p0, Vec2 p1)
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = Vec2.Zero });
        _world.Shapes.AttachEdge(_world.Bodies, _world.Broadphase, ground, p0, p1);
        return ground;
    }

    private BodyId AddBall(Vec2 center, float radius = 0.5f)
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Position = center });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, body, new Vec2(0, 0), radius);
        return body;
    }

    [Fact]
    public void Ball_RestsOnEdge()
    {
        AddGround(new Vec2(-10, 0), new Vec2(10, 0));
        BodyId ball = AddBall(new Vec2(0, 2));

        for (int i = 0; i < 180; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(ball).Position;
        p.Y.Should().BeApproximately(0.5f, 0.06f);
        MathF.Abs(p.X).Should().BeLessThan(0.05f);
        _world.ContactCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Capsule_RestsOnEdge()
    {
        AddGround(new Vec2(-10, 0), new Vec2(10, 0));
        BodyId body = _world.Bodies.Create(new BodyDef { Position = new Vec2(0, 2) });
        _world.Shapes.AttachCapsule(_world.Bodies, _world.Broadphase, body,
            new Vec2(-0.5f, 0), new Vec2(0.5f, 0), 0.25f);

        for (int i = 0; i < 180; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(body).Position;
        p.Y.Should().BeApproximately(0.25f, 0.06f);
    }

    [Fact]
    public void Box_RestsOnEdge()
    {
        AddGround(new Vec2(-10, 0), new Vec2(10, 0));
        BodyId box = _world.Bodies.Create(new BodyDef { Position = new Vec2(0, 2) });
        _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, box,
            new Vec2[]
            {
                new(-0.5f, -0.5f),
                new(0.5f, -0.5f),
                new(0.5f, 0.5f),
                new(-0.5f, 0.5f),
            });

        for (int i = 0; i < 240; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(box).Position;
        p.Y.Should().BeApproximately(0.5f, 0.08f);
    }

    [Fact]
    public void ChainLoop_HoldsStack()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = Vec2.Zero });
        int edges = _world.Shapes.AttachChain(
            _world.Bodies, _world.Broadphase, ground,
            new Vec2[]
            {
                new(-10, 0),
                new(10, 0),
                new(10, -1),
                new(-10, -1),
            },
            loop: true);
        edges.Should().Be(4);

        BodyId ball = AddBall(new Vec2(0, 2));

        for (int i = 0; i < 180; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(ball).Position;
        p.Y.Should().BeApproximately(0.5f, 0.06f);
    }

    [Fact]
    public void Ray_HitsEdge()
    {
        AddGround(new Vec2(-10, 0), new Vec2(10, 0));

        var hits = new RaycastHit2D[4];
        int count = _world.Raycast(new Ray2D(new Vec2(0, 5), new Vec2(0, -1)), 10f, hits.AsSpan());

        count.Should().Be(1);
        hits[0].Distance.Should().BeApproximately(5f, 0.001f);
        hits[0].Normal.Y.Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public void CircleEdge_Separated_ReturnsFalse()
    {
        bool hit = Narrowphase2D.EvaluateCircleEdge(
            new Circle2(new Vec2(0, 5), 0.5f), new Edge2D(new Vec2(-10, 0), new Vec2(10, 0)),
            out ContactManifold manifold);

        hit.Should().BeFalse();
        manifold.Should().Be(ContactManifold.Empty);
    }

    [Fact]
    public void Edge_RejectsDegenerate()
    {
        Action zero = () => _ = new Edge2D(new Vec2(1, 1), new Vec2(1, 1));
        zero.Should().Throw<ArgumentException>();
    }
}
