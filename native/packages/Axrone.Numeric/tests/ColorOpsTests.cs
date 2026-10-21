namespace Axrone.Numeric.Tests;

public sealed class ColorOpsTests
{
    [Fact]
    public void Conversions_RoundTrip()
    {
        var original = new Rgba128Float(0.2f, 0.4f, 0.6f, 1f);

        HslColor hsl = ColorOps.ToHsl(original);
        ColorOps.ApproximatelyEquals(ColorOps.FromHsl(hsl.H, hsl.S, hsl.L, hsl.A), original, 1e-5f).Should().BeTrue();

        HsvColor hsv = ColorOps.ToHsv(original);
        ColorOps.ApproximatelyEquals(ColorOps.FromHsv(hsv.H, hsv.S, hsv.V, hsv.A), original, 1e-5f).Should().BeTrue();

        CmykColor cmyk = ColorOps.ToCmyk(original);
        ColorOps.ApproximatelyEquals(ColorOps.FromCmyk(cmyk.C, cmyk.M, cmyk.Y, cmyk.K, cmyk.A), original, 1e-5f).Should().BeTrue();

        XyzColor xyz = ColorOps.ToXyz(original);
        ColorOps.ApproximatelyEquals(ColorOps.FromXyz(xyz.X, xyz.Y, xyz.Z, xyz.Alpha), original, 1e-3f).Should().BeTrue();

        LabColor lab = ColorOps.ToLab(original);
        lab.L.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Temperature_Is_Warm_And_Cool()
    {
        Rgba128Float warm = ColorOps.FromTemperature(2700f);
        Rgba128Float cool = ColorOps.FromTemperature(7500f);
        (warm.R > cool.R).Should().BeTrue();
        (cool.B > warm.B).Should().BeTrue();
    }

    [Fact]
    public void Named_Colors_Resolve()
    {
        ColorOps.TryGetNamedColor("  Light Gray ", out Rgba32 gray).Should().BeTrue();
        gray.Should().Be(new Rgba32(191, 191, 191, 255));
        ColorOps.TryGetNamedColor("blurple", out _).Should().BeFalse();
        ColorOps.GetNamedColor("red").Should().Be(new Rgba32(255, 0, 0, 255));
        Action unknown = () => ColorOps.GetNamedColor("blurple");
        unknown.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Blend_Modes()
    {
        Rgba128Float white = new(1f, 1f, 1f, 1f);
        Rgba128Float black = new(0f, 0f, 0f, 1f);
        ColorBlending.Blend(white, new Rgba128Float(1f, 0f, 0f, 1f), ColorBlendMode.Normal).Should().Be(new Rgba128Float(1f, 0f, 0f, 1f));
        ColorBlending.Blend(black, new Rgba128Float(1f, 0f, 0f, 1f), ColorBlendMode.Screen).Should().Be(new Rgba128Float(1f, 0f, 0f, 1f));
        ColorOps.ApproximatelyEquals(
            ColorBlending.Blend(ColorOps.FromHsl(0f, 1f, 0.5f, 1f), ColorOps.FromHsl(0f, 1f, 0.5f, 1f), ColorBlendMode.Multiply),
            new Rgba128Float(1f, 0f, 0f, 1f),
            1e-4f).Should().BeTrue();
        Action bad = () => ColorBlending.Blend(white, black, (ColorBlendMode)99);
        bad.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Harmony_Generates_Expected_Counts()
    {
        ColorOps.GenerateHarmony(new Rgba128Float(1f, 0f, 0f, 1f), ColorHarmonyType.Complementary).Should().HaveCount(2);
        ColorOps.GenerateHarmony(new Rgba128Float(1f, 0f, 0f, 1f), ColorHarmonyType.Triadic).Should().HaveCount(3);
        ColorOps.GenerateHarmony(new Rgba128Float(1f, 0f, 0f, 1f), ColorHarmonyType.Monochromatic).Should().HaveCount(5);
        ColorOps.GenerateHarmony(new Rgba128Float(1f, 0f, 0f, 1f), ColorHarmonyType.Analogous, 3).Should().HaveCount(3);
        Action bad = () => ColorOps.GenerateHarmony(new Rgba128Float(1f, 0f, 0f, 1f), (ColorHarmonyType)99);
        bad.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Metrics_Behave()
    {
        ColorOps.Luminance(new Rgba128Float(0f, 0f, 0f, 1f)).Should().BeApproximately(0f, 1e-6f);
        ColorOps.ContrastRatio(new Rgba128Float(0f, 0f, 0f, 1f), new Rgba128Float(1f, 1f, 1f, 1f)).Should().BeApproximately(21f, 1e-4f);
        ColorOps.IsAccessible(new Rgba128Float(0f, 0f, 0f, 1f), new Rgba128Float(1f, 1f, 1f, 1f)).Should().BeTrue();
        ColorOps.Distance(new Rgba128Float(0f, 0f, 0f, 1f), new Rgba128Float(1f, 1f, 1f, 1f)).Should().BeApproximately(MathF.Sqrt(3f), 1e-5f);
        ColorOps.Compare(new Rgba128Float(0f, 0f, 0f, 1f), new Rgba128Float(1f, 1f, 1f, 1f)).Should().Be(-1);
        ColorOps.ApproximatelyEquals(new Rgba128Float(1f, 0.0001f, 0f, 1f), new Rgba128Float(1f, 0f, 0f, 1f), 1e-3f).Should().BeTrue();
    }

    [Fact]
    public void Random_Stays_In_Range()
    {
        for (int i = 0; i < 16; i++)
        {
            Rgba128Float c = ColorOps.Random();
            c.R.Should().BeGreaterThanOrEqualTo(0f);
            c.R.Should().BeLessThanOrEqualTo(1f);
            c.A.Should().Be(1f);
            ColorOps.ToHsl(ColorOps.RandomPastel()).L.Should().BeGreaterThanOrEqualTo(0.7f);
            ColorOps.ToHsl(ColorOps.RandomVibrant()).S.Should().BeGreaterThanOrEqualTo(0.8f);
        }
    }
}

