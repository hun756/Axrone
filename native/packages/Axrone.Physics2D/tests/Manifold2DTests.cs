namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class Manifold2DTests
{
    private const float Tolerance = 1e-4f;

    private static Polygon2D Box(float minX, float minY, float maxX, float maxY) =>
        Polygon2D.Create(new Vec2[] { new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY) });

    [Fact]
    public void BoxBox_FaceContact_TwoPoints()
    {
        Polygon2D a = Box(0, 0, 1, 1);
        Polygon2D b = Box(0.5f, 0, 1.5f, 1);

        Narrowphase2D.EvaluatePolygonPolygon(in a, in b, out ContactManifold manifold).Should().BeTrue();

        manifold.Count.Should().Be(2);
        manifold.Normal.X.Should().BeApproximately(1f, Tolerance);
        manifold.Normal.Y.Should().BeApproximately(0f, Tolerance);
        manifold.Point0.Separation.Should().BeApproximately(-0.5f, Tolerance);
        manifold.Point1.Separation.Should().BeApproximately(-0.5f, Tolerance);
        manifold.Point0.Id.Should().NotBe(manifold.Point1.Id);
        manifold.Point0.PositionB.X.Should().BeApproximately(0.5f, Tolerance);
        manifold.Point0.PositionA.X.Should().BeApproximately(1f, Tolerance);
    }

    [Fact]
    public void BoxBox_IdsStable_AcrossFrames()
    {
        Polygon2D a = Box(0, 0, 1, 1);
        Polygon2D b = Box(0.5f, 0, 1.5f, 1);

        Narrowphase2D.EvaluatePolygonPolygon(in a, in b, out ContactManifold first).Should().BeTrue();
        Narrowphase2D.EvaluatePolygonPolygon(in a, in b, out ContactManifold second).Should().BeTrue();

        second.Point0.Id.Should().Be(first.Point0.Id);
        second.Point1.Id.Should().Be(first.Point1.Id);
    }

    [Fact]
    public void BoxBox_CornerTouch_SinglePoint()
    {
        Polygon2D a = Box(0, 0, 1, 1);
        Polygon2D b = Box(1, 1, 2, 2);

        Narrowphase2D.EvaluatePolygonPolygon(in a, in b, out ContactManifold manifold).Should().BeTrue();

        manifold.Count.Should().Be(1);
        manifold.Point0.Separation.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void BoxBox_Separated_NoContact()
    {
        Polygon2D a = Box(0, 0, 1, 1);
        Polygon2D b = Box(5, 5, 6, 6);

        Narrowphase2D.EvaluatePolygonPolygon(in a, in b, out ContactManifold manifold).Should().BeFalse();
        manifold.Count.Should().Be(0);
    }

    [Fact]
    public void CircleCircle_Overlap_Touch_Separated()
    {
        var a = new Circle2(new Vec2(0, 0), 1f);
        var overlapping = new Circle2(new Vec2(1.5f, 0), 1f);
        var touching = new Circle2(new Vec2(2, 0), 1f);
        var separated = new Circle2(new Vec2(3, 0), 1f);

        Narrowphase2D.EvaluateCircleCircle(in a, in overlapping, out ContactManifold hit).Should().BeTrue();
        hit.Count.Should().Be(1);
        hit.Normal.X.Should().BeApproximately(1f, Tolerance);
        hit.Point0.Separation.Should().BeApproximately(-0.5f, Tolerance);

        Narrowphase2D.EvaluateCircleCircle(in a, in touching, out ContactManifold touch).Should().BeTrue();
        touch.Point0.Separation.Should().BeApproximately(0f, Tolerance);

        Narrowphase2D.EvaluateCircleCircle(in a, in separated, out ContactManifold miss).Should().BeFalse();
        miss.Count.Should().Be(0);
    }

    [Fact]
    public void CirclePolygon_Outside_Inside_Separated()
    {
        var circle = new Circle2(new Vec2(0, 0), 1f);
        Polygon2D box = Box(0.5f, -1, 2, 1);

        Narrowphase2D.EvaluateCirclePolygon(in circle, in box, out ContactManifold outside).Should().BeTrue();
        outside.Normal.X.Should().BeApproximately(1f, Tolerance);
        outside.Point0.Separation.Should().BeApproximately(-0.5f, Tolerance);

        var inner = new Circle2(new Vec2(0, 0), 0.5f);
        Polygon2D outer = Box(-1, -1, 1, 1);
        Narrowphase2D.EvaluateCirclePolygon(in inner, in outer, out ContactManifold inside).Should().BeTrue();
        inside.Point0.Separation.Should().BeApproximately(-1.5f, Tolerance);
        (inside.Normal.LengthSquared()).Should().BeApproximately(1f, Tolerance);

        var far = new Circle2(new Vec2(10, 10), 1f);
        Narrowphase2D.EvaluateCirclePolygon(in far, in box, out ContactManifold miss).Should().BeFalse();
        miss.Count.Should().Be(0);
    }

    [Fact]
    public void PolygonCircle_FlipsCirclePolygon()
    {
        var circle = new Circle2(new Vec2(0, 0), 1f);
        Polygon2D box = Box(0.5f, -1, 2, 1);

        Narrowphase2D.EvaluateCirclePolygon(in circle, in box, out ContactManifold forward).Should().BeTrue();
        Narrowphase2D.EvaluatePolygonCircle(in box, in circle, out ContactManifold flipped).Should().BeTrue();

        flipped.Normal.Should().Be(forward.Normal * -1.0f);
        flipped.Point0.PositionA.Should().Be(forward.Point0.PositionB);
        flipped.Point0.PositionB.Should().Be(forward.Point0.PositionA);
        flipped.Point0.Separation.Should().Be(forward.Point0.Separation);
    }

    [Fact]
    public void CapsulePairs_MatchEpa()
    {
        var capsule = new Capsule2D(new Vec2(0, 0), new Vec2(0, 2), 0.5f);
        Polygon2D box = Box(0.25f, 0.5f, 2, 1.5f);

        Epa2D.ComputePenetration(in capsule, in box, out Vec2 epaNormal, out float epaDepth, out _, out _)
            .Should().BeTrue();
        Narrowphase2D.EvaluateCapsulePolygon(in capsule, in box, out ContactManifold manifold).Should().BeTrue();

        manifold.Count.Should().Be(1);
        manifold.Normal.X.Should().BeApproximately(epaNormal.X, Tolerance);
        manifold.Normal.Y.Should().BeApproximately(epaNormal.Y, Tolerance);
        (-manifold.Point0.Separation).Should().BeApproximately(epaDepth, Tolerance);
    }

    [Fact]
    public void Cache_CarriesImpulses_ById()
    {
        Polygon2D a = Box(0, 0, 1, 1);
        Polygon2D b = Box(0.5f, 0, 1.5f, 1);

        Narrowphase2D.EvaluatePolygonPolygon(in a, in b, out ContactManifold first).Should().BeTrue();
        first.SetImpulses(0, 3f, 0.5f);
        first.SetImpulses(1, 1f, 0f);

        var cache = new ContactCache();
        cache.Store(in first);
        cache.Count.Should().Be(2);

        Narrowphase2D.EvaluatePolygonPolygon(in a, in b, out ContactManifold second).Should().BeTrue();
        cache.WarmStart(ref second);

        second.Point0.NormalImpulse.Should().Be(3f);
        second.Point0.TangentImpulse.Should().Be(0.5f);
        second.Point1.NormalImpulse.Should().Be(1f);
    }

    [Fact]
    public void Cache_IgnoresUnknownIds_And_Clears()
    {
        var circle = new Circle2(new Vec2(0, 0), 1f);
        var other = new Circle2(new Vec2(1.5f, 0), 1f);

        Narrowphase2D.EvaluateCircleCircle(in circle, in other, out ContactManifold seeded).Should().BeTrue();
        seeded.SetImpulses(0, 9f, 0f);

        var cache = new ContactCache();
        cache.Store(in seeded);

        Polygon2D a = Box(0, 0, 1, 1);
        Polygon2D b = Box(0.5f, 0, 1.5f, 1);
        Narrowphase2D.EvaluatePolygonPolygon(in a, in b, out ContactManifold manifold).Should().BeTrue();
        cache.WarmStart(ref manifold);

        manifold.Point0.NormalImpulse.Should().Be(0f);
        manifold.Point1.NormalImpulse.Should().Be(0f);

        cache.Clear();
        cache.Count.Should().Be(0);
    }
}
