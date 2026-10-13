namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class Joint2DTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public Joint2DTests()
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
    public void DistanceJoint_HoldsRestLength()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(3, 0));

        JointId joint = _world.Joints.CreateDistance(_world.Bodies, a, b, new Vec2(0, 0), new Vec2(0, 0), 3f);
        joint.IsValid.Should().BeTrue();
        _world.Joints.Count.Should().Be(1);

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        Vec2 pa = _world.Bodies.GetTransform(a).Position;
        Vec2 pb = _world.Bodies.GetTransform(b).Position;
        (pb - pa).LengthSquared().Should().BeApproximately(9f, 0.05f);
    }

    [Fact]
    public void DistanceJoint_PendulumSwings_WithoutStretch()
    {
        BodyId anchor = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, 0) });
        BodyId bob = _world.Bodies.Create(new BodyDef
        {
            Position = new Vec2(2, 0),
            LinearVelocity = new Vec2(0, 2),
        });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, bob, new Vec2(0, 0), 0.25f);

        _world.Joints.CreateDistance(_world.Bodies, anchor, bob, new Vec2(0, 0), new Vec2(0, 0), 2f);

        float maxDeviation = 0f;
        float maxX = 0f;
        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
            Vec2 p = _world.Bodies.GetTransform(bob).Position;
            maxDeviation = MathF.Max(maxDeviation, MathF.Abs(p.LengthSquared() - 4f));
            maxX = MathF.Max(maxX, MathF.Abs(p.X - 2f));
        }

        maxDeviation.Should().BeLessThan(0.05f);
        maxX.Should().BeGreaterThan(0.1f);
    }

    [Fact]
    public void DistanceJoint_Create_WakesBodies()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(3, 0));

        _world.Bodies.SetAwake(a, false);
        _world.Bodies.SetAwake(b, false);

        _world.Joints.CreateDistance(_world.Bodies, a, b, new Vec2(0, 0), new Vec2(0, 0), 3f);

        _world.Bodies.IsAwake(a).Should().BeTrue();
        _world.Bodies.IsAwake(b).Should().BeTrue();
    }

    [Fact]
    public void DistanceJoint_Destroy_FreesBodies()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(3, 0));

        JointId joint = _world.Joints.CreateDistance(_world.Bodies, a, b, new Vec2(0, 0), new Vec2(0, 0), 3f);
        _world.Joints.Destroy(joint);

        _world.Joints.IsValid(joint).Should().BeFalse();
        _world.Joints.Count.Should().Be(0);

        _world.Bodies.SetLinearVelocity(a, new Vec2(-2, 0));
        _world.Bodies.SetLinearVelocity(b, new Vec2(2, 0));
        for (int i = 0; i < 60; i++)
        {
            _world.Step(Dt);
        }

        Vec2 pa = _world.Bodies.GetTransform(a).Position;
        Vec2 pb = _world.Bodies.GetTransform(b).Position;
        (pb - pa).LengthSquared().Should().BeGreaterThan(9f);
    }

    [Fact]
    public void DestroyBody_DropsJoints()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(3, 0));

        _world.Joints.CreateDistance(_world.Bodies, a, b, new Vec2(0, 0), new Vec2(0, 0), 3f);
        _world.Joints.Count.Should().Be(1);

        _world.DestroyBody(a);

        _world.Joints.Count.Should().Be(0);
        _world.Bodies.IsValid(b).Should().BeTrue();
    }

    [Fact]
    public void JointedPair_SleepsTogether()
    {
        _world.Gravity = Vec2.Zero;
        BodyId a = AddBall(new Vec2(0, 0), 0.25f);
        BodyId b = AddBall(new Vec2(1, 0), 0.25f);

        _world.Joints.CreateDistance(_world.Bodies, a, b, new Vec2(0, 0), new Vec2(0, 0), 1f);

        for (int i = 0; i < 120; i++)
        {
            _world.Step(Dt);
        }

        _world.Bodies.IsAwake(a).Should().BeFalse();
        _world.Bodies.IsAwake(b).Should().BeFalse();
    }

    [Fact]
    public void CreateDistance_RejectsBadInput()
    {
        BodyId a = AddBall(new Vec2(0, 0));
        BodyId b = AddBall(new Vec2(3, 0));

        Action negative = () => _world.Joints.CreateDistance(_world.Bodies, a, b, new Vec2(0, 0), new Vec2(0, 0), -1f);
        Action nan = () => _world.Joints.CreateDistance(_world.Bodies, a, b, new Vec2(0, 0), new Vec2(0, 0), float.NaN);
        Action dead = () => _world.Joints.CreateDistance(_world.Bodies, a, BodyId.FromRaw(999u), new Vec2(0, 0), new Vec2(0, 0), 1f);

        negative.Should().Throw<InvalidOperationException>();
        nan.Should().Throw<InvalidOperationException>();
        dead.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void JointId_Compares()
    {
        var first = new JointId(JointKind.Distance, 2u);
        var second = new JointId(JointKind.Distance, 5u);

        (first < second).Should().BeTrue();
        first.Kind.Should().Be(JointKind.Distance);
        first.SlotIndex.Should().Be(2u);
        first.ToString().Should().Be("Joint(Distance:2)");
        first.IsValid.Should().BeTrue();
        JointId.Invalid.IsValid.Should().BeFalse();
    }
}
