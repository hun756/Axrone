namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class RopeJointTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public RopeJointTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private BodyId AddBall(Vec2 center, Vec2 velocity = default, float radius = 0.5f)
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Position = center, LinearVelocity = velocity });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, body, new Vec2(0, 0), radius);
        return body;
    }

    private static float AnchorDistance(World world, BodyId a, BodyId b)
    {
        Vec2 pa = world.Bodies.GetTransform(a).Position;
        Vec2 pb = world.Bodies.GetTransform(b).Position;
        return (pb - pa).Length();
    }

    [Fact]
    public void Rope_Holds_Max_Length()
    {
        BodyId anchor = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        BodyId ball = AddBall(new Vec2(2, 0), new Vec2(40, 0), 0.25f);

        JointId joint = _world.Joints.CreateRope(_world.Bodies, new RopeDef
        {
            BodyA = anchor,
            BodyB = ball,
            MaxLength = 2.0f,
        });
        joint.IsValid.Should().BeTrue();

        float maxSeen = 0.0f;
        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
            maxSeen = MathF.Max(maxSeen, AnchorDistance(_world, anchor, ball));
        }

        maxSeen.Should().BeLessThanOrEqualTo(2.3f);
        AnchorDistance(_world, anchor, ball).Should().BeApproximately(2.0f, 0.25f);
    }

    [Fact]
    public void Rope_Slack_Exerts_Nothing()
    {
        BodyId anchor = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        BodyId ball = AddBall(new Vec2(1, 0), new Vec2(-5, 0), 0.25f);

        _world.Joints.CreateRope(_world.Bodies, new RopeDef
        {
            BodyA = anchor,
            BodyB = ball,
            MaxLength = 4.0f,
        });

        for (int i = 0; i < 30; i++)
        {
            _world.Step(Dt);
        }

        AnchorDistance(_world, anchor, ball).Should().BeLessThan(4.0f);
    }

    [Fact]
    public void Rope_Rejects_Bad_Definition()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(1, 0));

        Action nan = () => _world.Joints.CreateRope(_world.Bodies, new RopeDef
        {
            BodyA = a,
            BodyB = b,
            MaxLength = float.NaN,
        });
        nan.Should().Throw<ArgumentOutOfRangeException>();

        Action negative = () => _world.Joints.CreateRope(_world.Bodies, new RopeDef
        {
            BodyA = a,
            BodyB = b,
            MaxLength = -1.0f,
        });
        negative.Should().Throw<ArgumentOutOfRangeException>();

        Action shared = () => _world.Joints.CreateRope(_world.Bodies, new RopeDef
        {
            BodyA = a,
            BodyB = a,
            MaxLength = 2.0f,
        });
        shared.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rope_Destroy_Releases()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(1, 0));

        JointId joint = _world.Joints.CreateRope(_world.Bodies, new RopeDef
        {
            BodyA = a,
            BodyB = b,
            MaxLength = 2.0f,
        });
        _world.Joints.Count.Should().Be(1);

        _world.Joints.Destroy(joint);
        _world.Joints.IsValid(joint).Should().BeFalse();
        _world.Joints.Count.Should().Be(0);
    }
}
