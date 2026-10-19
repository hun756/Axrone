using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public sealed class SleepFlagTests : IDisposable
{
    private readonly World _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void SetAwake_Tracks_Sleeping_Bit()
    {
        BodyId body = _world.Bodies.Create(new BodyDef());

        _world.Bodies.SetAwake(body, false);
        _world.Bodies.IsAwake(body).Should().BeFalse();
        _world.Bodies.IsSleeping(body).Should().BeTrue();

        _world.Bodies.SetAwake(body, true);
        _world.Bodies.IsAwake(body).Should().BeTrue();
        _world.Bodies.IsSleeping(body).Should().BeFalse();
    }

    [Fact]
    public void Flags_Without_SleepingBodies_Never_Sleeps()
    {
        using var world = new World(new WorldDef { Flags = SolverFlags.Default & ~SolverFlags.SleepingBodies });

        BodyId ground = world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        world.Shapes.AttachBox(world.Bodies, world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId ball = world.Bodies.Create(new BodyDef());
        world.Shapes.AttachSphere(world.Bodies, world.Broadphase, ball, Vec3.Zero, 0.5f);
        world.Bodies.SetTransform(ball, new Vec3(0.0f, 2.0f, 0.0f), Quat.Identity);

        for (int i = 0; i < 600; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        world.Bodies.IsAwake(ball).Should().BeTrue();
        world.Bodies.IsSleeping(ball).Should().BeFalse();
    }
}
