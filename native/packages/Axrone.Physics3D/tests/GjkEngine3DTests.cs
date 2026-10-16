using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class GjkEngine3DTests
{
    [Fact]
    public void Policies_Diverge_OnNearTouchingPair()
    {
        var a = new Sphere3(Vec3.Zero, 1.0f);
        var b = new Sphere3(new Vec3(2.01f, 0.0f, 0.0f), 1.0f);

        GjkResult fast = Gjk3D.ComputeDistance<Sphere3, Sphere3, FastGjkPolicy, NullGjkObserver>(a, b);
        GjkResult precise = Gjk3D.ComputeDistance<Sphere3, Sphere3, HighPrecisionGjkPolicy, NullGjkObserver>(a, b);

        fast.IsDisjoint.Should().BeTrue();
        precise.IsDisjoint.Should().BeTrue();
        fast.Iterations.Value.Should().BeLessThanOrEqualTo(precise.Iterations.Value);
        precise.Distance.Value.Value.Should().BeApproximately(0.01f, 1e-5f);
    }

    [Fact]
    public void Overlap_Reports_TouchingWithZeroDistance()
    {
        var a = new Sphere3(Vec3.Zero, 1.0f);
        var b = new Sphere3(new Vec3(1.0f, 0.0f, 0.0f), 1.0f);

        GjkResult result = Gjk3D.ComputeDistance<Sphere3, Sphere3, StandardGjkPolicy, NullGjkObserver>(a, b);

        result.IsOverlapping.Should().BeTrue();
        result.Distance.Should().Be(Distance.Zero);
    }

    [Fact]
    public void Observer_Counts_Iterations()
    {
        var a = new Sphere3(Vec3.Zero, 1.0f);
        var b = new Sphere3(new Vec3(5.0f, 0.0f, 0.0f), 1.0f);

        TelemetryGjkObserver.TelemetryState = new GjkTelemetryState();
        GjkResult result = Gjk3D.ComputeDistance<Sphere3, Sphere3, StandardGjkPolicy, TelemetryGjkObserver>(a, b);

        result.IsDisjoint.Should().BeTrue();
        TelemetryGjkObserver.TelemetryState.TotalQueries.Should().Be(1);
        TelemetryGjkObserver.TelemetryState.TotalIterations.Should().Be(result.Iterations.Value);
        TelemetryGjkObserver.TelemetryState.TotalDisjoint.Should().Be(1);
    }

    [Fact]
    public void Builder_Pipeline_Executes_WithPolicySwap()
    {
        var a = new Box3(Vec3.Zero, new Vec3(1.0f, 1.0f, 1.0f));
        var b = new Box3(new Vec3(5.0f, 0.0f, 0.0f), new Vec3(1.0f, 1.0f, 1.0f));

        GjkResult result = GjkQuery.Create()
            .WithShapeA(a)
            .WithShapeB(b)
            .WithPolicy<FastGjkPolicy>()
            .WithIterationLimit(IterationLimit.From(12))
            .Execute();

        result.IsDisjoint.Should().BeTrue();
        result.Distance.Value.Value.Should().BeApproximately(3.0f, 1e-3f);
    }

    [Fact]
    public void Match_Selects_DisjointArm()
    {
        var a = new Sphere3(Vec3.Zero, 1.0f);
        var b = new Sphere3(new Vec3(5.0f, 0.0f, 0.0f), 1.0f);

        GjkResult result = GjkQuery.Create().WithShapeA(a).WithShapeB(b).Execute();

        float distance = result.Match(
            onDisjoint: static (d, pa, pb, axis, it) => d.Value.Value,
            onOverlapping: static (pa, pb, it) => -1.0f,
            onMaxIterations: static (d, pa, pb, it) => -2.0f,
            onDegenerate: static it => -3.0f);

        distance.Should().BeApproximately(3.0f, 1e-4f);
    }

    [Fact]
    public void InvalidLimit_Throws_OnConstructionAndInit()
    {
        var actFrom = () => IterationLimit.From(0);
        actFrom.Should().Throw<ArgumentOutOfRangeException>();

        var a = new Sphere3(Vec3.Zero, 1.0f);
        var actInit = () => GjkQuery.Create().WithShapeA(a).WithShapeB(a)
            .WithIterationLimit(new IterationLimit(0)).Execute();
        actInit.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Capture_Finds_Tetrahedron_OnOverlap()
    {
        var lower = new OrientedShape<Cylinder3>(
            new Cylinder3(Vec3.Zero, 0.5f, 1.0f, ShapeAxis3D.Y), RigidTransform3D.Identity);
        var upper = new OrientedShape<Cylinder3>(
            new Cylinder3(new Vec3(0.0f, 0.8f, 0.0f), 0.5f, 1.0f, ShapeAxis3D.Y), RigidTransform3D.Identity);

        Span<SimplexVertex> simplex = stackalloc SimplexVertex[4];
        bool captured = Gjk3D.TryCaptureTetrahedron<OrientedShape<Cylinder3>, OrientedShape<Cylinder3>, StandardGjkPolicy>(
            lower, upper, simplex, out int count);

        captured.Should().BeTrue();
        count.Should().Be(4);
    }
}
