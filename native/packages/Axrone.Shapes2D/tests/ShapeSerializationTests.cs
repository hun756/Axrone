namespace Axrone.Shapes2D.Tests;

public sealed class ShapeSerializationTests
{
    [Fact]
    public void Fingerprint_Is_Stable_And_Prefixed()
    {
        Shape2D rect = ShapeFactory.CreateRectangle(0f, 0f, 4f, 2f, ShapePaintFactory.CreateSolidPaint("red"));
        string first = rect.Fingerprint;
        string second = rect.Fingerprint;
        first.Should().Be(second);
        first.Should().StartWith("rectangle:");
    }

    [Fact]
    public void Rectangle_RoundTrips()
    {
        Shape2D rect = ShapeFactory.CreateRectangle(
            1f, 2f, 4f, 2f,
            ShapePaintFactory.CreateSolidPaint("red"),
            ShapePaintFactory.CreateStroke(ShapePaintFactory.CreateSolidPaint("white"), 1f),
            opacity: 0.5f,
            name: "hero");
        Shape2D restored = ShapeSerializer.DeserializeShape(ShapeSerializer.StringifyShape(rect));
        restored.Should().BeOfType<RectangleShape>();
        var rect2 = (RectangleShape)restored;
        rect2.X.Should().Be(1f);
        rect2.Width.Should().Be(4f);
        rect2.Opacity.Should().BeApproximately(0.5f, 1e-6f);
        rect2.Name.Should().Be("hero");
        rect2.Stroke.Should().NotBeNull();
    }

    [Fact]
    public void Gradient_RoundTrips_With_Stops()
    {
        var gradient = ShapePaintFactory.CreateLinearGradient(
            new Vec2(0f, 0f), new Vec2(1f, 0f),
            [new GradientStop(0f, ColorParser.ParseColor("black")), new GradientStop(1f, ColorParser.ParseColor("white"))],
            spread: GradientSpread.Repeat,
            colorSpace: GradientColorSpace.LinearSrgb,
            units: GradientUnits.Local);
        Shape2D circle = ShapeFactory.CreateCircle(0f, 0f, 5f, gradient);

        Shape2D restored = ShapeSerializer.DeserializeShape(ShapeSerializer.StringifyShape(circle));
        restored.Should().BeOfType<CircleShape>();
        var paint = restored.Fill.Should().BeOfType<LinearGradientPaint>().Subject;
        paint.Spread.Should().Be(GradientSpread.Repeat);
        paint.ColorSpace.Should().Be(GradientColorSpace.LinearSrgb);
        paint.Units.Should().Be(GradientUnits.Local);
        paint.Stops.Should().HaveCount(2);
    }

    [Fact]
    public void Polygon_RoundTrips_With_Holes()
    {
        Vec2[] outer = [new(0f, 0f), new(4f, 0f), new(4f, 4f), new(0f, 4f)];
        var hole = new PolygonRing([new Vec2(1f, 1f), new Vec2(2f, 1f), new Vec2(2f, 2f), new Vec2(1f, 2f)], PolygonWinding.Cw);
        PolygonShape poly = ShapeFactory.CreatePolygon(outer, [hole]);

        Shape2D restored = ShapeSerializer.DeserializeShape(ShapeSerializer.StringifyShape(poly));
        restored.Should().BeOfType<PolygonShape>();
        ((PolygonShape)restored).Holes.Should().HaveCount(1);
    }

    [Fact]
    public void Line_Stroke_Forced_Center()
    {
        var stroke = ShapePaintFactory.CreateStroke(ShapePaintFactory.CreateSolidPaint("red"), 2f, ShapeStrokeAlignment.Center);
        Shape2D line = ShapeFactory.CreateLine(new Vec2(0f, 0f), new Vec2(3f, 4f), stroke);
        Shape2D restored = ShapeSerializer.DeserializeShape(ShapeSerializer.StringifyShape(line));
        restored.Should().BeOfType<LineShape>();
        ((LineShape)restored).Stroke.Should().NotBeNull();
    }

    [Fact]
    public void Unknown_Types_Throw_Serialization()
    {
        Action shape = () => ShapeSerializer.DeserializeShape("""{"type":"shape/nope"}""");
        shape.Should().Throw<SerializationException>();
        Action paint = () => ShapeSerializer.DeserializePaint(JsonDocument.Parse("""{"type":"paint/nope"}""").RootElement);
        paint.Should().Throw<SerializationException>();
        Action missing = () => ShapeSerializer.DeserializeShape("""{"kind":"x"}""");
        missing.Should().Throw<SerializationException>();
    }
}

