namespace Axrone.Physics2D.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class Distance2DTests
{
    private const float Tolerance = 1e-3f;

    [Fact]
    public void CircleCircle_Separated_ExactDistance()
    {
        var a = new Circle2(new Vec2(0, 0), 1f);
        var b = new Circle2(new Vec2(5, 0), 1f);

        Gjk2D.ComputeDistance(in a, in b, out float distance, out Vec2 pointA, out Vec2 pointB).Should().BeTrue();

        distance.Should().BeApproximately(3f, Tolerance);
        pointA.Should().Be(new Vec2(1, 0));
        pointB.Should().Be(new Vec2(4, 0));
    }

    [Fact]
    public void CircleCircle_TouchingAndOverlapping_ReportZero()
    {
        var a = new Circle2(new Vec2(0, 0), 1f);
        var touching = new Circle2(new Vec2(2, 0), 1f);
        var overlapping = new Circle2(new Vec2(1, 0), 1f);

        Gjk2D.ComputeDistance(in a, in touching, out float touchDistance, out _, out _).Should().BeFalse();
        touchDistance.Should().BeApproximately(0f, Tolerance);

        Gjk2D.ComputeDistance(in a, in overlapping, out float overlapDistance, out _, out _).Should().BeFalse();
        overlapDistance.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void BoxBox_SeparatedAxisAligned()
    {
        Polygon2D left = Polygon2D.Create(new Vec2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) });
        Polygon2D right = Polygon2D.Create(new Vec2[] { new(4, 0), new(5, 0), new(5, 1), new(4, 1) });

        Gjk2D.ComputeDistance(in left, in right, out float distance, out Vec2 pointA, out Vec2 pointB).Should().BeTrue();

        distance.Should().BeApproximately(3f, Tolerance);
        new Aabb2D(new Vec2(0, 0), new Vec2(1, 1)).Contains(pointA).Should().BeTrue();
        new Aabb2D(new Vec2(4, 0), new Vec2(5, 1)).Contains(pointB).Should().BeTrue();
        (pointB - pointA).LengthSquared().Should().BeApproximately(9f, 0.01f);
    }

    [Fact]
    public void BoxBox_SeparatedDiagonal_ExactCorners()
    {
        Polygon2D a = Polygon2D.Create(new Vec2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) });
        Polygon2D b = Polygon2D.Create(new Vec2[] { new(3, 3), new(4, 3), new(4, 4), new(3, 4) });

        Gjk2D.ComputeDistance(in a, in b, out float distance, out Vec2 pointA, out Vec2 pointB).Should().BeTrue();

        distance.Should().BeApproximately(MathF.Sqrt(8f), Tolerance);
        pointA.X.Should().BeApproximately(1f, Tolerance);
        pointA.Y.Should().BeApproximately(1f, Tolerance);
        pointB.X.Should().BeApproximately(3f, Tolerance);
        pointB.Y.Should().BeApproximately(3f, Tolerance);
    }

    [Fact]
    public void CirclePolygon_Separated()
    {
        var circle = new Circle2(new Vec2(0, 0), 1f);
        Polygon2D box = Polygon2D.Create(new Vec2[] { new(4, -1), new(5, -1), new(5, 1), new(4, 1) });

        Gjk2D.ComputeDistance(in circle, in box, out float distance, out Vec2 pointA, out Vec2 pointB).Should().BeTrue();

        distance.Should().BeApproximately(3f, Tolerance);
        pointA.X.Should().BeApproximately(1f, Tolerance);
        pointA.Y.Should().BeApproximately(0f, Tolerance);
        pointB.X.Should().BeApproximately(4f, Tolerance);
    }

    [Fact]
    public void CapsuleBox_Separated()
    {
        var capsule = new Capsule2D(new Vec2(0, 0), new Vec2(0, 4), 1f);
        Polygon2D box = Polygon2D.Create(new Vec2[] { new(3, 1), new(4, 1), new(4, 3), new(3, 3) });

        Gjk2D.ComputeDistance(in capsule, in box, out float distance, out Vec2 pointA, out Vec2 pointB).Should().BeTrue();

        distance.Should().BeApproximately(2f, Tolerance);
        pointA.X.Should().BeApproximately(1f, Tolerance);
        pointB.X.Should().BeApproximately(3f, Tolerance);
    }

    [Fact]
    public void Concentric_Overlaps_ReportsZero()
    {
        Polygon2D outer = Polygon2D.CreateBox(new Vec2(2, 2));
        var inner = new Circle2(new Vec2(0, 0), 1f);

        Gjk2D.ComputeDistance(in outer, in inner, out float distance, out _, out _).Should().BeFalse();
        distance.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void MixedPairs_AgreeWithAnalytic()
    {
        var circleA = new Circle2(new Vec2(-3, 1), 0.5f);
        var circleB = new Circle2(new Vec2(2, -2), 1.5f);
        float expected = MathF.Sqrt(25f + 9f) - 2.0f;

        Gjk2D.ComputeDistance(in circleA, in circleB, out float distance, out _, out _).Should().BeTrue();
        distance.Should().BeApproximately(expected, Tolerance);
    }
}
