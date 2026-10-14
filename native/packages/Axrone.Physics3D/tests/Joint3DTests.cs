using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public sealed class Joint3DTests : IDisposable
{
    private readonly World _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void DistanceJoint_Holds_RestLength()
    {
        BodyId a = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, a, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(a, new Vec3(-2.0f, 5.0f, 0.0f), Quat.Identity);

        BodyId b = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, b, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(b, new Vec3(2.0f, 5.0f, 0.0f), Quat.Identity);

        JointId joint = _world.Joints.CreateDistance(
            _world.Bodies, a, b, Vec3.Zero, Vec3.Zero, 4.0f);

        _world.Joints.IsValid(joint).Should().BeTrue();

        for (int i = 0; i < 120; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        Vec3 pa = _world.Bodies.GetTransform(a).Position;
        Vec3 pb = _world.Bodies.GetTransform(b).Position;
        (pa - pb).Length().Should().BeApproximately(4.0f, 0.15f);
        pa.Y.Should().BeLessThan(5.0f);
    }

    [Fact]
    public void BallSocket_Keeps_AnchorsTogether()
    {
        BodyId a = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, a, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(a, new Vec3(0.0f, 3.0f, 0.0f), Quat.Identity);

        BodyId b = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, b, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(b, new Vec3(0.0f, 5.0f, 0.0f), Quat.Identity);

        _world.Joints.CreateBallSocket(_world.Bodies, a, b, new Vec3(0.0f, 1.0f, 0.0f), new Vec3(0.0f, -1.0f, 0.0f));

        for (int i = 0; i < 120; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        RigidTransform3D fa = _world.Bodies.GetTransform(a);
        RigidTransform3D fb = _world.Bodies.GetTransform(b);
        Vec3 anchorA = fa.TransformPoint(new Vec3(0.0f, 1.0f, 0.0f));
        Vec3 anchorB = fb.TransformPoint(new Vec3(0.0f, -1.0f, 0.0f));

        (anchorA - anchorB).Length().Should().BeLessThan(0.1f);
    }

    [Fact]
    public void Weld_Locks_RelativeOrientation()
    {
        BodyId a = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, a, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(a, new Vec3(0.0f, 3.0f, 0.0f), Quat.Identity);

        BodyId b = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, b, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(b, new Vec3(2.0f, 3.0f, 0.0f), Quat.Identity);
        _world.Bodies.SetAngularVelocity(b, new Vec3(0.0f, 0.0f, 5.0f));

        _world.Joints.CreateWeld(_world.Bodies, a, b, Vec3.Zero, new Vec3(-2.0f, 0.0f, 0.0f));

        for (int i = 0; i < 120; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        Quat qa = _world.Bodies.GetTransform(a).Rotation;
        Quat qb = _world.Bodies.GetTransform(b).Rotation;
        Quat relative = qa.Conjugate() * qb;
        if (relative.W < 0.0f)
        {
            relative = new Quat(-relative.X, -relative.Y, -relative.Z, -relative.W);
        }

        relative.W.Should().BeApproximately(1.0f, 0.05f);
    }

    [Fact]
    public void Destroy_Removes_Joint()
    {
        BodyId a = _world.Bodies.Create(new BodyDef());
        BodyId b = _world.Bodies.Create(new BodyDef());

        JointId joint = _world.Joints.CreateDistance(_world.Bodies, a, b, Vec3.Zero, Vec3.Zero, 1.0f);
        _world.Joints.Destroy(joint);

        _world.Joints.IsValid(joint).Should().BeFalse();
        _world.Joints.Count.Should().Be(0);
    }

    [Fact]
    public void DestroyBody_Drops_ItsJoints()
    {
        BodyId a = _world.Bodies.Create(new BodyDef());
        BodyId b = _world.Bodies.Create(new BodyDef());
        _world.Joints.CreateDistance(_world.Bodies, a, b, Vec3.Zero, Vec3.Zero, 1.0f);

        _world.DestroyBody(a);

        _world.Joints.Count.Should().Be(0);
    }
}
