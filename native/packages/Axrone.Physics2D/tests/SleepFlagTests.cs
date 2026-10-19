namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class SleepFlagTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public SleepFlagTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

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

        BodyId ground = world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, -1) });
        world.Shapes.AttachPolygon(
            world.Bodies, world.Broadphase, ground,
            new Vec2[]
            {
                new(-10, -1), new(10, -1), new(10, 1), new(-10, 1),
            });

        BodyId box = world.Bodies.Create(new BodyDef { Position = new Vec2(0, 0.4f) });
        world.Shapes.AttachPolygon(
            world.Bodies, world.Broadphase, box,
            new Vec2[]
            {
                new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f),
            });

        for (int i = 0; i < 180; i++)
        {
            world.Step(Dt);
        }

        world.Bodies.IsAwake(box).Should().BeTrue();
        world.Bodies.IsSleeping(box).Should().BeFalse();
    }
}
