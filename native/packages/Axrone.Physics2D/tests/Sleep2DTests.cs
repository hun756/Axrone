namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class Sleep2DTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public Sleep2DTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private BodyId AddBox(Vec2 center, Vec2 halfExtents, BodyType type = BodyType.Dynamic)
    {
        BodyId body = _world.Bodies.Create(new BodyDef { Type = type, Position = center });
        _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, body,
            new Vec2[]
            {
                new(-halfExtents.X, -halfExtents.Y),
                new(halfExtents.X, -halfExtents.Y),
                new(halfExtents.X, halfExtents.Y),
                new(-halfExtents.X, halfExtents.Y),
            });
        return body;
    }

    private static void Settle(World world, int steps = 120)
    {
        for (int i = 0; i < steps; i++)
        {
            world.Step(Dt);
        }
    }

    [Fact]
    public void RestingBox_FallsAsleep()
    {
        AddBox(new Vec2(0, -1), new Vec2(10, 1), BodyType.Static);
        BodyId box = AddBox(new Vec2(0, 0.4f), new Vec2(0.5f, 0.5f));

        Settle(_world);

        _world.Bodies.IsAwake(box).Should().BeFalse();
        _world.Bodies.GetLinearVelocity(box).Should().Be(Vec2.Zero);
        float y = _world.Bodies.GetTransform(box).Position.Y;

        for (int i = 0; i < 60; i++)
        {
            _world.Step(Dt);
        }

        _world.Bodies.GetTransform(box).Position.Y.Should().BeApproximately(y, 1e-6f);
        _world.Bodies.IsAwake(box).Should().BeFalse();
    }

    [Fact]
    public void StackedPair_SleepsTogether()
    {
        AddBox(new Vec2(0, -1), new Vec2(10, 1), BodyType.Static);
        BodyId lower = AddBox(new Vec2(0, 0.4f), new Vec2(0.5f, 0.5f));
        BodyId upper = AddBox(new Vec2(0, 1.35f), new Vec2(0.5f, 0.5f));

        Settle(_world, 180);

        _world.Bodies.IsAwake(lower).Should().BeFalse();
        _world.Bodies.IsAwake(upper).Should().BeFalse();
    }

    [Fact]
    public void NewContact_WakesSleeper()
    {
        AddBox(new Vec2(0, -1), new Vec2(10, 1), BodyType.Static);
        BodyId box = AddBox(new Vec2(0, 0.4f), new Vec2(0.5f, 0.5f));

        Settle(_world);
        _world.Bodies.IsAwake(box).Should().BeFalse();

        AddBox(new Vec2(0.2f, 0.6f), new Vec2(0.5f, 0.5f));
        _world.Step(Dt);

        _world.Bodies.IsAwake(box).Should().BeTrue();
    }

    [Fact]
    public void Kinematic_NeverSleeps()
    {
        BodyId body = _world.Bodies.Create(new BodyDef
        {
            Type = BodyType.Kinematic,
            LinearVelocity = new Vec2(0.001f, 0),
        });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, body, new Vec2(0, 0), 0.5f);

        Settle(_world);

        _world.Bodies.IsAwake(body).Should().BeTrue();
    }

    [Fact]
    public void AllowSleepFalse_StaysAwake()
    {
        AddBox(new Vec2(0, -1), new Vec2(10, 1), BodyType.Static);
        BodyId box = _world.Bodies.Create(new BodyDef { Position = new Vec2(0, 0.4f), AllowSleep = false });
        _world.Shapes.AttachCircle(_world.Bodies, _world.Broadphase, box, new Vec2(0, 0), 0.5f);

        Settle(_world);

        _world.Bodies.IsAwake(box).Should().BeTrue();
    }

    [Fact]
    public void WorldDef_BadSleepTuning_Throws()
    {
        Action time = () => _ = new WorldDef { TimeToSleep = 0f };
        Action linear = () => _ = new WorldDef { LinearSleepTolerance = -1f };
        Action angular = () => _ = new WorldDef { AngularSleepTolerance = float.NaN };

        time.Should().Throw<InvalidOperationException>();
        linear.Should().Throw<InvalidOperationException>();
        angular.Should().Throw<InvalidOperationException>();
    }
}
