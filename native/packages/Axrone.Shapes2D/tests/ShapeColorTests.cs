namespace Axrone.Shapes2D.Tests;

public sealed class ShapeColorTests
{
    [Fact]
    public void Named_And_Hex_Colors_Parse()
    {
        ColorParser.ParseColor("red").Should().Be(new ResolvedColor(1f, 0f, 0f, 1f));
        ColorParser.ParseColor("transparent").Should().Be(ResolvedColor.Transparent);
        ColorParser.ParseColor("#ff0000").Should().Be(new ResolvedColor(1f, 0f, 0f, 1f));
        ColorParser.ParseColor("#f00").Should().Be(new ResolvedColor(1f, 0f, 0f, 1f));
        ResolvedColor half = ColorParser.ParseColor("#ff000080");
        half.A.Should().BeApproximately(0.502f, 1e-3f);
        Action unknown = () => ColorParser.ParseColor("blurple");
        unknown.Should().Throw<PaintValidationException>();
        Action blank = () => ColorParser.ParseColor("  ");
        blank.Should().Throw<PaintValidationException>();
    }

    [Fact]
    public void FromRgb_Clamps_And_Validates()
    {
        ColorParser.FromRgb(2f, -1f, 0.5f).Should().Be(new ResolvedColor(1f, 0f, 0.5f, 1f));
        Action nan = () => ColorParser.FromRgb(float.NaN, 0f, 0f);
        nan.Should().Throw<ShapeValidationException>();
    }

    [Theory]
    [InlineData(0f, 0f, 0f, 0f)]
    [InlineData(1.5f, 1f, 0.5f, 0.5f)]
    [InlineData(-0.5f, 0f, 0.5f, 0.5f)]
    public void Spread_Policies_Wrap(float input, float pad, float repeat, float reflect)
    {
        PadSpreadPolicy.Apply(input).Should().BeApproximately(pad, 1e-6f);
        RepeatSpreadPolicy.Apply(input).Should().BeApproximately(repeat, 1e-6f);
        ReflectSpreadPolicy.Apply(input).Should().BeApproximately(reflect, 1e-6f);
    }

    [Fact]
    public void Reflect_Folds_Above_One()
    {
        ReflectSpreadPolicy.Apply(1.5f).Should().BeApproximately(0.5f, 1e-6f);
    }

    [Fact]
    public void Srgb_Lerp_Midpoint()
    {
        ResolvedColor mid = SrgbColorPolicy.Interpolate(ResolvedColor.Black, ResolvedColor.White, 0.5f);
        mid.R.Should().BeApproximately(0.5f, 1e-6f);
        mid.A.Should().BeApproximately(1f, 1e-6f);
        SrgbColorPolicy.SpaceKind.Should().Be(GradientColorSpace.Srgb);
        LinearSrgbColorPolicy.SpaceKind.Should().Be(GradientColorSpace.LinearSrgb);
        HslColorPolicy.SpaceKind.Should().Be(GradientColorSpace.Hsl);
        LabColorPolicy.SpaceKind.Should().Be(GradientColorSpace.Lab);
    }

    [Fact]
    public void Linear_Lerp_Differs_From_Srgb()
    {
        ResolvedColor linear = LinearSrgbColorPolicy.Interpolate(new ResolvedColor(0f, 0f, 0f, 1f), new ResolvedColor(1f, 1f, 1f, 1f), 0.5f);
        linear.R.Should().BeApproximately(0.735f, 1e-2f);
    }

    [Fact]
    public void Hsl_RoundTrip_Preserves_Gray()
    {
        var gray = new ResolvedColor(0.5f, 0.5f, 0.5f, 1f);
        ResolvedColor mid = HslColorPolicy.Interpolate(gray, ResolvedColor.White, 0.5f);
        mid.R.Should().BeApproximately(mid.G, 1e-5f);
        mid.G.Should().BeApproximately(mid.B, 1e-5f);
    }

    [Fact]
    public void Lab_Lerp_Midpoint_Is_Neutral()
    {
        ResolvedColor mid = LabColorPolicy.Interpolate(ResolvedColor.Black, ResolvedColor.White, 0.5f);
        mid.R.Should().BeApproximately(mid.G, 2e-2f);
        mid.G.Should().BeApproximately(mid.B, 2e-2f);
    }
}
