namespace Axrone.Geometry.Tests;

using Axrone.Geometry;
using Axrone.Numeric;

using FluentAssertions;
using Xunit;

public class RaycastingTests
{
    private static readonly Triangle s_triangle = new(
        new Vec3(0, 0, 5),
        new Vec3(1, 0, 5),
        new Vec3(0, 1, 5),
        7);

    private static Ray UnitRay(Vec3 origin, Vec3 direction) =>
        new(origin, Vec3.Normalize(direction));

    [Fact]
    public void PrimitiveId_NoneIsInvalid()
    {
        PrimitiveId.None.IsValid.Should().BeFalse();
        new PrimitiveId(3).IsValid.Should().BeTrue();
        (new PrimitiveId(2) < new PrimitiveId(5)).Should().BeTrue();
    }

    [Fact]
    public void PrimitiveCount_Adds()
    {
        (PrimitiveCount.Zero + 4).Value.Should().Be(4);
    }

    [Fact]
    public void Ray_Defaults_GuardSelfHits()
    {
        var ray = new Ray(new Vec3(0, 0, 0), new Vec3(1, 0, 0));

        ray.MinDistance.Should().Be(Distance.DefaultEpsilon);
        ray.MaxDistance.Should().Be(Distance.PositiveInfinity);
        ray.PointAt(2f).Should().Be(new Vec3(2, 0, 0));
    }

    [Fact]
    public void Ray_InvalidWindow_Throws()
    {
        Action negative = () => _ = new Ray(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Distance(-1f), Distance.PositiveInfinity);
        Action inverted = () => _ = new Ray(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Distance(5f), new Distance(2f));

        negative.Should().Throw<ArgumentOutOfRangeException>();
        inverted.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Ray_AxisAligned_UsesGuardedReciprocal()
    {
        var ray = new Ray(new Vec3(0, 0, 0), new Vec3(0, 0, 1));

        float.Abs(ray.InvDirection.X).Should().Be(1e9f);
        ray.InvDirection.Z.Should().Be(1f);
    }

    [Fact]
    public void Triangle_ComputesBoundsCentroidAndId()
    {
        Aabb3D bounds = Triangle.ComputeBounds(s_triangle);

        bounds.Min.Should().Be(new Vec3(0, 0, 5));
        bounds.Max.Should().Be(new Vec3(1, 1, 5));
        Triangle.ComputeCentroid(s_triangle).X.Should().BeApproximately(1f / 3f, 1e-6f);
        Triangle.GetId(s_triangle).Should().Be(new PrimitiveId(7));
    }

    [Fact]
    public void Triangle_FrontalHit_ReportsPointAndFacingNormal()
    {
        Ray ray = UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1));

        Triangle.TryIntersect(ray, s_triangle, out PrimitiveHit hit).Should().BeTrue();

