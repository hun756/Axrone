using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public sealed class Sleep3DTests : IDisposable
{
    private readonly World _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void RestingBall_FallsAsleep()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId ball = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(ball, new Vec3(0.0f, 2.0f, 0.0f), Quat.Identity);

        for (int i = 0; i < 600; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        _world.Bodies.IsAwake(ball).Should().BeFalse();
        _world.IslandMetrics.SleptBodies.Should().BeGreaterThan(0);
    }

    [Fact]
    public void JointedPair_Sleeps_Together()
    {
        BodyId a = _world.Bodies.Create(new BodyDef { GravityScale = 0.0f });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, a, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(a, new Vec3(-1.0f, 3.0f, 0.0f), Quat.Identity);

        BodyId b = _world.Bodies.Create(new BodyDef { GravityScale = 0.0f });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, b, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(b, new Vec3(1.0f, 3.0f, 0.0f), Quat.Identity);

        _world.Joints.CreateDistance(_world.Bodies, a, b, Vec3.Zero, Vec3.Zero, 2.0f);

        for (int i = 0; i < 300; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        _world.Bodies.IsAwake(a).Should().Be(_world.Bodies.IsAwake(b));
    }

    [Fact]
    public void NewContact_Wakes_SleepingBody()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId ball = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(ball, new Vec3(0.0f, 0.5f, 0.0f), Quat.Identity);

        for (int i = 0; i < 300; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        _world.Bodies.IsAwake(ball).Should().BeFalse();

        BodyId drop = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, drop, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(drop, new Vec3(0.0f, 5.0f, 0.0f), Quat.Identity);

        for (int i = 0; i < 120; i++)
        {
            _world.Step(1.0f / 60.0f);
            if (_world.Bodies.IsAwake(ball))
            {
                break;
            }
        }

        _world.Bodies.IsAwake(ball).Should().BeTrue();
    }
}
