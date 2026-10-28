namespace Axrone.Raycast.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class RaycastContinuousTests
{
    [Fact]
    public void SphereSweepReportsFirstTimeOfImpact()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        var raycaster = new ContinuousRaycaster3D(engine);
        var query = new RaycastSweepQuery3D(new Vec3(-5f, 0f, 0f), new Vec3(5f, 0f, 0f));

        TimeOfImpact3D impact = raycaster.SweepSphere(in query, 1f);

        impact.Hit.Should().BeTrue();
        impact.Time.Should().BeApproximately(0.3f, 1e-4f);
        impact.Normal.X.Should().BeApproximately(-1f, 1e-4f);
        impact.WitnessCast.X.Should().BeApproximately(-2f, 1e-4f);
        impact.WitnessTarget.X.Should().BeApproximately(-1f, 1e-4f);

        var shortSweep = new RaycastSweepQuery3D(new Vec3(-5f, 0f, 0f), new Vec3(0f, 0f, 0f));
        TimeOfImpact3D boundary = raycaster.SweepSphere(in shortSweep, 1f);
        boundary.Hit.Should().BeTrue();
        boundary.Time.Should().BeApproximately(0.6f, 1e-4f);
    }

    [Fact]
    public void SphereSweepMissesWhenMovingAway()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        var raycaster = new ContinuousRaycaster3D(engine);
        var away = new RaycastSweepQuery3D(new Vec3(5f, 0f, 0f), new Vec3(6f, 0f, 0f));

        raycaster.SweepSphere(in away, 1f).Hit.Should().BeFalse();
    }

    [Fact]
    public void PointSweepAdvancesToSurfaceContact()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        var raycaster = new ContinuousRaycaster3D(engine);
        var query = new RaycastSweepQuery3D(
            new Vec3(-5f, 0f, 0f),
            new Vec3(5f, 0f, 0f),
            MaxIterations: 16,
            Tolerance: 1e-3f);

        TimeOfImpact3D impact = raycaster.SweepTest(in query);

        impact.Hit.Should().BeTrue();
        impact.Time.Should().BeApproximately(0.4f, 0.01f);
        impact.Normal.X.Should().BeApproximately(-1f, 0.01f);
    }

    [Fact]
    public void PointSweepMissesEmptySpace()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(new Vec3(0f, 10f, 0f), 1f));
        var raycaster = new ContinuousRaycaster3D(engine);
        var query = new RaycastSweepQuery3D(new Vec3(-5f, 0f, 0f), new Vec3(5f, 0f, 0f));

        raycaster.SweepTest(in query).Hit.Should().BeFalse();
    }

    [Fact]
    public void BoxSweepReportsInflatedContact()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateAabb(new Axrone.Geometry.Aabb3D(-1f, -1f, -1f, 1f, 1f, 1f)));
        var raycaster = new ContinuousRaycaster3D(engine);
        var query = new RaycastSweepQuery3D(new Vec3(-5f, 0f, 0f), new Vec3(5f, 0f, 0f));

        TimeOfImpact3D impact = raycaster.SweepBox(in query, new Vec3(0.5f));

        impact.Hit.Should().BeTrue();
        impact.Time.Should().BeApproximately(0.35f, 1e-4f);
        impact.WitnessCast.X.Should().BeApproximately(-1.5f, 1e-4f);
        impact.WitnessTarget.X.Should().BeApproximately(-1f, 1e-4f);
    }

    [Fact]
    public void ContinuousCastSamplesMovingOrigin()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        var raycaster = new ContinuousRaycaster3D(engine);

        bool found = raycaster.ContinuousCast(
            new Vec3(-5f, 0f, 0f),
            new Vec3(-5f, 1f, 0f),
            Vec3.UnitX,
            20f,
            out RaycastHit3D hit,
            samples: 10);

        found.Should().BeTrue();
        hit.Distance.Should().BeApproximately(4f, 1e-3f);

        bool missed = raycaster.ContinuousCast(
            new Vec3(-5f, 0f, 0f),
            new Vec3(-5f, 1f, 0f),
            Vec3.UnitY,
            20f,
            out _,
            samples: 10);
        missed.Should().BeFalse();
    }

    [Fact]
    public void PredictiveRaycastHitsStationaryTarget()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        var raycaster = new ContinuousRaycaster3D(engine);

        bool found = raycaster.PredictiveRaycast(
            new Vec3(-5f, 0f, 0f),
            Vec3.Zero,
            Vec3.Zero,
            Vec3.Zero,
            1f,
            0.1f,
            out RaycastHit3D hit);
        found.Should().BeTrue();
        hit.Distance.Should().BeApproximately(4f, 1e-4f);

        bool missed = raycaster.PredictiveRaycast(
            new Vec3(-5f, 0f, 0f),
            Vec3.Zero,
            new Vec3(0f, 100f, 0f),
            Vec3.Zero,
            1f,
            0.1f,
            out _);
        missed.Should().BeFalse();
    }

    [Fact]
    public void PredictiveRaycastRejectsNonPositiveStep()
    {
        using var engine = new RaycastEngine3D();
        var raycaster = new ContinuousRaycaster3D(engine);

        Action act = () => raycaster.PredictiveRaycast(
            Vec3.Zero,
            Vec3.Zero,
            new Vec3(1f, 0f, 0f),
            Vec3.Zero,
            1f,
            0f,
            out _);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DegenerateSweepIsAMiss()
    {
        using var engine = new RaycastEngine3D();
        _ = engine.Register(RaycastShape3D.CreateSphere(Vec3.Zero, 1f));
        var raycaster = new ContinuousRaycaster3D(engine);
        var still = new RaycastSweepQuery3D(Vec3.Zero, Vec3.Zero);

        raycaster.SweepSphere(in still, 1f).Hit.Should().BeFalse();
        raycaster.SweepTest(in still).Hit.Should().BeFalse();
        raycaster.SweepBox(in still, new Vec3(1f)).Hit.Should().BeFalse();
    }
}
