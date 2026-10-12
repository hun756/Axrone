namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

/// <summary>
/// Bit-exact replay: the engine holds no RNG, no wall clocks and no
/// order-unstable containers on the step path, so the same operation
/// sequence must reproduce every float bit-for-bit. This pins that contract:
/// a mixed scene (stack, pendulum, motor, bounce, sleep crossings) stepped
/// twice from identical construction compares exactly, not approximately.
/// </summary>
public sealed class Determinism2DTests : IDisposable
{
    private const float Dt = 1f / 60f;
    private const int Steps = 300;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private readonly record struct Snapshot(
        Vec2 Position,
        float Angle,
        Vec2 LinearVelocity,
        float AngularVelocity,
        bool Awake,
        int Contacts);

    private static World BuildScene()
    {
        var world = new World();

        BodyId ground = world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, -1) });
        world.Shapes.AttachPolygon(
            world.Bodies, world.Broadphase, ground,
            new Vec2[] { new(-10, -1), new(10, -1), new(10, 1), new(-10, 1) });

        for (int i = 0; i < 3; i++)
        {
            BodyId box = world.Bodies.Create(new BodyDef { Position = new Vec2(0.1f * i, 0.4f + i) });
            world.Shapes.AttachPolygon(
                world.Bodies, world.Broadphase, box,
                new Vec2[] { new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f) });
        }

        BodyId anchor = world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(-4, 3) });
        BodyId bob = world.Bodies.Create(new BodyDef { Position = new Vec2(-2, 3), LinearVelocity = new Vec2(0, 1) });
        world.Shapes.AttachCircle(world.Bodies, world.Broadphase, bob, new Vec2(0, 0), 0.25f);
        world.Joints.CreateDistance(world.Bodies, anchor, bob, new Vec2(0, 0), new Vec2(-2, 0), 2f);

        BodyId spinner = world.Bodies.Create(new BodyDef { Position = new Vec2(4, 2) });
        world.Shapes.AttachCircle(world.Bodies, world.Broadphase, spinner, new Vec2(0, 0), 0.25f);
        world.Joints.CreateRevolute(world.Bodies, new RevoluteDef
        {
            BodyA = ground,
            BodyB = spinner,
            WorldAnchor = new Vec2(4, 2),
            MotorSpeed = 3.0f,
            MaxMotorTorque = 50.0f,
            EnableMotor = true,
        });

        BodyId ball = world.Bodies.Create(new BodyDef { Position = new Vec2(2, 4), LinearVelocity = new Vec2(-1, -2) });
        world.Shapes.AttachCircle(
            world.Bodies, world.Broadphase, ball, new Vec2(0, 0), 0.4f,
            material: new PhysicsMaterial(0.1f, 0.8f));

        return world;
    }

    private static List<Snapshot> Run(World world)
    {
        var snapshots = new List<Snapshot>();
        for (int i = 0; i < Steps; i++)
        {
            world.Step(Dt);
        }

        for (int slot = 0; slot < 32; slot++)
        {
            BodyId id = new((uint)slot + 1u);
            if (!world.Bodies.IsValid(id))
            {
                continue;
            }

            snapshots.Add(new Snapshot(
                world.Bodies.GetTransform(id).Position,
                world.Bodies.GetTransform(id).Angle,
                world.Bodies.GetLinearVelocity(id),
                world.Bodies.GetAngularVelocity(id),
                world.Bodies.IsAwake(id),
                world.ContactCount));
        }

        return snapshots;
    }

    [Fact]
    public void IdenticalRuns_ReproduceEveryBit()
    {
        using var first = BuildScene();
        using var second = BuildScene();

        List<Snapshot> runA = Run(first);
        List<Snapshot> runB = Run(second);

        runA.Count.Should().Be(runB.Count).And.BeGreaterThan(0);

        for (int i = 0; i < runA.Count; i++)
        {
            runA[i].Position.X.Should().Be(runB[i].Position.X);
            runA[i].Position.Y.Should().Be(runB[i].Position.Y);
            runA[i].Angle.Should().Be(runB[i].Angle);
            runA[i].LinearVelocity.X.Should().Be(runB[i].LinearVelocity.X);
            runA[i].LinearVelocity.Y.Should().Be(runB[i].LinearVelocity.Y);
            runA[i].AngularVelocity.Should().Be(runB[i].AngularVelocity);
            runA[i].Awake.Should().Be(runB[i].Awake);
            runA[i].Contacts.Should().Be(runB[i].Contacts);
        }
    }

    [Fact]
    public void Scene_ExercisesSleepAndJoints()
    {
        using var world = BuildScene();

        for (int i = 0; i < Steps; i++)
        {
            world.Step(Dt);
        }

        world.ContactCount.Should().BeGreaterThan(0);
    }
}
