namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class Ccd2DTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public Ccd2DTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private BodyId AddWall(float wallMinX, float thickness)
    {
        BodyId wall = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(wallMinX + (thickness / 2f), 0) });
        _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, wall,
            new Vec2[]
            {
                new(-thickness / 2f, -5f),
                new(thickness / 2f, -5f),
                new(thickness / 2f, 5f),
                new(-thickness / 2f, 5f),
            });
        return wall;
    }

    private BodyId AddBall(Vec2 center, Vec2 velocity, bool bullet)
    {
        BodyId ball = _world.Bodies.Create(new BodyDef
        {
            Position = center,
            LinearVelocity = velocity,
            Bullet = bullet,
        });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, ball, new Vec2(0, 0), 0.5f);
        return ball;
    }

    [Fact]
    public void Bullet_Stops_AtThinWall()
    {
        AddWall(10f, 0.2f);
        BodyId ball = AddBall(new Vec2(0.9f, 0), new Vec2(120, 0), bullet: true);

        for (int i = 0; i < 30; i++)
        {
            _world.Step(Dt);
        }

        float x = _world.Bodies.GetTransform(ball).Position.X;
        x.Should().BeLessThan(10f + 0.5f + 0.3f);
        _world.ContactCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NonBullet_Tunnels_ThroughThinWall()
    {
        AddWall(10f, 0.2f);
        BodyId ball = AddBall(new Vec2(0.9f, 0), new Vec2(120, 0), bullet: false);

        for (int i = 0; i < 30; i++)
        {
            _world.Step(Dt);
        }

        float x = _world.Bodies.GetTransform(ball).Position.X;
        x.Should().BeGreaterThan(10.2f);
    }

    [Fact]
    public void SlowNonBullet_DoesNotTunnel()
    {
        AddWall(10f, 0.2f);
        BodyId ball = AddBall(new Vec2(8, 0), new Vec2(2, 0), bullet: false);

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        float x = _world.Bodies.GetTransform(ball).Position.X;
        x.Should().BeLessThan(10f + 0.5f + 0.3f);
    }

    [Fact]
    public void FastestBullet_RespectsSubstepCap()
    {
        AddWall(10f, 0.2f);
        BodyId ball = AddBall(new Vec2(0, 0), new Vec2(120, 0), bullet: true);

        for (int i = 0; i < 30; i++)
        {
            _world.Step(Dt);
        }

        float x = _world.Bodies.GetTransform(ball).Position.X;
        x.Should().BeLessThan(10f + 0.5f + 0.3f);
    }

    [Fact]
    public void WorldDef_BadCcdTuning_Throws()
    {
        Action sweep = () => _ = new WorldDef { CcdSweepStep = 0f };
        Action cap = () => _ = new WorldDef { MaxCcdSubsteps = 0 };

        sweep.Should().Throw<InvalidOperationException>();
        cap.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HeadOnBullets_MeetInsteadOfPassingThrough()
    {
        BodyId left = _world.Bodies.Create(new BodyDef
        {
            Position = new Vec2(-0.1f, 0),
            LinearVelocity = new Vec2(12, 0),
            Bullet = true,
        });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, left, new Vec2(0, 0), 0.05f);

        BodyId right = _world.Bodies.Create(new BodyDef
        {
            Position = new Vec2(0.1f, 0),
            LinearVelocity = new Vec2(-12, 0),
            Bullet = true,
        });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, right, new Vec2(0, 0), 0.05f);

        _world.Step(Dt);

        _world.ContactCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FastSpin_DoesNotExplode()
    {
        BodyId spinner = _world.Bodies.Create(new BodyDef
        {
            Position = new Vec2(0, 2),
            LinearVelocity = new Vec2(1, 0),
            AngularVelocity = 20f,
            Bullet = true,
        });
        _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, spinner,
            new Vec2[]
            {
                new(-2f, -0.1f),
                new(2f, -0.1f),
                new(2f, 0.1f),
                new(-2f, 0.1f),
            });

        for (int i = 0; i < 60; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(spinner).Position;
        p.X.Should().BeGreaterThan(-30f);
        p.X.Should().BeLessThan(30f);
        float.IsNaN(p.X).Should().BeFalse();
        float.IsNaN(p.Y).Should().BeFalse();
    }
}
