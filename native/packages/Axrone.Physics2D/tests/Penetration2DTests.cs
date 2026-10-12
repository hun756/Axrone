namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class Penetration2DTests
{
    private const float Tolerance = 1e-3f;

    private static Polygon2D Box(float minX, float minY, float maxX, float maxY) =>
        Polygon2D.Create(new Vec2[] { new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY) });

    [Fact]
    public void BoxBox_XOverlap_NormalPointsAToB()
    {
        Polygon2D a = Box(0, 0, 1, 1);
        Polygon2D b = Box(0.5f, 0, 1.5f, 1);

        Epa2D.ComputePenetration(in a, in b, out Vec2 normal, out float depth, out Vec2 pointA, out Vec2 pointB)
            .Should().BeTrue();

        normal.X.Should().BeApproximately(1f, Tolerance);
        normal.Y.Should().BeApproximately(0f, Tolerance);
        depth.Should().BeApproximately(0.5f, Tolerance);
        pointA.X.Should().BeApproximately(1f, Tolerance);
        pointB.X.Should().BeApproximately(0.5f, Tolerance);
    }

    [Fact]
    public void BoxBox_YOverlap()
    {
        Polygon2D a = Box(0, 0, 1, 1);
        Polygon2D b = Box(0, 0.5f, 1, 1.5f);

        Epa2D.ComputePenetration(in a, in b, out Vec2 normal, out float depth, out _, out _)
            .Should().BeTrue();

        normal.X.Should().BeApproximately(0f, Tolerance);
        normal.Y.Should().BeApproximately(1f, Tolerance);
        depth.Should().BeApproximately(0.5f, Tolerance);
    }

    [Fact]
    public void CircleCircle_Overlap()
    {
        var a = new Circle2(new Vec2(0, 0), 1f);
        var b = new Circle2(new Vec2(1.5f, 0), 1f);

        Epa2D.ComputePenetration(in a, in b, out Vec2 normal, out float depth, out Vec2 pointA, out Vec2 pointB)
            .Should().BeTrue();

        normal.X.Should().BeApproximately(1f, Tolerance);
        normal.Y.Should().BeApproximately(0f, Tolerance);
        depth.Should().BeApproximately(0.5f, Tolerance);
        pointA.X.Should().BeApproximately(1f, Tolerance);
        pointB.X.Should().BeApproximately(0.5f, Tolerance);
    }

    [Fact]
    public void CircleBox_Overlap()
    {
        var circle = new Circle2(new Vec2(0, 0), 1f);
        Polygon2D box = Box(0.5f, -1, 2, 1);

        Epa2D.ComputePenetration(in circle, in box, out Vec2 normal, out float depth, out _, out _)
            .Should().BeTrue();

        normal.X.Should().BeApproximately(1f, Tolerance);
        depth.Should().BeApproximately(0.5f, Tolerance);
    }

    [Fact]
    public void CapsuleBox_Overlap()
    {
        var capsule = new Capsule2D(new Vec2(0, 0), new Vec2(0, 2), 0.5f);
        Polygon2D box = Box(0.25f, 0.5f, 2, 1.5f);

        Epa2D.ComputePenetration(in capsule, in box, out Vec2 normal, out float depth, out _, out _)
            .Should().BeTrue();

        normal.X.Should().BeApproximately(1f, Tolerance);
        depth.Should().BeApproximately(0.25f, Tolerance);
    }

    [Fact]
    public void Separated_ReturnsFalse()
    {
        Polygon2D a = Box(0, 0, 1, 1);
        Polygon2D b = Box(5, 5, 6, 6);

        Epa2D.ComputePenetration(in a, in b, out _, out float depth, out _, out _).Should().BeFalse();
        depth.Should().Be(0f);
    }

    [Fact]
    public void DeepConcentric_ReportsFullDepth()
    {
        var circle = new Circle2(new Vec2(0, 0), 2f);
        Polygon2D box = Box(-1, -1, 1, 1);

        Epa2D.ComputePenetration(in circle, in box, out Vec2 normal, out float depth, out _, out _)
            .Should().BeTrue();

        depth.Should().BeApproximately(3f, 0.01f);
        (normal.LengthSquared()).Should().BeApproximately(1f, Tolerance);
    }

    [Fact]
    public void Touching_ReportsZeroDepthContact()
    {
        var a = new Circle2(new Vec2(0, 0), 1f);
        var b = new Circle2(new Vec2(2, 0), 1f);

        Epa2D.ComputePenetration(in a, in b, out _, out float depth, out _, out _).Should().BeTrue();
        depth.Should().BeApproximately(0f, Tolerance);
    }
}
