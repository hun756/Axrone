using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public sealed class Determinism3DTests
{
    [Fact]
    public void RepeatedSteps_Are_BitIdentical()
    {
        Vec3 first = RunScene();
        Vec3 second = RunScene();

        first.Should().Be(second);
    }

    private static Vec3 RunScene()
    {
        using var world = new World();
        BodyId ground = world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        world.Shapes.AttachBox(world.Bodies, world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId ball = world.Bodies.Create(new BodyDef());
        world.Shapes.AttachSphere(world.Bodies, world.Broadphase, ball, Vec3.Zero, 0.5f);
        world.Bodies.SetTransform(ball, new Vec3(1.0f, 4.0f, -2.0f), Quat.Identity);
        world.Bodies.SetLinearVelocity(ball, new Vec3(0.5f, 0.0f, 0.3f));

        BodyId box = world.Bodies.Create(new BodyDef());
        world.Shapes.AttachBox(world.Bodies, world.Broadphase, box, Vec3.Zero, new Vec3(0.4f, 0.4f, 0.4f));
        world.Bodies.SetTransform(box, new Vec3(-1.0f, 6.0f, 1.0f), Quat.Identity);

        BodyId anchor = world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        world.Joints.CreateDistance(world.Bodies, anchor, ball, new Vec3(1.0f, 8.0f, -2.0f), Vec3.Zero, 4.0f);

        for (int i = 0; i < 300; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        return world.Bodies.GetTransform(ball).Position;
    }
}
