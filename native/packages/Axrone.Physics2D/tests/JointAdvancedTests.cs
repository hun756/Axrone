namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class JointAdvancedTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public JointAdvancedTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private BodyId AddBall(Vec2 center, float radius = 0.5f, BodyType type = BodyType.Dynamic, float density = 1.0f)
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Type = type, Position = center });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, body, new Vec2(0, 0), radius, density);
        return body;
    }

    [Fact]
    public void Motor_PullsFollowerToTarget()
    {
        BodyId carrier = AddBall(new Vec2(0, 0), type: BodyType.Static);
        BodyId follower = AddBall(new Vec2(3, 2));

        _world.Joints.CreateMotor(_world.Bodies, new MotorDef
        {
            BodyA = carrier,
            BodyB = follower,
            LinearOffset = new Vec2(1, 0),
            MaxForce = 50f,
            MaxTorque = 10f,
            CorrectionFactor = 0.5f,
        });

        for (int i = 0; i < 240; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(follower).Position;
        ((p - new Vec2(1, 0)).LengthSquared()).Should().BeLessThan(0.09f);
    }

    [Fact]
    public void Motor_WeakForceCannotTeleport()
    {
        _world.Gravity = Vec2.Zero;
        BodyId carrier = AddBall(new Vec2(0, 0), type: BodyType.Static);
        BodyId follower = AddBall(new Vec2(3, 0));

        _world.Joints.CreateMotor(_world.Bodies, new MotorDef
        {
            BodyA = carrier,
            BodyB = follower,
            LinearOffset = Vec2.Zero,
            MaxForce = 0.1f,
            MaxTorque = 1f,
        });

        for (int i = 0; i < 60; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(follower).Position;
        p.X.Should().BeGreaterThan(2.5f);
        p.X.Should().BeLessThan(3.0f);
    }

    [Fact]
    public void Motor_RejectsBadInput()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(1, 0));

        Action sameBody = () => _world.Joints.CreateMotor(_world.Bodies, new MotorDef { BodyA = a, BodyB = a });
        sameBody.Should().Throw<InvalidOperationException>();

        Action badFactor = () => _world.Joints.CreateMotor(_world.Bodies, new MotorDef { BodyA = a, BodyB = b, CorrectionFactor = 0f });
        badFactor.Should().Throw<InvalidOperationException>();

        Action badForce = () => _world.Joints.CreateMotor(_world.Bodies, new MotorDef { BodyA = a, BodyB = b, MaxForce = -1f });
        badForce.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Wheel_SuspensionSettlesAndLocksLateral()
    {
        BodyId chassis = AddBall(new Vec2(0, 2), type: BodyType.Static);
        BodyId wheel = AddBall(new Vec2(0, 0), radius: 0.4f);
        _world.Bodies.SetLinearVelocity(wheel, new Vec2(2, -3));

        _world.Joints.CreateWheel(_world.Bodies, new WheelDef
        {
            BodyA = chassis,
            BodyB = wheel,
            WorldAnchor = new Vec2(0, 0),
            WorldAxis = new Vec2(0, 1),
        });

        for (int i = 0; i < 240; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(wheel).Position;
        MathF.Abs(p.X).Should().BeLessThan(0.1f);
        p.Y.Should().BeGreaterThan(-1.5f);
        p.Y.Should().BeLessThan(1.5f);
        float.IsNaN(p.X).Should().BeFalse();
        float.IsNaN(p.Y).Should().BeFalse();
    }

    [Fact]
    public void Wheel_HubMotorSpins()
    {
        _world.Gravity = Vec2.Zero;
        BodyId chassis = AddBall(new Vec2(0, 0), type: BodyType.Static);
        BodyId wheel = AddBall(new Vec2(0, -1), radius: 0.4f);

        _world.Joints.CreateWheel(_world.Bodies, new WheelDef
        {
            BodyA = chassis,
            BodyB = wheel,
            WorldAnchor = new Vec2(0, -1),
            WorldAxis = new Vec2(0, 1),
            EnableMotor = true,
            MotorSpeed = 5f,
            MaxMotorTorque = 20f,
        });

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        _world.Bodies.GetAngularVelocity(wheel).Should().BeApproximately(5f, 0.5f);
        Vec2 p = _world.Bodies.GetTransform(wheel).Position;
        MathF.Abs(p.X).Should().BeLessThan(0.1f);
        MathF.Abs(p.Y + 1.0f).Should().BeLessThan(0.1f);
    }

    [Fact]
    public void Wheel_RejectsBadInput()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(1, 0));

        Action badAxis = () => _world.Joints.CreateWheel(_world.Bodies, new WheelDef
        {
            BodyA = a,
            BodyB = b,
            WorldAnchor = new Vec2(0, 0),
            WorldAxis = Vec2.Zero,
        });
        badAxis.Should().Throw<InvalidOperationException>();

        Action badFrequency = () => _world.Joints.CreateWheel(_world.Bodies, new WheelDef
        {
            BodyA = a,
            BodyB = b,
            WorldAnchor = new Vec2(0, 0),
            WorldAxis = new Vec2(0, 1),
            SuspensionFrequency = 0f,
        });
        badFrequency.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Pulley_HeavySideFalls_LightSideRises()
    {
        BodyId heavy = AddBall(new Vec2(0, 2), radius: 0.3f, density: 4.0f);
        BodyId light = AddBall(new Vec2(4, 2), radius: 0.3f, density: 1.0f);

        _world.Joints.CreatePulley(_world.Bodies, new PulleyDef
        {
            BodyA = heavy,
            BodyB = light,
            GroundAnchorA = new Vec2(0, 5),
            GroundAnchorB = new Vec2(4, 5),
            WorldAnchorA = new Vec2(0, 2),
            WorldAnchorB = new Vec2(4, 2),
        });

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        Vec2 pa = _world.Bodies.GetTransform(heavy).Position;
        Vec2 pb = _world.Bodies.GetTransform(light).Position;
        pa.Y.Should().BeLessThan(2.0f);
        pb.Y.Should().BeGreaterThan(2.0f);

        float total = (pa - new Vec2(0, 5)).LengthSquared();
        total = MathF.Sqrt(total);
        float totalB = (pb - new Vec2(4, 5)).LengthSquared();
        totalB = MathF.Sqrt(totalB);
        (total + totalB).Should().BeApproximately(6.0f, 0.15f);
    }

    [Fact]
    public void Pulley_RatioDoublesTravel()
    {
        _world.Gravity = Vec2.Zero;
        BodyId a = AddBall(new Vec2(0, 0), radius: 0.3f);
        BodyId b = AddBall(new Vec2(6, 0), radius: 0.3f);

        _world.Joints.CreatePulley(_world.Bodies, new PulleyDef
        {
            BodyA = a,
            BodyB = b,
            GroundAnchorA = new Vec2(0, 4),
            GroundAnchorB = new Vec2(6, 4),
            WorldAnchorA = new Vec2(0, 0),
            WorldAnchorB = new Vec2(6, 0),
            Ratio = 2.0f,
        });
        _world.Bodies.SetLinearVelocity(a, new Vec2(0, -2));

        for (int i = 0; i < 60; i++)
        {
            _world.Step(Dt);
        }

        Vec2 pa = _world.Bodies.GetTransform(a).Position;
        Vec2 pb = _world.Bodies.GetTransform(b).Position;
        float travelA = 0.0f - pa.Y;
        float travelB = pb.Y - 0.0f;
        travelA.Should().BeGreaterThan(0.2f);
        (travelA / travelB).Should().BeApproximately(2.0f, 0.4f);
    }

    [Fact]
    public void Pulley_RejectsBadInput()
    {
        BodyId a = AddBall(new Vec2(0, 2));
        BodyId b = AddBall(new Vec2(4, 2));

        Action badRatio = () => _world.Joints.CreatePulley(_world.Bodies, new PulleyDef
        {
            BodyA = a,
            BodyB = b,
            GroundAnchorA = new Vec2(0, 5),
            GroundAnchorB = new Vec2(4, 5),
            WorldAnchorA = new Vec2(0, 2),
            WorldAnchorB = new Vec2(4, 2),
            Ratio = 0f,
        });
        badRatio.Should().Throw<InvalidOperationException>();

        Action slack = () => _world.Joints.CreatePulley(_world.Bodies, new PulleyDef
        {
            BodyA = a,
            BodyB = b,
            GroundAnchorA = new Vec2(0, 2),
            GroundAnchorB = new Vec2(4, 5),
            WorldAnchorA = new Vec2(0, 2),
            WorldAnchorB = new Vec2(4, 2),
        });
        slack.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Gear_LocksRevolutePair()
    {
        _world.Gravity = Vec2.Zero;
        BodyId anchorA = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, 0) });
        BodyId bobA = AddBall(new Vec2(0, 0));
        BodyId anchorB = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(5, 0) });
        BodyId bobB = AddBall(new Vec2(5, 0));

        JointId j1 = _world.Joints.CreateRevolute(_world.Bodies,
            new RevoluteDef { BodyA = anchorA, BodyB = bobA, WorldAnchor = new Vec2(0, 0) });
        JointId j2 = _world.Joints.CreateRevolute(_world.Bodies,
            new RevoluteDef { BodyA = anchorB, BodyB = bobB, WorldAnchor = new Vec2(5, 0) });
        _world.Joints.CreateGear(_world.Bodies, new GearDef { JointA = j1, JointB = j2, Ratio = 1.0f });

        _world.Bodies.SetAngularVelocity(bobA, 2.0f);

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        float a1 = _world.Bodies.GetTransform(bobA).Angle;
        float a2 = _world.Bodies.GetTransform(bobB).Angle;
        (a1 + a2).Should().BeApproximately(0.0f, 0.3f);
        MathF.Abs(a1).Should().BeGreaterThan(0.5f);
        MathF.Abs(a2).Should().BeGreaterThan(0.5f);
    }

    [Fact]
    public void Gear_LocksSliderPairInRatio()
    {
        _world.Gravity = Vec2.Zero;
        BodyId rail = AddBall(new Vec2(0, -5), type: BodyType.Static);
        BodyId s1 = AddBall(new Vec2(-2, 0));
        BodyId s2 = AddBall(new Vec2(2, 0));

        JointId j1 = _world.Joints.CreatePrismatic(_world.Bodies, new PrismaticDef
        {
            BodyA = rail, BodyB = s1, WorldAnchor = new Vec2(-2, 0), WorldAxis = new Vec2(1, 0),
        });
        JointId j2 = _world.Joints.CreatePrismatic(_world.Bodies, new PrismaticDef
        {
            BodyA = rail, BodyB = s2, WorldAnchor = new Vec2(2, 0), WorldAxis = new Vec2(1, 0),
        });
        _world.Joints.CreateGear(_world.Bodies, new GearDef { JointA = j1, JointB = j2, Ratio = 2.0f });

        _world.Bodies.SetLinearVelocity(s1, new Vec2(2, 0));

        for (int i = 0; i < 60; i++)
        {
            _world.Step(Dt);
        }

        float x1 = _world.Bodies.GetTransform(s1).Position.X;
        float x2 = _world.Bodies.GetTransform(s2).Position.X;
        (x1 + (2.0f * x2)).Should().BeApproximately(2.0f, 0.3f);
        (x1 + 2.0f).Should().BeGreaterThan(0.2f);
    }

    [Fact]
    public void Gear_DestroyCascadesAndStepsClean()
    {
        _world.Gravity = Vec2.Zero;
        BodyId anchorA = AddBall(new Vec2(0, 0), type: BodyType.Static);
        BodyId bobA = AddBall(new Vec2(0, -2));
        BodyId anchorB = AddBall(new Vec2(5, 0), type: BodyType.Static);
        BodyId bobB = AddBall(new Vec2(5, -2));

        JointId j1 = _world.Joints.CreateRevolute(_world.Bodies,
            new RevoluteDef { BodyA = anchorA, BodyB = bobA, WorldAnchor = new Vec2(0, 0) });
        JointId j2 = _world.Joints.CreateRevolute(_world.Bodies,
            new RevoluteDef { BodyA = anchorB, BodyB = bobB, WorldAnchor = new Vec2(5, 0) });
        JointId gear = _world.Joints.CreateGear(_world.Bodies, new GearDef { JointA = j1, JointB = j2, Ratio = 1.0f });
        _world.Joints.Count.Should().Be(3);

        _world.Joints.Destroy(j1);

        _world.Joints.Count.Should().Be(1);
        _world.Joints.IsValid(j2).Should().BeTrue();
        _world.Joints.IsValid(gear).Should().BeFalse();

        for (int i = 0; i < 30; i++)
        {
            _world.Step(Dt);
        }
    }

    [Fact]
    public void Gear_DestroyBodyCleansDependents()
    {
        _world.Gravity = Vec2.Zero;
        BodyId anchorA = AddBall(new Vec2(0, 0), type: BodyType.Static);
        BodyId bobA = AddBall(new Vec2(0, -2));
        BodyId anchorB = AddBall(new Vec2(5, 0), type: BodyType.Static);
        BodyId bobB = AddBall(new Vec2(5, -2));

        JointId j1 = _world.Joints.CreateRevolute(_world.Bodies,
            new RevoluteDef { BodyA = anchorA, BodyB = bobA, WorldAnchor = new Vec2(0, 0) });
        JointId j2 = _world.Joints.CreateRevolute(_world.Bodies,
            new RevoluteDef { BodyA = anchorB, BodyB = bobB, WorldAnchor = new Vec2(5, 0) });
        _ = _world.Joints.CreateGear(_world.Bodies, new GearDef { JointA = j1, JointB = j2, Ratio = 1.0f });

        _world.DestroyBody(bobA);

        _world.Joints.Count.Should().Be(1);
        _world.Joints.IsValid(j2).Should().BeTrue();

        for (int i = 0; i < 30; i++)
        {
            _world.Step(Dt);
        }
    }

    [Fact]
    public void Gear_RejectsBadInput()
    {
        BodyId anchor = AddBall(new Vec2(0, 0), type: BodyType.Static);
        BodyId bob = AddBall(new Vec2(0, -2));
        BodyId anchor2 = AddBall(new Vec2(5, 0), type: BodyType.Static);
        BodyId bob2 = AddBall(new Vec2(5, -2));
        JointId hinge = _world.Joints.CreateRevolute(_world.Bodies,
            new RevoluteDef { BodyA = anchor, BodyB = bob, WorldAnchor = new Vec2(0, 0) });
        JointId hinge2 = _world.Joints.CreateRevolute(_world.Bodies,
            new RevoluteDef { BodyA = anchor2, BodyB = bob2, WorldAnchor = new Vec2(5, 0) });
        JointId rod = _world.Joints.CreateDistance(_world.Bodies, anchor, bob, new Vec2(0, 0), new Vec2(0, 0), 2f);
        JointId gear = _world.Joints.CreateGear(_world.Bodies, new GearDef { JointA = hinge, JointB = hinge2, Ratio = 1.0f });

        Action same = () => _world.Joints.CreateGear(_world.Bodies, new GearDef { JointA = hinge, JointB = hinge, Ratio = 1.0f });
        same.Should().Throw<InvalidOperationException>();

        Action wrongKind = () => _world.Joints.CreateGear(_world.Bodies, new GearDef { JointA = hinge, JointB = rod, Ratio = 1.0f });
        wrongKind.Should().Throw<InvalidOperationException>();

        Action gearOfGear = () => _world.Joints.CreateGear(_world.Bodies, new GearDef { JointA = hinge, JointB = gear, Ratio = 1.0f });
        gearOfGear.Should().Throw<InvalidOperationException>();

        Action badRatio = () => _world.Joints.CreateGear(_world.Bodies, new GearDef { JointA = hinge, JointB = hinge2, Ratio = 0f });
        badRatio.Should().Throw<InvalidOperationException>();
    }
}
