namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public sealed class IslandSleepTests : IDisposable
{
    private const float Dt = 1f / 60f;

    private readonly World _world;

    public IslandSleepTests()
    {
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DefaultPolicy_MatchesBox2DQuietSemantics()
    {
        DefaultSleepPolicy.IsQuiet(new Vec2(0, 0), 0f, 0.0001f, 0.001f).Should().BeTrue();
        DefaultSleepPolicy.IsQuiet(new Vec2(1, 0), 0f, 0.0001f, 0.001f).Should().BeFalse();
        DefaultSleepPolicy.IsQuiet(new Vec2(0, 0), 1f, 0.0001f, 0.001f).Should().BeFalse();

        DefaultSleepPolicy.UpdateSleepTimer(0.2f, Dt, true).Should().BeApproximately(0.2f + Dt, 1e-6f);
        DefaultSleepPolicy.UpdateSleepTimer(0.2f, Dt, false).Should().Be(0f);

        DefaultSleepPolicy.ShouldSleep(0.5f, 0.5f, false).Should().BeTrue();
        DefaultSleepPolicy.ShouldSleep(0.4f, 0.5f, false).Should().BeFalse();
        DefaultSleepPolicy.ShouldSleep(99f, 0.5f, true).Should().BeFalse();
    }

    [Fact]
    public void AggressivePolicy_SleepsSooner()
    {
        AggressiveSleepPolicy.UpdateSleepTimer(0f, Dt, true).Should().BeApproximately(Dt * 1.25f, 1e-6f);
        AggressiveSleepPolicy.ShouldSleep(0.4f, 0.5f, false).Should().BeTrue();
        AggressiveSleepPolicy.ShouldSleep(0.3f, 0.5f, false).Should().BeFalse();

        Vec2 drift = new(0.011f, 0);
        DefaultSleepPolicy.IsQuiet(drift, 0f, 0.0001f, 0.001f).Should().BeFalse();
        AggressiveSleepPolicy.IsQuiet(drift, 0f, 0.0001f, 0.001f).Should().BeTrue();
    }

    [Fact]
    public void Configuration_RejectsBadTuning()
    {
        Action linear = () => _ = new IslandSolverConfiguration { LinearTolerance = -1f };
        Action angular = () => _ = new IslandSolverConfiguration { AngularTolerance = -1f };
        Action sleep = () => _ = new IslandSolverConfiguration { TimeToSleep = -1f };
        Action capacity = () => _ = new IslandSolverConfiguration { InitialCapacity = 0 };

        linear.Should().Throw<ArgumentOutOfRangeException>();
        angular.Should().Throw<ArgumentOutOfRangeException>();
        sleep.Should().Throw<ArgumentOutOfRangeException>();
        capacity.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Summary_DescribesEveryOutcome()
    {
        new IslandEvaluationSummary(IslandEvaluationStatus.Sleeping, 2, 0.6f, (SlotIndex)3).IsSleeping.Should().BeTrue();
        new IslandEvaluationSummary(IslandEvaluationStatus.AwakeActive, 1, 0f, (SlotIndex)0).IsSleeping.Should().BeFalse();

        new IslandEvaluationSummary(IslandEvaluationStatus.BlockedByKinematic, 1, 0f, (SlotIndex)0)
            .DescribeOutcome().Should().Contain("kinematic");
        new IslandEvaluationSummary(IslandEvaluationStatus.DisallowedSleep, 1, 0f, (SlotIndex)0)
            .DescribeOutcome().Should().Contain("disallow");
        new IslandEvaluationSummary(IslandEvaluationStatus.Sleeping, 1, 1f, (SlotIndex)0)
            .DescribeOutcome().Should().Contain("sleep");
        new IslandEvaluationSummary(IslandEvaluationStatus.AwakeActive, 1, 0f, (SlotIndex)0)
            .DescribeOutcome().Should().Contain("awake");
    }

    [Fact]
    public void SlotIndex_RoundTrips()
    {
        SlotIndex slot = (SlotIndex)7;
        slot.IsValid.Should().BeTrue();
        SlotIndex.Invalid.IsValid.Should().BeFalse();

        BodyId body = BodyId.FromSlotIndex(slot);
        body.ToSlotIndex().Should().Be(slot);
    }

    [Fact]
    public void RestingStack_SleepsAndReportsMetrics()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(0, -1) });
        _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, ground,
            new Vec2[] { new(-10, -0.5f), new(10, -0.5f), new(10, 0.5f), new(-10, 0.5f) });

        BodyId box = _world.Bodies.Create(new BodyDef { Position = new Vec2(0, 1) });
        _world.Shapes.AttachPolygon(
            _world.Bodies, _world.Broadphase, box,
            new Vec2[] { new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f) });

        for (int i = 0; i < 240; i++)
        {
            _world.Step(Dt);
        }

        _world.Bodies.IsAwake(box).Should().BeFalse();
        _world.IslandMetrics.TotalBodiesPutToSleep.Should().BeGreaterThan(0);
        _world.IslandMetrics.TotalIslandsProcessed.Should().BeGreaterThan(0);
    }
}
