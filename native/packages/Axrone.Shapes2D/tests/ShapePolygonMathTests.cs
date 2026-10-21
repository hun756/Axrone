namespace Axrone.Shapes2D.Tests;

public sealed class ShapePolygonMathTests
{
    private static readonly Vec2[] Square = [new(0f, 0f), new(4f, 0f), new(4f, 4f), new(0f, 4f)];
    private static readonly Vec2[] Triangle = [new(0f, 0f), new(4f, 0f), new(0f, 3f)];

    [Fact]
    public void Ring_Requires_Three_Points()
    {
        Action two = () => _ = new PolygonRing([new Vec2(0f, 0f), new Vec2(1f, 1f)], PolygonWinding.Ccw);
        two.Should().Throw<ShapeValidationException>();
    }

    [Fact]
    public void Area_Perimeter_Centroid()
    {
        PolygonMath.SignedArea(Square).Should().BeApproximately(16.0, 1e-9);
        PolygonMath.SignedArea(Triangle).Should().BeApproximately(6.0, 1e-9);
        PolygonMath.Perimeter(Square).Should().BeApproximately(16f, 1e-5f);
        PolygonMath.Centroid(Square).Should().Be(new Vec2(2f, 2f));
        PolygonMath.ComputeBounds(Square).Should().Be(new ShapeBounds(0f, 0f, 4f, 4f));
        PolygonMath.ComputeBounds([]).Should().Be(ShapeBounds.Empty);
    }

    [Fact]
    public void Point_Tests()
    {
        PolygonMath.PointInPolygon(Square, new Vec2(2f, 2f)).Should().BeTrue();
        PolygonMath.PointInPolygon(Square, new Vec2(9f, 9f)).Should().BeFalse();
        PolygonMath.PointInConvexPolygon(Square, new Vec2(2f, 2f)).Should().BeTrue();
        PolygonMath.PointInConvexPolygon(Square, new Vec2(9f, 9f)).Should().BeFalse();

        var holes = new PolygonRing[] { new(new Vec2[] { new(1f, 1f), new(2f, 1f), new(2f, 2f), new(1f, 2f) }, PolygonWinding.Cw) };
        PolygonMath.PointInPolygonWithHoles(Square, holes, new Vec2(1.5f, 1.5f)).Should().BeFalse();
        PolygonMath.PointInPolygonWithHoles(Square, holes, new Vec2(3f, 3f)).Should().BeTrue();
    }

    [Fact]
    public void Convexity_And_Simplicity()
    {
        PolygonMath.IsConvexPolygon(Square).Should().BeTrue();
        PolygonMath.IsConvexPolygon([new Vec2(0f, 0f), new Vec2(4f, 0f), new Vec2(4f, 4f), new Vec2(2f, 1f), new Vec2(0f, 4f)]).Should().BeFalse();
        PolygonMath.IsSimplePolygon(Square).Should().BeTrue();
        PolygonMath.IsSimplePolygon([new Vec2(0f, 0f), new Vec2(4f, 4f), new Vec2(0f, 4f), new Vec2(4f, 0f)]).Should().BeFalse();
    }

    [Fact]
    public void Edge_Distance_And_Segment()
    {
        PolygonMath.PointToPolygonEdgeDistance(Square, new Vec2(2f, 6f)).Should().BeApproximately(2f, 1e-5f);
        PolygonMath.DistanceToSegmentSquared(new Vec2(1f, 1f), new Vec2(0f, 0f), new Vec2(2f, 0f)).Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void Cleanup_Removes_Collinear_And_Duplicates()
    {
        Vec2[] cleaned = PolygonMath.RemoveCollinearVertices([new Vec2(0f, 0f), new Vec2(2f, 0f), new Vec2(4f, 0f), new Vec2(4f, 4f), new Vec2(0f, 4f)]);
        cleaned.Should().HaveCount(4);

        Vec2[] deduped = PolygonMath.DeduplicateVertices([new Vec2(0f, 0f), new Vec2(0f, 0f), new Vec2(4f, 0f), new Vec2(4f, 4f), new Vec2(0f, 4f), new Vec2(0f, 0f)]);
        deduped.Should().HaveCount(4);
    }
}
