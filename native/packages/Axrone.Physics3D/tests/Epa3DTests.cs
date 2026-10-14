using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class Epa3DTests
{
    [Fact]
    public void CylinderCylinder_Stacked_Reports_TwoSidedManifold()
    {
        var lower = new OrientedShape<Cylinder3>(
            new Cylinder3(Vec3.Zero, 0.5f, 1.0f, ShapeAxis3D.Y), RigidTransform3D.Identity);
        var upper = new OrientedShape<Cylinder3>(
            new Cylinder3(new Vec3(0.0f, 0.8f, 0.0f), 0.5f, 1.0f, ShapeAxis3D.Y), RigidTransform3D.Identity);

        bool hit = Epa3D.ComputePenetration(lower, upper, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Count.Should().Be(1);
        manifold.Normal.Y.Should().BeApproximately(-1.0f, 0.05f);
        manifold[0].Separation.Should().BeApproximately(-0.2f, 0.05f);
    }

    [Fact]
    public void CylinderCylinder_Disjoint_Reports_NoContact()
    {
        var lower = new OrientedShape<Cylinder3>(
            new Cylinder3(Vec3.Zero, 0.5f, 1.0f, ShapeAxis3D.Y), RigidTransform3D.Identity);
        var upper = new OrientedShape<Cylinder3>(
            new Cylinder3(new Vec3(0.0f, 5.0f, 0.0f), 0.5f, 1.0f, ShapeAxis3D.Y), RigidTransform3D.Identity);

        Epa3D.ComputePenetration(lower, upper, out _).Should().BeFalse();
    }

    [Fact]
    public void RotatedCylinder_AgainstBox_Reports_Contact()
    {
        var tilted = new OrientedShape<Cylinder3>(
            new Cylinder3(Vec3.Zero, 0.5f, 2.0f, ShapeAxis3D.Y),
            new RigidTransform3D(new Vec3(0.0f, 1.2f, 0.0f),
                Quat.CreateFromAxisAngle(new Vec3(0.0f, 0.0f, 1.0f), 0.3f)));
        var box = new OrientedShape<Box3>(
            new Box3(Vec3.Zero, new Vec3(2.0f, 0.5f, 2.0f)), RigidTransform3D.Identity);

        bool hit = Epa3D.ComputePenetration(tilted, box, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Normal.Y.Should().BeGreaterThan(0.9f);
    }

    [Fact]
    public void ConeAgainstGround_Reports_UpwardNormal()
    {
        var cone = new OrientedShape<Cone3>(
            new Cone3(new Vec3(0.0f, 0.3f, 0.0f), 0.5f, 2.0f, ShapeAxis3D.Y), RigidTransform3D.Identity);
        var ground = new OrientedShape<Box3>(
            new Box3(new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f)), RigidTransform3D.Identity);

        bool hit = Epa3D.ComputePenetration(ground, cone, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Normal.Y.Should().BeApproximately(-1.0f, 0.05f);
    }

    [Fact]
    public void CylinderWorld_Drops_OnGround()
    {
        using var world = new World();
        BodyId ground = world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        world.Shapes.AttachBox(world.Bodies, world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId body = world.Bodies.Create(new BodyDef());
        world.Shapes.AttachCylinder(world.Bodies, world.Broadphase, body, Vec3.Zero, 0.5f, 1.0f);
        world.Bodies.SetTransform(body, new Vec3(0.0f, 5.0f, 0.0f), Quat.Identity);

        for (int i = 0; i < 240; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        float restingY = world.Bodies.GetTransform(body).Position.Y;
        restingY.Should().BeApproximately(0.5f, 0.12f);
    }

    [Fact]
    public void ConeWorld_Drops_OnGround()
    {
        using var world = new World();
        BodyId ground = world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        world.Shapes.AttachBox(world.Bodies, world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId body = world.Bodies.Create(new BodyDef());
        world.Shapes.AttachCone(world.Bodies, world.Broadphase, body, Vec3.Zero, 0.5f, 1.0f);
        world.Bodies.SetTransform(body, new Vec3(0.0f, 5.0f, 0.0f), Quat.Identity);

        for (int i = 0; i < 240; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        float restingY = world.Bodies.GetTransform(body).Position.Y;
        restingY.Should().BeApproximately(0.25f, 0.15f);
    }
}
