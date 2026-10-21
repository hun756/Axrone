namespace Axrone.Numeric.Tests;

public sealed class ColorArithmeticTests
{
    [Fact]
    public void Operators_Clamp()
    {
        Rgba128Float red = new(1f, 0f, 0f, 1f);
        red.Should().Be(new Rgba128Float(1f, 0f, 0f, 1f));
        (red + new Rgba128Float(0.5f, 0f, 0f, 0f)).Should().Be(red);
        (new Rgba128Float(0.2f, 0f, 0f, 1f) - new Rgba128Float(0.5f, 0f, 0f, 1f)).Should().Be(new Rgba128Float(0f, 0f, 0f, 0f));
        (new Rgba128Float(0.5f, 0.5f, 0.5f, 0.5f) * new Rgba128Float(0.5f, 0.5f, 0.5f, 0.5f))
            .Should().Be(new Rgba128Float(0.25f, 0.25f, 0.25f, 0.25f));
        (new Rgba128Float(0.5f, 0.5f, 0.5f, 0.5f) * 2f).Should().Be(new Rgba128Float(1f, 1f, 1f, 0.5f));
        (2f * new Rgba128Float(0.5f, 0f, 0f, 1f)).Should().Be(new Rgba128Float(1f, 0f, 0f, 1f));
    }

    [Fact]
    public void FromArray_Validates()
    {
        float[] values = [0.25f, 0.5f, 0.75f, 1f];
        Rgba128Float.FromArray(values).Should().Be(new Rgba128Float(0.25f, 0.5f, 0.75f, 1f));
        Rgba128Float.FromArray(values, 1).Should().Be(new Rgba128Float(0.5f, 0.75f, 1f, 1f));
        Action badOffset = () => Rgba128Float.FromArray(values, -1);
        badOffset.Should().Throw<ArgumentOutOfRangeException>();
        Action shortArray = () => Rgba128Float.FromArray([0.1f, 0.2f]);
        shortArray.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Lerp_Family()
    {
        ColorOps.ApproximatelyEquals(
            Rgba128Float.Lerp(new Rgba128Float(0f, 0f, 0f, 1f), new Rgba128Float(1f, 1f, 1f, 1f), 0.5f),
            new Rgba128Float(0.5f, 0.5f, 0.5f, 1f), 1e-6f).Should().BeTrue();
        Rgba128Float.Lerp(new Rgba128Float(0f, 0f, 0f, 1f), new Rgba128Float(1f, 1f, 1f, 1f), 2f).Should().Be(new Rgba128Float(1f, 1f, 1f, 1f));
        Rgba128Float mid = Rgba128Float.LerpHsl(new Rgba128Float(1f, 0f, 0f, 1f), new Rgba128Float(0f, 0f, 1f, 1f), 0.5f);
        ColorOps.ToHsl(mid).H.Should().BeApproximately(300f, 1f);
        Rgba128Float labMid = Rgba128Float.LerpLab(new Rgba128Float(0f, 0f, 0f, 1f), new Rgba128Float(1f, 1f, 1f, 1f), 0.5f);
        labMid.R.Should().BeGreaterThan(0f);
        labMid.R.Should().BeLessThan(1f);
    }

    [Fact]
    public void Palette_Covers_Web_Names()
    {
        Colors.Brown.Should().Be(new Rgba32(153, 102, 51, 255));
        Colors.Pink.Should().Be(new Rgba32(255, 191, 204, 255));
        Colors.Navy.Should().Be(new Rgba32(0, 0, 128, 255));
        Colors.Maroon.Should().Be(new Rgba32(128, 0, 0, 255));
        Colors.Olive.Should().Be(new Rgba32(128, 128, 0, 255));
        Colors.Aqua.Should().Be(new Rgba32(0, 255, 255, 255));
        Colors.Teal.Should().Be(new Rgba32(0, 128, 128, 255));
        Colors.Silver.Should().Be(new Rgba32(192, 192, 192, 255));
        Colors.Fuchsia.Should().Be(new Rgba32(255, 0, 255, 255));
    }

    [Fact]
    public void Adjustments()
    {
        var gray = new Rgba128Float(0.5f, 0.5f, 0.5f, 1f);
        ColorOps.ToHsl(gray.Lighten(0.2f)).L.Should().BeGreaterThan(0.5f);
        ColorOps.ToHsl(gray.Darken(0.2f)).L.Should().BeLessThan(0.5f);
        ColorOps.ApproximatelyEquals(new Rgba128Float(1f, 0f, 0f, 1f).Saturate(0f), new Rgba128Float(1f, 0f, 0f, 1f), 1e-6f).Should().BeTrue();
        ColorOps.ApproximatelyEquals(new Rgba128Float(1f, 0f, 0f, 1f).Desaturate(1f), new Rgba128Float(0.5f, 0.5f, 0.5f, 1f), 1e-4f).Should().BeTrue();
        ColorOps.ToHsl(new Rgba128Float(1f, 0f, 0f, 1f).AdjustHue(120f)).H.Should().BeApproximately(120f, 1f);
        new Rgba128Float(1f, 0f, 0f, 1f).Invert().Should().Be(new Rgba128Float(0f, 1f, 1f, 1f));
        Rgba128Float grayed = new Rgba128Float(0.2f, 0.4f, 0.6f, 0.8f).Grayscale();
        grayed.R.Should().BeApproximately(0.299f * 0.2f + 0.587f * 0.4f + 0.114f * 0.6f, 1e-6f);
        grayed.A.Should().Be(0.8f);
    }
}
