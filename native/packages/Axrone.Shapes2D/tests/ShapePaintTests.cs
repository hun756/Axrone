namespace Axrone.Shapes2D.Tests;

public sealed class ShapePaintTests
{
    private static GradientStop[] Stops(params (float Offset, string Color)[] entries)
    {
        var stops = new GradientStop[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            stops[i] = ShapePaintFactory.CreateGradientStop(entries[i].Offset, entries[i].Color);
        }

        return stops;
    }

    [Fact]
    public void Solid_Fingerprint_Is_Stable_Text()
    {
        var paint = ShapePaintFactory.CreateSolidPaint("red");
        paint.Kind.Should().Be("solid");
        paint.KindTag.Should().Be(PaintKind.Solid);
        paint.Fingerprint.Should().Be(paint.Fingerprint);
        paint.Fingerprint.Should().StartWith("solid:1:0:0:1");
    }

    [Fact]
    public void Gradient_Factory_Sorts_Stops_And_Validates()
    {
        var gradient = ShapePaintFactory.CreateLinearGradient(
            new Vec2(0f, 0f), new Vec2(1f, 0f),
            Stops((1f, "white"), (0f, "black")));
        gradient.Stops[0].Offset.Should().Be(0f);
        gradient.Stops[1].Offset.Should().Be(1f);
        gradient.Kind.Should().Be("linear-gradient");
        gradient.Fingerprint.Should().Be(gradient.Fingerprint);

        Action empty = () => ShapePaintFactory.CreateLinearGradient(new Vec2(0f, 0f), new Vec2(1f, 0f), []);
        empty.Should().Throw<PaintValidationException>();

        Action badOffset = () => ShapePaintFactory.CreateGradientStop(2f, "red");
        badOffset.Should().Throw<PaintValidationException>();
    }

    [Fact]
    public void Radial_Requires_Positive_Radius()
    {
        Action zero = () => ShapePaintFactory.CreateRadialGradient(new Vec2(0f, 0f), 0f, Stops((0f, "black")));
        zero.Should().Throw<ShapeValidationException>();
    }

    [Fact]
    public void Lut_Caches_Per_Paint_And_Size()
    {
        var gradient = ShapePaintFactory.CreateLinearGradient(
            new Vec2(0f, 0f), new Vec2(1f, 0f),
            Stops((0f, "black"), (1f, "white")));
        float[] first = PaintSampler.GetOrCreateLut(gradient, 8);
        float[] second = PaintSampler.GetOrCreateLut(gradient, 8);
        first.Should().BeSameAs(second);
        first.Length.Should().Be(32);
        PaintSampler.GetOrCreateLut(gradient, 16).Length.Should().Be(64);

        Action solid = () => PaintSampler.GetOrCreateLut(ShapePaintFactory.CreateSolidPaint("red"));
        solid.Should().Throw<PaintValidationException>();
    }

    [Fact]
    public void Linear_Sampling_Projects_On_Axis()
    {
        var gradient = ShapePaintFactory.CreateLinearGradient(
            new Vec2(0f, 0f), new Vec2(1f, 0f),
            Stops((0f, "black"), (1f, "white")),
            units: GradientUnits.Local);
        ResolvedColor mid = PaintSampler.Sample(gradient, new Vec2(0.5f, 9f));
        mid.R.Should().BeApproximately(0.5f, 2e-2f);
        ResolvedColor clamped = PaintSampler.Sample(gradient, new Vec2(9f, 0f));
        clamped.R.Should().BeApproximately(1f, 1e-6f);
    }

    [Fact]
    public void Radial_Sampling_Uses_Distance()
    {
        var gradient = ShapePaintFactory.CreateRadialGradient(
            new Vec2(0f, 0f), 2f,
            Stops((0f, "white"), (1f, "black")),
            units: GradientUnits.Local);
        ResolvedColor center = PaintSampler.Sample(gradient, new Vec2(0f, 0f));
        center.R.Should().BeApproximately(1f, 2e-2f);
        ResolvedColor edge = PaintSampler.Sample(gradient, new Vec2(2f, 0f));
        edge.R.Should().BeApproximately(0f, 2e-2f);
    }

    [Fact]
    public void Stroke_Requires_Paint_And_Width()
    {
        var stroke = ShapePaintFactory.CreateStroke(ShapePaintFactory.CreateSolidPaint("red"), 2f);
        stroke.Width.Should().Be(2f);
        stroke.Alignment.Should().Be(ShapeStrokeAlignment.Center);

        Action nullPaint = () => ShapePaintFactory.CreateStroke(null!, 1f);
        nullPaint.Should().Throw<ArgumentNullException>();

        Action zeroWidth = () => ShapePaintFactory.CreateStroke(ShapePaintFactory.CreateSolidPaint("red"), 0f);
        zeroWidth.Should().Throw<ShapeValidationException>();
    }
}

