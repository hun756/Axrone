using Axrone.Numeric;
using Axrone.Physics2D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics2D.Tests;

public class GjkEngineTests
{
    [Fact]
    public void Policies_Diverge_OnNearTouchingPair()
    {
        var a = new Circle2(new Vec2(0.0f, 0.0f), 1.0f);
        var b = new Circle2(new Vec2(2.01f, 0.0f), 1.0f);

        GjkResult fast = GjkEngine2D.ComputeDistance<Circle2, Circle2, FastGjkPolicy, NullGjkObserver>(a, b);
        GjkResult precise = GjkEngine2D.ComputeDistance<Circle2, Circle2, HighPrecisionGjkPolicy, NullGjkObserver>(a, b);

        fast.IsDisjoint.Should().BeTrue();
        precise.IsDisjoint.Should().BeTrue();
        fast.Iterations.Value.Should().BeLessThanOrEqualTo(precise.Iterations.Value);
        fast.Distance.Value.Value.Should().BeApproximately(0.01f, 1e-3f);
        precise.Distance.Value.Value.Should().BeApproximately(0.01f, 1e-5f);
    }

    [Fact]
    public void Overlap_Reports_TouchingWithZeroDistance()
    {
        var a = new Circle2(new Vec2(0.0f, 0.0f), 1.0f);
        var b = new Circle2(new Vec2(1.0f, 0.0f), 1.0f);

        GjkResult result = GjkEngine2D.ComputeDistance<Circle2, Circle2, StandardGjkPolicy, NullGjkObserver>(a, b);

        result.IsOverlapping.Should().BeTrue();
        result.Distance.Should().Be(Distance.Zero);
    }

    [Fact]
    public void Observer_Counts_Iterations()
    {
        var a = new Circle2(new Vec2(0.0f, 0.0f), 1.0f);
        var b = new Circle2(new Vec2(5.0f, 0.0f), 1.0f);

        TelemetryGjkObserver.TelemetryState = new GjkTelemetryState();
        GjkResult result = GjkEngine2D.ComputeDistance<Circle2, Circle2, StandardGjkPolicy, TelemetryGjkObserver>(a, b);

        result.IsDisjoint.Should().BeTrue();
        TelemetryGjkObserver.TelemetryState.TotalQueries.Should().Be(1);
        TelemetryGjkObserver.TelemetryState.TotalIterations.Should().Be(result.Iterations.Value);
        TelemetryGjkObserver.TelemetryState.TotalDisjoint.Should().Be(1);
    }

    [Fact]
    public void Builder_Pipeline_Executes_WithPolicySwap()
    {
        var a = new Circle2(new Vec2(0.0f, 0.0f), 1.0f);
        var b = new Circle2(new Vec2(5.0f, 0.0f), 1.0f);

        GjkResult result = GjkQuery.Create()
            .WithShapeA(a)
            .WithShapeB(b)
            .WithPolicy<FastGjkPolicy>()
            .WithIterationLimit(IterationLimit.From(12))
            .Execute();

        result.IsDisjoint.Should().BeTrue();
        result.Distance.Value.Value.Should().BeApproximately(3.0f, 1e-4f);
    }

    [Fact]
    public void Match_Selects_DisjointArm()
    {
        var a = new Circle2(new Vec2(0.0f, 0.0f), 1.0f);
        var b = new Circle2(new Vec2(5.0f, 0.0f), 1.0f);

        GjkResult result = GjkQuery.Create().WithShapeA(a).WithShapeB(b).Execute();

        float distance = result.Match(
            onDisjoint: static (d, pa, pb, axis, it) => d.Value.Value,
            onOverlapping: static (pa, pb, it) => -1.0f,
            onMaxIterations: static (d, pa, pb, it) => -2.0f,
            onDegenerate: static it => -3.0f);

        distance.Should().BeApproximately(3.0f, 1e-4f);
    }

    [Fact]
    public void Match_Selects_OverlappingArm()
    {
        var a = new Circle2(new Vec2(0.0f, 0.0f), 1.0f);
        var b = new Circle2(new Vec2(1.0f, 0.0f), 1.0f);

        GjkResult result = GjkQuery.Create().WithShapeA(a).WithShapeB(b).Execute();

        string arm = result.Match(
            onDisjoint: static (d, pa, pb, axis, it) => "disjoint",
            onOverlapping: static (pa, pb, it) => "overlap",
            onMaxIterations: static (d, pa, pb, it) => "budget",
            onDegenerate: static it => "degenerate");

        arm.Should().Be("overlap");
    }

    [Fact]
    public void InvalidLimit_Throws_OnConstructionAndInit()
    {
        var actFrom = () => IterationLimit.From(0);
        actFrom.Should().Throw<ArgumentOutOfRangeException>();

        var a = new Circle2(new Vec2(0.0f, 0.0f), 1.0f);
        var actInit = () => GjkQuery.Create().WithShapeA(a).WithShapeB(a)
            .WithIterationLimit(new IterationLimit(0)).Execute();
        actInit.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Nominals_Guard_TheirInvariants()
    {
        var actDistance = () => Distance.From(-1.0f);
        actDistance.Should().Throw<ArgumentOutOfRangeException>();

        Distance.From(2.0f).Squared().Value.Value.Should().BeApproximately(4.0f, 1e-6f);
        DistanceSquared.From(9.0f).ToDistance().Value.Value.Should().BeApproximately(3.0f, 1e-6f);
        IterationLimit.From(5).HasReached(new IterationCount(5)).Should().BeTrue();
        Scalar.From(2.0f).Sqrt().Value.Value.Should().BeApproximately(1.4142135f, 1e-6f);
    }
}
