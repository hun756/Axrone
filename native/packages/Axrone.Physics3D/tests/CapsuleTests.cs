using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class CapsuleTests
{
    [Fact]
    public void CapsuleCapsule_Reports_DepthAndNormal()
    {
        var a = new Capsule3(new Vec3(0.0f, -1.0f, 0.0f), new Vec3(0.0f, 1.0f, 0.0f), 0.5f);
        var b = new Capsule3(new Vec3(0.8f, -1.0f, 0.0f), new Vec3(0.8f, 1.0f, 0.0f), 0.5f);

        bool hit = Penetration3D.CapsuleCapsule(a, b, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Normal.X.Should().BeApproximately(-1.0f, 1e-5f);
        manifold[0].Separation.Should().BeApproximately(-0.2f, 1e-4f);
    }

    [Fact]
    public void CapsuleCapsule_Disjoint_Reports_NoContact()
    {
        var a = new Capsule3(new Vec3(0.0f, -1.0f, 0.0f), new Vec3(0.0f, 1.0f, 0.0f), 0.5f);
        var b = new Capsule3(new Vec3(5.0f, -1.0f, 0.0f), new Vec3(5.0f, 1.0f, 0.0f), 0.5f);

        Penetration3D.CapsuleCapsule(a, b, out _).Should().BeFalse();
    }

    [Fact]
    public void CapsuleSphere_Reports_Depth()
    {
        var capsule = new Capsule3(new Vec3(0.0f, -1.0f, 0.0f), new Vec3(0.0f, 1.0f, 0.0f), 0.5f);
        var sphere = new Sphere3(new Vec3(0.8f, 0.0f, 0.0f), 0.5f);

        bool hit = Penetration3D.CapsuleSphere(capsule, sphere, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Normal.X.Should().BeApproximately(-1.0f, 1e-5f);
        manifold[0].Separation.Should().BeApproximately(-0.2f, 1e-4f);
    }

    [Fact]
    public void CapsuleBox_FaceContact_Reports_Depth()
    {
        var capsule = new Capsule3(new Vec3(0.0f, 1.2f, 0.0f), new Vec3(0.0f, 2.2f, 0.0f), 0.5f);
        var box = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));

        bool hit = Penetration3D.CapsuleBox(capsule, box, RigidTransform3D.Identity, out Manifold3D manifold);

        hit.Should().BeTrue();
        manifold.Normal.Y.Should().BeApproximately(1.0f, 1e-5f);
        manifold[0].Separation.Should().BeApproximately(-0.3f, 1e-4f);
    }

    [Fact]
    public void CapsuleBox_Disjoint_Reports_NoContact()
    {
        var capsule = new Capsule3(new Vec3(0.0f, 5.0f, 0.0f), new Vec3(0.0f, 6.0f, 0.0f), 0.5f);
        var box = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));

        Penetration3D.CapsuleBox(capsule, box, RigidTransform3D.Identity, out _).Should().BeFalse();
    }

    [Fact]
    public void RayCapsule_Hits_Side()
    {
        var capsule = new Capsule3(new Vec3(0.0f, -1.0f, 0.0f), new Vec3(0.0f, 1.0f, 0.0f), 0.5f);
        var ray = new Ray3D(new Vec3(-5.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f));

        bool hit = ShapeRaycast3D.RayCapsule(ray, 100.0f, capsule, out float distance, out Vec3 normal);

        hit.Should().BeTrue();
        distance.Should().BeApproximately(4.5f, 1e-4f);
        normal.X.Should().BeApproximately(-1.0f, 1e-4f);
    }

    [Fact]
    public void CapsuleWorld_Drops_OnGround()
    {
        using var world = new World();
        BodyId ground = world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        world.Shapes.AttachBox(world.Bodies, world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        BodyId body = world.Bodies.Create(new BodyDef());
        world.Shapes.AttachCapsule(world.Bodies, world.Broadphase, body,
            new Vec3(0.0f, -0.5f, 0.0f), new Vec3(0.0f, 0.5f, 0.0f), 0.5f);
        world.Bodies.SetTransform(body, new Vec3(0.0f, 5.0f, 0.0f), Quat.Identity);

        for (int i = 0; i < 240; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        float restingY = world.Bodies.GetTransform(body).Position.Y;
        restingY.Should().BeApproximately(1.0f, 0.12f);
    }
}
