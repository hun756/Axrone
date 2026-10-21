namespace Axrone.Shapes2D.Tests;

public sealed class ShapeModelTests
{
    private static readonly Vec2[] Square = [new(0f, 0f), new(4f, 0f), new(4f, 4f), new(0f, 4f)];

    [Fact]
    public void Geometry_Roles_Compute()
    {
        new RectangleGeometry(0f, 0f, 4f, 2f).ComputeArea().Should().Be(8f);
        new RectangleGeometry(0f, 0f, 4f, 2f).ComputeCentroid().Should().Be(new Vec2(2f, 1f));
        new CircleGeometry(0f, 0f, 1f).ComputeArea().Should().BeApproximately(MathF.PI, 1e-6f);
        new CircleGeometry(0f, 0f, 1f).ComputePerimeter().Should().BeApproximately(ShapeConstants.Tau, 1e-6f);
        new EllipseGeometry(0f, 0f, 2f, 1f).ComputeArea().Should().BeApproximately(2f * MathF.PI, 1e-5f);
        new TriangleGeometry(new Vec2(0f, 0f), new Vec2(4f, 0f), new Vec2(0f, 3f)).ComputeArea().Should().Be(6f);
        new LineGeometry(new Vec2(0f, 0f), new Vec2(3f, 4f)).ComputePerimeter().Should().Be(5f);
    }

    [Fact]
    public void Containment_Checks()
    {
        new RectangleGeometry(0f, 0f, 4f, 4f).ContainsPoint(new Vec2(2f, 2f)).Should().BeTrue();
        new RectangleGeometry(0f, 0f, 4f, 4f).ContainsPoint(new Vec2(9f, 9f)).Should().BeFalse();
        new CircleGeometry(0f, 0f, 2f).ContainsPoint(new Vec2(1f, 1f)).Should().BeTrue();
        new EllipseGeometry(0f, 0f, 2f, 1f).ContainsPoint(new Vec2(2f, 0f)).Should().BeTrue();
        new TriangleGeometry(new Vec2(0f, 0f), new Vec2(4f, 0f), new Vec2(0f, 4f)).ContainsPoint(new Vec2(1f, 1f)).Should().BeTrue();
    }

    [Fact]
    public void Factories_Validate()
    {
        Action badRect = () => ShapeFactory.CreateRectangle(0f, 0f, -1f, 1f);
        badRect.Should().Throw<ShapeValidationException>();
        Action collinear = () => ShapeFactory.CreateTriangle(new Vec2(0f, 0f), new Vec2(1f, 1f), new Vec2(2f, 2f));
        collinear.Should().Throw<ShapeValidationException>();
        Action zeroLine = () => ShapeFactory.CreateLine(new Vec2(1f, 1f), new Vec2(1f, 1f));
        zeroLine.Should().Throw<ShapeValidationException>();
        Action badAlign = () => ShapeFactory.CreateLine(
            new Vec2(0f, 0f), new Vec2(1f, 0f),
            ShapeFactory.CreateStroke(ShapeFactory.CreateSolidPaint("red"), 1f, ShapeStrokeAlignment.Outside));
        badAlign.Should().Throw<ShapeValidationException>();

        Shape2D named = ShapeFactory.CreateCircle(0f, 0f, 1f, name: "  hero  ");
        named.Name.Should().Be("hero");
        named.Opacity.Should().Be(1f);
    }

    [Fact]
    public void Polygon_Requires_Simple_Contained_Rings()
    {
        Action selfIntersect = () => ShapeFactory.CreatePolygon(
            [new Vec2(0f, 0f), new Vec2(4f, 4f), new Vec2(0f, 4f), new Vec2(4f, 0f)]);
        selfIntersect.Should().Throw<ShapeValidationException>();

        Action outsideHole = () =>
        {
            var holePoints = new Vec2[] { new(9f, 9f), new(10f, 9f), new(10f, 10f) };
            _ = ShapeFactory.CreatePolygon(
                Square,
                [new PolygonRing(holePoints, PolygonWinding.Cw)]);
        };
        outsideHole.Should().Throw<ShapeValidationException>();

        Action open = () => _ = new PolygonShape(Square, closed: false);
        open.Should().Throw<ShapeValidationException>();
    }

    [Fact]
    public void Polygon_Normalizes_Winding()
    {
        Vec2[] cw = [new(0f, 0f), new(0f, 4f), new(4f, 4f), new(4f, 0f)];
        PolygonShape poly = ShapeFactory.CreatePolygon(cw);
        poly.Outer.Winding.Should().Be(PolygonWinding.Ccw);
        poly.Convex.Should().BeTrue();
        poly.Closed.Should().BeTrue();

        Action wrongWinding = () => ShapeFactory.CreatePolygon(Square, winding: PolygonWinding.Cw);
        wrongWinding.Should().Throw<ShapeValidationException>();
    }

    [Fact]
    public void Builder_Requires_Outer_Ring()
    {
        Vec2[] hole = [new Vec2(1f, 1f), new Vec2(2f, 1f), new Vec2(2f, 2f), new Vec2(1f, 2f)];
        PolygonShape poly = PolygonBuilder<PhaseEmpty>.Create()
            .WithOuter(Square)
            .AddHole(hole)
            .Build();
        poly.Holes.Should().HaveCount(1);
        poly.Kind.Should().Be(ShapeKind.Polygon);

        Action missing = () => PolygonBuilder<PhaseEmpty>.Create().Build();
        missing.Should().Throw<ShapeValidationException>();
    }

    [Fact]
    public void Match_Dispatches_All_Kinds()
    {
        Shape2D circle = ShapeFactory.CreateCircle(0f, 0f, 1f);
        string kind = circle.Match(
            _ => "rect", _ => "circle", _ => "ellipse", _ => "tri", _ => "line", _ => "poly");
        kind.Should().Be("circle");
    }
}
