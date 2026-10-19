namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class MouseJointTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public MouseJointTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private BodyId AddBall(Vec2 center, float radius = 0.5f)
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Position = center });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, body, new Vec2(0, 0), radius);
        return body;
    }

    [Fact]
    public void Mouse_Drags_Body_To_Target()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        BodyId ball = AddBall(new Vec2(0, 0));

        JointId joint = _world.Joints.CreateMouse(_world.Bodies, new MouseDef
        {
            BodyA = ground,
            BodyB = ball,
            Target = new Vec2(0, 0),
            MaxForce = 500.0f,
        });
        joint.IsValid.Should().BeTrue();
        _world.Joints.SetMouseTarget(joint, new Vec2(0, 5));

        for (int i = 0; i < 180; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(ball).Position;
        p.X.Should().BeApproximately(0.0f, 0.3f);
        p.Y.Should().BeApproximately(5.0f, 0.6f);
    }

    [Fact]
    public void Mouse_SetTarget_Moves_Body()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        BodyId ball = AddBall(new Vec2(0, 0));

        JointId joint = _world.Joints.CreateMouse(_world.Bodies, new MouseDef
        {
            BodyA = ground,
            BodyB = ball,
            Target = new Vec2(0, 0),
            MaxForce = 500.0f,
        });

        _world.Joints.SetMouseTarget(joint, new Vec2(4, 0));

        for (int i = 0; i < 180; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(ball).Position;
        p.X.Should().BeApproximately(4.0f, 0.6f);
    }

    [Fact]
    public void Mouse_Zero_MaxForce_Applies_Nothing()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        BodyId ball = AddBall(new Vec2(0, 0));

        _world.Joints.CreateMouse(_world.Bodies, new MouseDef
        {
            BodyA = ground,
            BodyB = ball,
            Target = new Vec2(10, 0),
            MaxForce = 0.0f,
        });

        for (int i = 0; i < 60; i++)
        {
            _world.Step(Dt);
        }

        Vec2 p = _world.Bodies.GetTransform(ball).Position;
        p.X.Should().BeApproximately(0.0f, 1e-4f);
        p.Y.Should().BeLessThan(0.0f);
    }

    [Fact]
    public void Mouse_Rejects_Bad_Definition()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(1, 0));

        Action shared = () => _world.Joints.CreateMouse(_world.Bodies, new MouseDef
        {
            BodyA = a,
            BodyB = a,
            Target = new Vec2(5, 0),
        });
        shared.Should().Throw<InvalidOperationException>();

        Action nanTarget = () => _world.Joints.CreateMouse(_world.Bodies, new MouseDef
        {
            BodyA = a,
            BodyB = b,
            Target = new Vec2(float.NaN, 0),
        });
        nanTarget.Should().Throw<InvalidOperationException>();

        JointId joint = _world.Joints.CreateMouse(_world.Bodies, new MouseDef
        {
            BodyA = a,
            BodyB = b,
            Target = new Vec2(5, 0),
        });

        Action badTarget = () => _world.Joints.SetMouseTarget(joint, new Vec2(float.PositiveInfinity, 0));
        badTarget.Should().Throw<InvalidOperationException>();

        Action badId = () => _world.Joints.SetMouseTarget(new JointId(JointKind.Distance, 7), new Vec2(1, 0));
        badId.Should().Throw<InvalidOperationException>();

        _world.Joints.Destroy(joint);
        _world.Joints.IsValid(joint).Should().BeFalse();
    }
}
