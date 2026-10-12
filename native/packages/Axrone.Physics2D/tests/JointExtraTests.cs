namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class JointExtraTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public JointExtraTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private BodyId AddBall(Vec2 center, float radius = 0.5f, BodyType type = BodyType.Dynamic)
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Type = type, Position = center });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, body, new Vec2(0, 0), radius);
        return body;
    }

    [Fact]
    public void Weld_HoldsRelativePose()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(2, 0.5f));

        _world.Joints.CreateWeld(_world.Bodies, new WeldDef { BodyA = a, BodyB = b, WorldAnchor = new Vec2(1, 0.25f) });

        Vec2 startA = _world.Bodies.GetTransform(a).Position;
        Vec2 startB = _world.Bodies.GetTransform(b).Position;
        Vec2 startOffset = startB - startA;

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        Vec2 pa = _world.Bodies.GetTransform(a).Position;
        Vec2 pb = _world.Bodies.GetTransform(b).Position;
        ((pb - pa) - startOffset).LengthSquared().Should().BeLessThan(0.0025f);
    }

    [Fact]
    public void Weld_FallsAsOne()
    {
        BodyId a = AddBall(new Vec2(0, 5));
        BodyId b = AddBall(new Vec2(0, 6));

        _world.Joints.CreateWeld(_world.Bodies, new WeldDef { BodyA = a, BodyB = b, WorldAnchor = new Vec2(0, 5.5f) });

        for (int i = 0; i < 60; i++)
        {
            _world.Step(Dt);
        }

        Vec2 pa = _world.Bodies.GetTransform(a).Position;
        Vec2 pb = _world.Bodies.GetTransform(b).Position;
        (pb.Y - pa.Y).Should().BeApproximately(1f, 0.05f);
    }

    [Fact]
    public void Prismatic_SlidesFreely_ButLocksPerp()
    {
        BodyId rail = AddBall(new Vec2(0, -5), type: BodyType.Static);
        BodyId slider = AddBall(new Vec2(0, 0));
        _world.Bodies.SetLinearVelocity(slider, new Vec2(3, 2));

        _world.Joints.CreatePrismatic(_world.Bodies, new PrismaticDef
        {
            BodyA = rail,
            BodyB = slider,
            WorldAnchor = new Vec2(0, 0),
            WorldAxis = new Vec2(1, 0),
        });

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(slider).Position;
        p.X.Should().BeGreaterThan(1f);
        MathF.Abs(p.Y).Should().BeLessThan(0.05f);
    }

    [Fact]
    public void Prismatic_LimitStopsSlide()
    {
        BodyId rail = AddBall(new Vec2(0, -5), type: BodyType.Static);
        BodyId slider = AddBall(new Vec2(0, 0));
        _world.Bodies.SetLinearVelocity(slider, new Vec2(10, 0));

        _world.Joints.CreatePrismatic(_world.Bodies, new PrismaticDef
        {
            BodyA = rail,
            BodyB = slider,
            WorldAnchor = new Vec2(0, 0),
            WorldAxis = new Vec2(1, 0),
            EnableLimit = true,
            LowerTranslation = -1f,
            UpperTranslation = 1f,
        });

        for (int i = 0; i < 180; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(slider).Position;
        p.X.Should().BeLessThanOrEqualTo(1.15f);
        p.X.Should().BeGreaterThan(0.5f);
    }

    [Fact]
    public void Prismatic_MotorDrivesToSpeed()
    {
        BodyId rail = AddBall(new Vec2(0, 0), type: BodyType.Static);
        BodyId slider = AddBall(new Vec2(-4, 0));

        _world.Joints.CreatePrismatic(_world.Bodies, new PrismaticDef
        {
            BodyA = rail,
            BodyB = slider,
            WorldAnchor = new Vec2(-4, 0),
            WorldAxis = new Vec2(1, 0),
            EnableMotor = true,
            MotorSpeed = 2f,
            MaxMotorForce = 100f,
        });

        for (int i = 0; i < 30; i++)
        {
            _world.Step(Dt);
        }

        Vec2 v = _world.Bodies.GetLinearVelocity(slider);
        v.X.Should().BeApproximately(2f, 0.3f);
        Vec2 p = _world.Bodies.GetTransform(slider).Position;
        p.X.Should().BeGreaterThan(-4f);
        MathF.Abs(p.Y).Should().BeLessThan(0.05f);
    }

    [Fact]
    public void Prismatic_Create_WakesBodies()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(1, 0));
        _world.Bodies.SetAwake(a, false);
        _world.Bodies.SetAwake(b, false);

        _world.Joints.CreatePrismatic(_world.Bodies, new PrismaticDef
        {
            BodyA = a,
            BodyB = b,
            WorldAnchor = new Vec2(0.5f, 0),
            WorldAxis = new Vec2(1, 0),
        });

        _world.Bodies.IsAwake(a).Should().BeTrue();
        _world.Bodies.IsAwake(b).Should().BeTrue();
    }

    [Fact]
    public void JointDefs_RejectBadInput()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(1, 0));

        Action sameBody = () => _world.Joints.CreateWeld(_world.Bodies, new WeldDef { BodyA = a, BodyB = a, WorldAnchor = new Vec2(0, 0) });
        sameBody.Should().Throw<InvalidOperationException>();

        Action badAxis = () => _world.Joints.CreatePrismatic(_world.Bodies, new PrismaticDef
        {
            BodyA = a,
            BodyB = b,
            WorldAnchor = new Vec2(0, 0),
            WorldAxis = Vec2.Zero,
        });
        badAxis.Should().Throw<InvalidOperationException>();

        Action badLimit = () => _world.Joints.CreatePrismatic(_world.Bodies, new PrismaticDef
        {
            BodyA = a,
            BodyB = b,
            WorldAnchor = new Vec2(0, 0),
            WorldAxis = new Vec2(1, 0),
            EnableLimit = true,
            LowerTranslation = 2f,
            UpperTranslation = 1f,
        });
        badLimit.Should().Throw<InvalidOperationException>();
    }
}
