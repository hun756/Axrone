namespace Axrone.Raycast.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class RaycastIntersectionsTests
{
    private static readonly RaycastShapeId Id = new(7);

    [Fact]
    public void CircleHitReportsEntryPointAndUnitNormal()
    {
        var ray = new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX);
        RaycastShape2D shape = RaycastShape2D.CreateCircle(Vec2.Zero, 1f, 3u);

        RaycastIntersections.TryIntersect2D(in ray, in shape, Id, 10f, out RaycastHit2D hit).Should().BeTrue();

        hit.Shape.Should().Be(Id);
        hit.Distance.Should().BeApproximately(4f, 1e-4f);
        hit.Point.X.Should().BeApproximately(-1f, 1e-4f);
        hit.Point.Y.Should().BeApproximately(0f, 1e-4f);
        hit.Normal.X.Should().BeApproximately(-1f, 1e-4f);
        hit.LayerMask.Should().Be(3u);
        hit.Fraction.Should().BeApproximately(0.4f, 1e-4f);
    }

    [Fact]
    public void CircleMissAndDistanceBoundaryAreExact()
    {
        var ray = new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX);
        RaycastShape2D shape = RaycastShape2D.CreateCircle(Vec2.Zero, 1f);

        RaycastIntersections.TryIntersect2D(in ray, in shape, Id, 3.9f, out _).Should().BeFalse();
        RaycastIntersections.TryIntersect2D(in ray, in shape, Id, 4f, out RaycastHit2D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(4f, 1e-4f);

        var sideways = new Ray2D(new Vec2(-5f, 0f), Vec2.UnitY);
        RaycastIntersections.TryIntersect2D(in sideways, in shape, Id, 100f, out _).Should().BeFalse();
    }

    [Fact]
    public void AabbFromInsideReportsExitFace()
    {
        var shape = RaycastShape2D.CreateAabb(new Aabb2D(-1f, -1f, 1f, 1f));
        var outside = new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX);
        RaycastIntersections.TryIntersect2D(in outside, in shape, Id, 10f, out RaycastHit2D entry).Should().BeTrue();
        entry.Distance.Should().BeApproximately(4f, 1e-4f);
        entry.Normal.X.Should().BeApproximately(-1f, 1e-4f);

        var inside = new Ray2D(Vec2.Zero, Vec2.UnitX);
        RaycastIntersections.TryIntersect2D(in inside, in shape, Id, 10f, out RaycastHit2D exit).Should().BeTrue();
        exit.Distance.Should().BeApproximately(1f, 1e-4f);
        exit.Normal.X.Should().BeApproximately(1f, 1e-4f);

        var backwards = new Ray2D(new Vec2(-5f, 0f), Vec2.NegativeUnitX);
        RaycastIntersections.TryIntersect2D(in backwards, in shape, Id, 100f, out _).Should().BeFalse();
    }

    [Fact]
    public void OrientedBoxRespectsRotation()
    {
        RaycastShape2D shape = RaycastShape2D.CreateBox(Vec2.Zero, new Vec2(2f, 1f), MathF.PI * 0.5f);
        var ray = new Ray2D(new Vec2(0f, 5f), Vec2.NegativeUnitY);

        RaycastIntersections.TryIntersect2D(in ray, in shape, Id, 10f, out RaycastHit2D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(3f, 1e-4f);
        hit.Point.Y.Should().BeApproximately(2f, 1e-4f);
        hit.Normal.Y.Should().BeApproximately(1f, 1e-4f);

        RaycastIntersections.TryIntersect2D(in ray, in shape, Id, 2.9f, out _).Should().BeFalse();

        var reversed = new Ray2D(new Vec2(0f, 5f), Vec2.UnitY);
        RaycastIntersections.TryIntersect2D(in reversed, in shape, Id, 100f, out _).Should().BeFalse();
    }

    [Fact]
    public void SegmentHitFacesTheRayAndReverseMisses()
    {
        RaycastShape2D shape = RaycastShape2D.CreateSegment(new Vec2(5f, -1f), new Vec2(5f, 1f));
        var ray = new Ray2D(Vec2.Zero, Vec2.UnitX);

        RaycastIntersections.TryIntersect2D(in ray, in shape, Id, 10f, out RaycastHit2D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(5f, 1e-4f);
        hit.Normal.X.Should().BeApproximately(-1f, 1e-4f);

        var reverse = new Ray2D(Vec2.Zero, Vec2.NegativeUnitX);
        RaycastIntersections.TryIntersect2D(in reverse, in shape, Id, 10f, out _).Should().BeFalse();
    }

    [Fact]
    public void PolygonReturnsNearestEdgeAndMissesAway()
    {
        RaycastShape2D shape = RaycastShape2D.CreatePolygon(
        [
            new Vec2(-1f, -1f),
            new Vec2(1f, -1f),
            new Vec2(1f, 1f),
            new Vec2(-1f, 1f),
        ]);
        var ray = new Ray2D(new Vec2(-5f, 0f), Vec2.UnitX);

        RaycastIntersections.TryIntersect2D(in ray, in shape, Id, 10f, out RaycastHit2D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(4f, 1e-4f);
        hit.Normal.X.Should().BeApproximately(-1f, 1e-4f);

        var away = new Ray2D(new Vec2(-5f, 0f), Vec2.UnitY);
        RaycastIntersections.TryIntersect2D(in away, in shape, Id, 100f, out _).Should().BeFalse();
    }

    [Fact]
    public void CapsuleHitsSideAndCapsButNotOutside()
    {
        RaycastShape2D shape = RaycastShape2D.CreateCapsule(new Vec2(-1f, 0f), new Vec2(1f, 0f), 1f);
        var side = new Ray2D(new Vec2(0f, 5f), Vec2.NegativeUnitY);

        RaycastIntersections.TryIntersect2D(in side, in shape, Id, 10f, out RaycastHit2D sideHit).Should().BeTrue();
        sideHit.Distance.Should().BeApproximately(4f, 1e-4f);
        sideHit.Normal.Y.Should().BeApproximately(1f, 1e-4f);

        var cap = new Ray2D(new Vec2(-1f, 5f), Vec2.NegativeUnitY);
        RaycastIntersections.TryIntersect2D(in cap, in shape, Id, 10f, out RaycastHit2D capHit).Should().BeTrue();
        capHit.Distance.Should().BeApproximately(4f, 1e-4f);

        var outside = new Ray2D(new Vec2(-3f, 5f), Vec2.NegativeUnitY);
        RaycastIntersections.TryIntersect2D(in outside, in shape, Id, 100f, out _).Should().BeFalse();
    }

    [Fact]
    public void SphereHitAndMiss()
    {
        var ray = new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX);
        RaycastShape3D shape = RaycastShape3D.CreateSphere(Vec3.Zero, 1f, 5u);

        RaycastIntersections.TryIntersect3D(in ray, in shape, Id, 10f, out RaycastHit3D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(4f, 1e-4f);
        hit.Normal.X.Should().BeApproximately(-1f, 1e-4f);
        hit.TriangleIndex.Should().Be(-1);
        hit.HasBarycentric.Should().BeFalse();

        var sideways = new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitY);
        RaycastIntersections.TryIntersect3D(in sideways, in shape, Id, 100f, out _).Should().BeFalse();
    }

    [Fact]
    public void AabbFromInsideReportsExitFaceIn3D()
    {
        RaycastShape3D shape = RaycastShape3D.CreateAabb(new Aabb3D(-1f, -1f, -1f, 1f, 1f, 1f));
        var inside = new Ray3D(Vec3.Zero, Vec3.UnitX);

        RaycastIntersections.TryIntersect3D(in inside, in shape, Id, 10f, out RaycastHit3D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(1f, 1e-4f);
        hit.Normal.X.Should().BeApproximately(1f, 1e-4f);

        var outside = new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX);
        RaycastIntersections.TryIntersect3D(in outside, in shape, Id, 10f, out _).Should().BeTrue();
        RaycastIntersections.TryIntersect3D(in outside, in shape, Id, 3.9f, out _).Should().BeFalse();
    }

    [Fact]
    public void PlaneHitsFrontSideOnly()
    {
        RaycastShape3D plane = RaycastShape3D.CreatePlane(Vec3.Zero, Vec3.UnitY);
        var fromAbove = new Ray3D(new Vec3(0f, 5f, 0f), Vec3.NegativeUnitY);

        RaycastIntersections.TryIntersect3D(in fromAbove, in plane, Id, 10f, out RaycastHit3D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(5f, 1e-4f);
        hit.Normal.Y.Should().BeApproximately(1f, 1e-4f);

        var away = new Ray3D(new Vec3(0f, 5f, 0f), Vec3.UnitY);
        RaycastIntersections.TryIntersect3D(in away, in plane, Id, 10f, out _).Should().BeFalse();

        var fromBelow = new Ray3D(new Vec3(0f, -5f, 0f), Vec3.UnitY);
        RaycastIntersections.TryIntersect3D(in fromBelow, in plane, Id, 10f, out RaycastHit3D backHit).Should().BeTrue();
        backHit.Normal.Y.Should().BeApproximately(1f, 1e-4f);
    }

    [Fact]
    public void TriangleReportsWindingNormalAndBarycentrics()
    {
        RaycastShape3D triangle = RaycastShape3D.CreateTriangle(
            Vec3.Zero,
            new Vec3(1f, 0f, 0f),
            new Vec3(0f, 1f, 0f));
        var ray = new Ray3D(new Vec3(0.25f, 0.25f, 1f), Vec3.NegativeUnitZ);

        RaycastIntersections.TryIntersect3D(in ray, in triangle, Id, 10f, out RaycastHit3D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(1f, 1e-4f);
        hit.Normal.Z.Should().BeApproximately(1f, 1e-4f);
        hit.TriangleIndex.Should().Be(0);
        hit.Barycentric.X.Should().BeApproximately(0.25f, 1e-4f);
        hit.Barycentric.Y.Should().BeApproximately(0.25f, 1e-4f);

        var missed = new Ray3D(new Vec3(2f, 2f, 1f), Vec3.NegativeUnitZ);
        RaycastIntersections.TryIntersect3D(in missed, in triangle, Id, 10f, out _).Should().BeFalse();
    }

    [Fact]
    public void OrientedBoxRespectsRotationIn3D()
    {
        Quat rotation = Quat.CreateFromAxisAngle(Vec3.UnitZ, MathF.PI * 0.5f);
        RaycastShape3D shape = RaycastShape3D.CreateBox(Vec3.Zero, new Vec3(2f, 1f, 1f), rotation);
        var ray = new Ray3D(new Vec3(0f, 5f, 0f), Vec3.NegativeUnitY);

        RaycastIntersections.TryIntersect3D(in ray, in shape, Id, 10f, out RaycastHit3D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(3f, 1e-4f);
        hit.Normal.Y.Should().BeApproximately(1f, 1e-4f);

        RaycastIntersections.TryIntersect3D(in ray, in shape, Id, 2.9f, out _).Should().BeFalse();
    }

    [Fact]
    public void CapsuleHitsVerticalAndArbitraryAxes()
    {
        RaycastShape3D vertical = RaycastShape3D.CreateCapsule(new Vec3(0f, -1f, 0f), new Vec3(0f, 1f, 0f), 0.5f);
        var down = new Ray3D(new Vec3(0f, 5f, 0f), Vec3.NegativeUnitY);
        RaycastIntersections.TryIntersect3D(in down, in vertical, Id, 10f, out RaycastHit3D side).Should().BeTrue();
        side.Distance.Should().BeApproximately(3.5f, 1e-4f);
        side.Normal.Y.Should().BeApproximately(1f, 1e-4f);

        var outside = new Ray3D(new Vec3(1f, 5f, 0f), Vec3.NegativeUnitY);
        RaycastIntersections.TryIntersect3D(in outside, in vertical, Id, 100f, out _).Should().BeFalse();

        RaycastShape3D horizontal = RaycastShape3D.CreateCapsule(new Vec3(-1f, 0f, 0f), new Vec3(1f, 0f, 0f), 0.5f);
        RaycastIntersections.TryIntersect3D(in down, in horizontal, Id, 10f, out RaycastHit3D horizontalHit).Should().BeTrue();
        horizontalHit.Distance.Should().BeApproximately(4.5f, 1e-4f);
        horizontalHit.Normal.Y.Should().BeApproximately(1f, 1e-4f);
    }

    [Fact]
    public void SphereCastInflatesTargetAndAdjustsContactPoint()
    {
        RaycastShape3D target = RaycastShape3D.CreateSphere(Vec3.Zero, 1f);
        RaycastShape3D volume = RaycastShape3D.CreateSphere(new Vec3(-5f, 0f, 0f), 1f);
        var ray = new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX);

        RaycastIntersections.TryShapeCast3D(in ray, in target, in volume, Id, 20f, out RaycastHit3D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(3f, 1e-4f);
        hit.Point.X.Should().BeApproximately(-1f, 1e-4f);
        hit.Normal.X.Should().BeApproximately(-1f, 1e-4f);

        var offset = new Ray3D(new Vec3(-5f, 2.5f, 0f), Vec3.UnitX);
        RaycastIntersections.TryShapeCast3D(in offset, in target, in volume, Id, 20f, out _).Should().BeFalse();

        var wide = RaycastShape3D.CreateSphere(new Vec3(-5f, 2.5f, 0f), 2f);
        RaycastIntersections.TryShapeCast3D(in offset, in target, in wide, Id, 20f, out RaycastHit3D wideHit).Should().BeTrue();
        wideHit.Distance.Should().BeApproximately(5f - MathF.Sqrt((3f * 3f) - (2.5f * 2.5f)), 1e-3f);
    }

    [Fact]
    public void BoxCastAgainstAabbInflatesByExtents()
    {
        RaycastShape3D target = RaycastShape3D.CreateAabb(new Aabb3D(-1f, -1f, -1f, 1f, 1f, 1f));
        RaycastShape3D volume = RaycastShape3D.CreateAabb(new Aabb3D(-0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f));
        var ray = new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX);

        RaycastIntersections.TryShapeCast3D(in ray, in target, in volume, Id, 20f, out RaycastHit3D hit).Should().BeTrue();
        hit.Distance.Should().BeApproximately(3.5f, 1e-4f);
        hit.Point.X.Should().BeApproximately(-1f, 1e-4f);

        var offset = new Ray3D(new Vec3(-5f, 1.6f, 0f), Vec3.UnitX);
        RaycastIntersections.TryShapeCast3D(in offset, in target, in volume, Id, 20f, out _).Should().BeFalse();
    }

    [Fact]
    public void SphereCastAgainstCapsuleAndPlane()
    {
        RaycastShape3D capsule = RaycastShape3D.CreateCapsule(new Vec3(0f, -1f, 0f), new Vec3(0f, 1f, 0f), 0.5f);
        RaycastShape3D volume = RaycastShape3D.CreateSphere(new Vec3(-5f, 0f, 0f), 0.5f);
        var ray = new Ray3D(new Vec3(-5f, 0f, 0f), Vec3.UnitX);

        RaycastIntersections.TryShapeCast3D(in ray, in capsule, in volume, Id, 20f, out RaycastHit3D capsuleHit).Should().BeTrue();
        capsuleHit.Distance.Should().BeApproximately(4f, 1e-4f);

        RaycastShape3D plane = RaycastShape3D.CreatePlane(Vec3.Zero, Vec3.UnitY);
        var fromAbove = new Ray3D(new Vec3(0f, 5f, 0f), Vec3.NegativeUnitY);
        RaycastIntersections.TryShapeCast3D(in fromAbove, in plane, in volume, Id, 20f, out RaycastHit3D planeHit).Should().BeTrue();
        planeHit.Distance.Should().BeApproximately(4.5f, 1e-4f);
        planeHit.Point.Y.Should().BeApproximately(0f, 1e-4f);
    }
}