        hit.IsHit.Should().BeTrue();
        hit.Distance.Value.Should().BeApproximately(5f, 1e-4f);
        hit.Point.Z.Should().BeApproximately(5f, 1e-4f);
        hit.Normal.Should().Be(new Vec3(0, 0, -1));
        hit.PrimitiveId.Should().Be(new PrimitiveId(7));
    }

    [Fact]
    public void Triangle_BackfaceHit_IsTwoSided()
    {
        Ray ray = UnitRay(new Vec3(0.1f, 0.1f, 10), new Vec3(0, 0, -1));

        Triangle.TryIntersect(ray, s_triangle, out PrimitiveHit hit).Should().BeTrue();

        hit.Distance.Value.Should().BeApproximately(5f, 1e-4f);
        hit.Normal.Should().Be(new Vec3(0, 0, 1));
    }

    [Fact]
    public void Triangle_Misses_OutsideParallelBehindAndWindow()
    {
        Ray offTarget = UnitRay(new Vec3(5, 5, 0), new Vec3(0, 0, 1));
        Ray parallel = UnitRay(new Vec3(0.1f, 0.1f, 0), new Vec3(1, 0, 0));
        Ray behind = UnitRay(new Vec3(0.1f, 0.1f, 10), new Vec3(0, 0, 1));
        var clipped = new Ray(new Vec3(0.1f, 0.1f, 0), new Vec3(0, 0, 1), Distance.DefaultEpsilon, new Distance(1f));

        Triangle.TryIntersect(offTarget, s_triangle, out _).Should().BeFalse();
        Triangle.TryIntersect(parallel, s_triangle, out _).Should().BeFalse();
        Triangle.TryIntersect(behind, s_triangle, out _).Should().BeFalse();
        Triangle.TryIntersect(clipped, s_triangle, out _).Should().BeFalse();
    }

    [Fact]
    public void Sphere_Hit_ReportsSurfaceContact()
    {
        var sphere = new Sphere(new Vec3(0, 0, 5), 1f, 3);
        Ray ray = UnitRay(new Vec3(0, 0, 0), new Vec3(0, 0, 1));

        Sphere.TryIntersect(ray, sphere, out PrimitiveHit hit).Should().BeTrue();

        hit.Distance.Value.Should().BeApproximately(4f, 1e-4f);
        hit.Point.Should().Be(new Vec3(0, 0, 4));
        hit.Normal.Should().Be(new Vec3(0, 0, -1));
        hit.PrimitiveId.Should().Be(new PrimitiveId(3));
    }

    [Fact]
    public void Sphere_FromInside_ReportsExit()
    {
        var sphere = new Sphere(new Vec3(0, 0, 5), 1f, 3);
        Ray ray = UnitRay(new Vec3(0, 0, 5), new Vec3(0, 0, 1));

        Sphere.TryIntersect(ray, sphere, out PrimitiveHit hit).Should().BeTrue();

        hit.Distance.Value.Should().BeApproximately(1f, 1e-4f);
    }

    [Fact]
    public void Sphere_Miss_And_NegativeRadius()
    {
        var sphere = new Sphere(new Vec3(0, 0, 5), 1f, 3);
        Ray ray = UnitRay(new Vec3(0, 0, 0), new Vec3(0, 1, 0));

        Sphere.TryIntersect(ray, sphere, out PrimitiveHit hit).Should().BeFalse();
        hit.IsHit.Should().BeFalse();

        Action negative = () => _ = new Sphere(new Vec3(0, 0, 0), -1f, 0);
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Sphere_ComputesBoundsAndCentroid()
    {
        var sphere = new Sphere(new Vec3(1, 2, 3), 2f, 9);

        Aabb3D bounds = Sphere.ComputeBounds(sphere);

        bounds.Min.Should().Be(new Vec3(-1, 0, 1));
        bounds.Max.Should().Be(new Vec3(3, 4, 5));
        Sphere.ComputeCentroid(sphere).Should().Be(new Vec3(1, 2, 3));
        Sphere.GetId(sphere).Should().Be(new PrimitiveId(9));
    }

    [Fact]
    public void LinearBvhNode_Slab_HonoursWindow()
    {
        var node = new LinearBvhNode(
            new Aabb3D(new Vec3(2, -1, -1), new Vec3(3, 1, 1)),
            new NodeIndex(4),
            0);

        node.IsLeaf.Should().BeFalse();
        Ray open = UnitRay(new Vec3(0, 0, 0), new Vec3(1, 0, 0));

        node.Intersects(open, out Distance enter).Should().BeTrue();
        enter.Value.Should().BeApproximately(2f, 1e-4f);

        var tooShort = new Ray(new Vec3(0, 0, 0), new Vec3(1, 0, 0), Distance.DefaultEpsilon, new Distance(1f));
        var tooLate = new Ray(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Distance(5f), Distance.PositiveInfinity);

        node.Intersects(tooShort, out _).Should().BeFalse();
        node.Intersects(tooLate, out _).Should().BeFalse();
    }

    [Fact]
    public void ClosestHitPolicy_TightensWindow()
    {
        ClosestHitPolicy.ShouldPrune(new Distance(5f), new Distance(3f)).Should().BeTrue();
        ClosestHitPolicy.ShouldPrune(new Distance(2f), new Distance(3f)).Should().BeFalse();

        var closest = PrimitiveHit.None;
        var max = Distance.PositiveInfinity;
        var candidate = new PrimitiveHit(new Vec3(0, 0, 2), new Vec3(0, 0, -1), new Distance(2f), new PrimitiveId(1));

        ClosestHitPolicy.OnPrimitiveHit(ref closest, ref max, candidate).Should().BeTrue();
        max.Should().Be(new Distance(2f));
        closest.PrimitiveId.Should().Be(new PrimitiveId(1));
        ClosestHitPolicy.TerminatesOnFirstHit.Should().BeFalse();
    }

    [Fact]
    public void AnyHitPolicy_StopsOnFirst()
    {
        var closest = PrimitiveHit.None;
        var max = Distance.PositiveInfinity;
        var candidate = new PrimitiveHit(new Vec3(0, 0, 2), new Vec3(0, 0, -1), new Distance(2f), new PrimitiveId(1));

        AnyHitPolicy.OnPrimitiveHit(ref closest, ref max, candidate).Should().BeFalse();
        closest.PrimitiveId.Should().Be(new PrimitiveId(1));
        AnyHitPolicy.TerminatesOnFirstHit.Should().BeTrue();
    }

    [Fact]
    public void HitBufferCollector_StopsWhenFull()
    {
        Span<PrimitiveHit> buffer = stackalloc PrimitiveHit[2];
        var collector = new HitBufferCollector(buffer);
        var hit = new PrimitiveHit(new Vec3(0, 0, 1), new Vec3(0, 0, -1), new Distance(1f), new PrimitiveId(1));

        collector.OnHit(hit).Should().BeTrue();
        collector.OnHit(hit).Should().BeTrue();
        collector.OnHit(hit).Should().BeFalse();

        collector.HasHit.Should().BeTrue();
        collector.Count.Should().Be(2);
        collector.Hits.Length.Should().Be(2);
        collector.Hit.PrimitiveId.Should().Be(new PrimitiveId(1));
        collector.MaxDistance.Should().Be(Distance.PositiveInfinity);
    }

    [Fact]
    public void EmptyCollector_HasMissHit()
    {
        Span<PrimitiveHit> buffer = stackalloc PrimitiveHit[2];
        var collector = new HitBufferCollector(buffer);

        collector.HasHit.Should().BeFalse();
        collector.Hit.IsHit.Should().BeFalse();
    }

    [Fact]
    public void RayQueryResult_Match_FoldsBranches()
    {
        RayQueryResult miss = RayQueryResult.Miss();
        var contact = new PrimitiveHit(new Vec3(0, 0, 2), new Vec3(0, 0, -1), new Distance(2f), new PrimitiveId(1));
        RayQueryResult hit = RayQueryResult.HitResult(contact);
        RayQueryResult occluded = RayQueryResult.Occluded(contact);

        miss.IsMiss.Should().BeTrue();
        hit.IsHit.Should().BeTrue();
        occluded.IsOccluded.Should().BeTrue();
        hit.Hit.PrimitiveId.Should().Be(new PrimitiveId(1));

        static string OnHit(int ctx, PrimitiveHit h) => $"hit{ctx}:{h.PrimitiveId.Value}";
        static string OnMiss(int ctx) => $"miss{ctx}";
        static string OnOccluded(int ctx, PrimitiveHit h) => $"occ{ctx}:{h.PrimitiveId.Value}";

        miss.Match(0, OnHit, OnMiss, OnOccluded).Should().Be("miss0");
        hit.Match(1, OnHit, OnMiss, OnOccluded).Should().Be("hit1:1");
        occluded.Match(2, OnHit, OnMiss, OnOccluded).Should().Be("occ2:1");

        Action access = () => _ = miss.Hit;
        access.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Ray_Equality_ComparesWindow()
    {
        var left = new Ray(new Vec3(0, 0, 0), new Vec3(1, 0, 0));
        var right = new Ray(new Vec3(0, 0, 0), new Vec3(1, 0, 0));
        var narrower = new Ray(new Vec3(0, 0, 0), new Vec3(1, 0, 0), Distance.DefaultEpsilon, new Distance(5f));

        (left == right).Should().BeTrue();
        (left != narrower).Should().BeTrue();
    }
}
