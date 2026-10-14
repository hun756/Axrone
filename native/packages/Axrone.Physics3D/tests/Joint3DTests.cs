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

    [Fact]
    public void Hinge_Swings_Freely_AboutAxis()
    {
        BodyId frame = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });

        BodyId door = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, door, Vec3.Zero, new Vec3(0.5f, 1.0f, 0.1f));
        _world.Bodies.SetTransform(door, new Vec3(0.5f, 3.0f, 0.0f), Quat.Identity);
        _world.Bodies.SetAngularVelocity(door, new Vec3(0.0f, 3.0f, 0.0f));

        _world.Joints.CreateHinge(
            _world.Bodies, frame, door,
            new Vec3(0.0f, 3.0f, 0.0f), new Vec3(-0.5f, 0.0f, 0.0f),
            new Vec3(0.0f, 1.0f, 0.0f), new Vec3(0.0f, 1.0f, 0.0f));

        for (int i = 0; i < 120; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        RigidTransform3D transform = _world.Bodies.GetTransform(door);
        Vec3 hingePoint = transform.TransformPoint(new Vec3(-0.5f, 0.0f, 0.0f));
        hingePoint.X.Should().BeApproximately(0.0f, 0.15f);
        hingePoint.Y.Should().BeApproximately(3.0f, 0.15f);
    }

    [Fact]
    public void HingeMotor_Spins_AtTargetSpeed()
    {
        BodyId frame = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });

        BodyId wheel = _world.Bodies.Create(new BodyDef { GravityScale = 0.0f });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, wheel, Vec3.Zero, 0.5f);
        _world.Bodies.SetTransform(wheel, new Vec3(0.0f, 3.0f, 0.0f), Quat.Identity);

        _world.Joints.CreateHinge(
            _world.Bodies, frame, wheel, new Vec3(0.0f, 3.0f, 0.0f), Vec3.Zero,
            new Vec3(0.0f, 0.0f, 1.0f), new Vec3(0.0f, 0.0f, 1.0f),
            motorSpeed: 4.0f, maxMotorTorque: 50.0f, enableMotor: true);

        for (int i = 0; i < 120; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        _world.Bodies.GetAngularVelocity(wheel).Z.Should().BeApproximately(4.0f, 0.5f);
    }

    [Fact]
    public void HingeLimit_Stops_AtUpperStop()
    {
        BodyId frame = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });

        BodyId arm = _world.Bodies.Create(new BodyDef { GravityScale = 0.0f });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, arm, Vec3.Zero, 0.3f);
        _world.Bodies.SetTransform(arm, new Vec3(0.0f, 3.0f, 0.0f), Quat.Identity);
        _world.Bodies.SetAngularVelocity(arm, new Vec3(0.0f, 0.0f, 10.0f));

        _world.Joints.CreateHinge(
            _world.Bodies, frame, arm, new Vec3(0.0f, 3.0f, 0.0f), Vec3.Zero,
            new Vec3(0.0f, 0.0f, 1.0f), new Vec3(0.0f, 0.0f, 1.0f),
            lowerLimit: -1.0f, upperLimit: 1.0f, enableLimit: true);

        for (int i = 0; i < 240; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        _world.Bodies.GetAngularVelocity(arm).Z.Should().BeLessThan(2.0f);
    }

    [Fact]
    public void Prismatic_Slides_AlongAxisOnly()
    {
        BodyId frame = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });

        BodyId slider = _world.Bodies.Create(new BodyDef());
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, slider, Vec3.Zero, 0.3f);
        _world.Bodies.SetTransform(slider, new Vec3(0.0f, 3.0f, 0.0f), Quat.Identity);
        _world.Bodies.SetLinearVelocity(slider, new Vec3(2.0f, 0.0f, 0.0f));

        _world.Joints.CreatePrismatic(
            _world.Bodies, frame, slider, new Vec3(0.0f, 3.0f, 0.0f), Vec3.Zero,
            new Vec3(1.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f));

        for (int i = 0; i < 120; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        Vec3 position = _world.Bodies.GetTransform(slider).Position;
        position.Y.Should().BeApproximately(3.0f, 0.2f);
        position.Z.Should().BeApproximately(0.0f, 0.2f);
        position.X.Should().BeGreaterThan(0.5f);
    }

    [Fact]
    public void PrismaticMotor_Drives_AtTargetSpeed()
    {
        BodyId frame = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });

        BodyId slider = _world.Bodies.Create(new BodyDef { GravityScale = 0.0f });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, slider, Vec3.Zero, 0.3f);
        _world.Bodies.SetTransform(slider, new Vec3(0.0f, 3.0f, 0.0f), Quat.Identity);

        _world.Joints.CreatePrismatic(
            _world.Bodies, frame, slider, new Vec3(0.0f, 3.0f, 0.0f), Vec3.Zero,
            new Vec3(1.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f),
            motorSpeed: 2.0f, maxMotorForce: 100.0f, enableMotor: true);

        for (int i = 0; i < 120; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        _world.Bodies.GetLinearVelocity(slider).X.Should().BeApproximately(2.0f, 0.3f);
    }

    [Fact]
    public void PrismaticLimit_Stops_AtUpperStop()
    {
        BodyId frame = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });

        BodyId slider = _world.Bodies.Create(new BodyDef { GravityScale = 0.0f });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, slider, Vec3.Zero, 0.3f);
        _world.Bodies.SetTransform(slider, new Vec3(0.0f, 3.0f, 0.0f), Quat.Identity);
        _world.Bodies.SetLinearVelocity(slider, new Vec3(5.0f, 0.0f, 0.0f));

        _world.Joints.CreatePrismatic(
            _world.Bodies, frame, slider, new Vec3(0.0f, 3.0f, 0.0f), Vec3.Zero,
            new Vec3(1.0f, 0.0f, 0.0f), new Vec3(1.0f, 0.0f, 0.0f),
            lowerLimit: -1.0f, upperLimit: 1.0f, enableLimit: true);

        for (int i = 0; i < 240; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        float travel = _world.Bodies.GetTransform(slider).Position.X;
        travel.Should().BeLessThan(1.3f);
        _world.Bodies.GetLinearVelocity(slider).X.Should().BeLessThan(0.5f);
    }
}
