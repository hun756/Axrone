namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class Revolute2DTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public Revolute2DTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private static float Normalize(float angle)
    {
        float wrapped = angle - (MathF.Floor((angle + MathF.PI) / (2.0f * MathF.PI)) * 2.0f * MathF.PI);
        return wrapped;
    }

    private float JointAngle(BodyId anchor, BodyId arm, float reference)
    {
        float angleA = _world.Bodies.GetTransform(anchor).Angle;
        float angleB = _world.Bodies.GetTransform(arm).Angle;
        return Normalize((angleB - angleA) - reference);
    }

    [Fact]
    public void Hinge_HoldsPin_WhileArmSwings()
    {
        BodyId anchor = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, 0) });
        BodyId arm = _world.Bodies.Create(new BodyDef
        {
            Position = new Vec2(2, 0),
            LinearVelocity = new Vec2(0, 1.5f),
        });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, arm, new Vec2(0, 0), 0.25f);

        JointId joint = _world.Joints.CreateRevolute(_world.Bodies, new RevoluteDef
        {
            BodyA = anchor,
            BodyB = arm,
            WorldAnchor = new Vec2(0, 0),
        });
        joint.IsValid.Should().BeTrue();

        float maxPinError = 0f;
        float swept = 0f;
        Vec2 previous = new(2, 0);
        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
            Vec2 p = _world.Bodies.GetTransform(arm).Position;
            maxPinError = MathF.Max(maxPinError, MathF.Abs(p.LengthSquared() - 4f));
            swept = MathF.Max(swept, MathF.Abs(p.X - previous.X));
            previous = p;
        }

        maxPinError.Should().BeLessThan(0.05f);
        swept.Should().BeGreaterThan(0.05f);
    }

    [Fact]
    public void Limit_StopsRotation_AtStop()
    {
        BodyId anchor = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, 0) });
        BodyId arm = _world.Bodies.Create(new BodyDef { Position = new Vec2(2, 0) });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, arm, new Vec2(0, 0), 0.25f);
        _world.Bodies.SetTransform(arm, new Vec2(2, 0), 2.0f);

        _world.Joints.CreateRevolute(_world.Bodies, new RevoluteDef
        {
            BodyA = anchor,
            BodyB = arm,
            WorldAnchor = new Vec2(0, 0),
            LowerAngle = -0.5f,
            UpperAngle = 0.5f,
            EnableLimit = true,
        });

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        float settled = JointAngle(anchor, arm, 2.0f);
        settled.Should().BeGreaterThan(-0.7f);
        settled.Should().BeLessThan(-0.3f);
    }

    [Fact]
    public void Motor_SpinsArm_AtTargetSpeed()
    {
        _world.Gravity = Vec2.Zero;
        BodyId anchor = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, 0) });
        BodyId arm = _world.Bodies.Create(new BodyDef { Position = new Vec2(0, 0) });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, arm, new Vec2(0, 0), 0.25f);

        _world.Joints.CreateRevolute(_world.Bodies, new RevoluteDef
        {
            BodyA = anchor,
            BodyB = arm,
            WorldAnchor = new Vec2(0, 0),
            MotorSpeed = 2.0f,
            MaxMotorTorque = 100.0f,
            EnableMotor = true,
        });

        for (int i = 0; i < 60; i++)
        {
            _world.Step(Dt);
        }

        float angle = Normalize(_world.Bodies.GetTransform(arm).Angle);
        angle.Should().BeGreaterThan(1.5f);
        angle.Should().BeLessThan(2.5f);
        _world.Bodies.GetAngularVelocity(arm).Should().BeApproximately(2f, 0.1f);
    }

    [Fact]
    public void CreateRevolute_RejectsBadInput()
    {
        BodyId a = _world.Bodies.Create(new BodyDef());
        BodyId b = _world.Bodies.Create(new BodyDef());

        var sameBody = new RevoluteDef { BodyA = a, BodyB = a, WorldAnchor = new Vec2(0, 0) };
        var badLimits = new RevoluteDef { BodyA = a, BodyB = b, WorldAnchor = new Vec2(0, 0), LowerAngle = 1f, UpperAngle = -1f, EnableLimit = true };
        var badAnchor = new RevoluteDef { BodyA = a, BodyB = b, WorldAnchor = new Vec2(float.NaN, 0) };
        var badTorque = new RevoluteDef { BodyA = a, BodyB = b, WorldAnchor = new Vec2(0, 0), MaxMotorTorque = -1f, EnableMotor = true };
        var deadBody = new RevoluteDef { BodyA = a, BodyB = new BodyId(999u), WorldAnchor = new Vec2(0, 0) };

        Action same = () => _world.Joints.CreateRevolute(_world.Bodies, in sameBody);
        Action limits = () => _world.Joints.CreateRevolute(_world.Bodies, in badLimits);
        Action anchor = () => _world.Joints.CreateRevolute(_world.Bodies, in badAnchor);
        Action torque = () => _world.Joints.CreateRevolute(_world.Bodies, in badTorque);
        Action dead = () => _world.Joints.CreateRevolute(_world.Bodies, in deadBody);

        same.Should().Throw<InvalidOperationException>();
        limits.Should().Throw<InvalidOperationException>();
        anchor.Should().Throw<InvalidOperationException>();
        torque.Should().Throw<InvalidOperationException>();
        dead.Should().Throw<InvalidOperationException>();
    }
}
