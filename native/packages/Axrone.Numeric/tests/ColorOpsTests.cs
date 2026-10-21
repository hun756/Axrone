namespace Axrone.Numeric.Tests;

public sealed class ColorOpsTests
{
    [Fact]
    public void Arithmetic_Clamps()
    {
        (new Color(0.8f, 0f, 0f, 1f) + new Color(0.5f, 0f, 0f, 0f)).Should().Be(new Color(1f, 0f, 0f, 1f));
        (new Color(0.2f, 0f, 0f, 1f) - new Color(0.5f, 0f, 0f, 1f)).Should().Be(new Color(0f, 0f, 0f, 0f));
        (new Color(0.5f, 0.5f, 0.5f, 0.5f) * new Color(0.5f, 0.5f, 0.5f, 0.5f))
            .Should().Be(new Color(0.25f, 0.25f, 0.25f, 0.25f));
        (new Color(0.5f, 0.5f, 0.5f, 0.5f) * 2f).Should().Be(new Color(1f, 1f, 1f, 0.5f));
        (2f * new Color(0.5f, 0f, 0f, 1f)).Should().Be(new Color(1f, 0f, 0f, 1f));
    }

    [Fact]
    public void Lerp_Interpolates()
    {
        Color.Lerp(Color.Black, Color.White, 0.5f).ApproximatelyEquals(new Color(0.5f, 0.5f, 0.5f, 1f), 1e-6f).Should().BeTrue();
        Color.Lerp(Color.Black, Color.White, 2f).Should().Be(Color.White);
    }

    [Fact]
    public void Lighten_Darken_Saturate()
    {
        var gray = new Color(0.5f, 0.5f, 0.5f, 1f);
        gray.Lighten(0.2f).ToHsl().L.Should().BeGreaterThan(0.5f);
        gray.Darken(0.2f).ToHsl().L.Should().BeLessThan(0.5f);
        Color.Red.Saturate(0f).ApproximatelyEquals(Color.Red, 1e-6f).Should().BeTrue();
        Color.Red.Desaturate(1f).Should().Be(new Color(0.5f, 0.5f, 0.5f, 1f));
        Color.Red.AdjustHue(120f).ToHsl().H.Should().BeApproximately(120f, 1e-3f);
    }

    [Fact]
    public void Invert_And_Grayscale()
    {
        Color.Red.Invert().Should().Be(Color.Cyan);
        Color gray = new Color(0.2f, 0.4f, 0.6f, 0.8f).Grayscale();
        gray.R.Should().BeApproximately(0.299f * 0.2f + 0.587f * 0.4f + 0.114f * 0.6f, 1e-6f);
        gray.A.Should().Be(0.8f);
    }

    [Fact]
    public void Blend_Modes()
    {
        Color.Blend(Color.White, Color.Red, ColorBlendMode.Normal).Should().Be(Color.Red);
        Color.Blend(Color.White, Color.Red, ColorBlendMode.Multiply).Should().Be(Color.Red);
        Color.Blend(Color.Black, Color.Red, ColorBlendMode.Screen).Should().Be(Color.Red);
        Color.Blend(Color.Black, Color.White, ColorBlendMode.Difference).Should().Be(Color.White);
        Action bad = () => Color.Blend(Color.Black, Color.White, (ColorBlendMode)99);
        bad.Should().Throw<ArgumentOutOfRangeException>();
    }
}
