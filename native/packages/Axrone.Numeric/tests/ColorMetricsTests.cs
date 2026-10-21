namespace Axrone.Numeric.Tests;

public sealed class ColorMetricsTests
{
    [Fact]
    public void Luminance_And_Contrast()
    {
        Color.Luminance(Color.Black).Should().BeApproximately(0f, 1e-6f);
        Color.Luminance(Color.White).Should().BeApproximately(1f, 1e-6f);
        Color.ContrastRatio(Color.Black, Color.White).Should().BeApproximately(21f, 1e-4f);
        Color.IsAccessible(Color.Black, Color.White).Should().BeTrue();
        Color.IsAccessible(Color.Black, Color.White, AccessibilityLevel.AAA).Should().BeTrue();
        Color.IsAccessible(new Color(0.5f, 0.5f, 0.5f, 1f), new Color(0.6f, 0.6f, 0.6f, 1f)).Should().BeFalse();
    }

    [Fact]
    public void Distance_Metrics()
    {
        Color.Distance(Color.Black, Color.White).Should().BeApproximately(MathF.Sqrt(3f), 1e-5f);
        Color.DistanceLab(Color.Black, Color.White).Should().BeGreaterThan(50f);
    }

    [Fact]
    public void Harmony_Generates_Expected_Counts()
    {
        Color.GenerateHarmony(Color.Red, ColorHarmonyType.Complementary).Should().HaveCount(2);
        Color.GenerateHarmony(Color.Red, ColorHarmonyType.Triadic).Should().HaveCount(3);
        Color.GenerateHarmony(Color.Red, ColorHarmonyType.Monochromatic).Should().HaveCount(5);
        Color.GenerateHarmony(Color.Red, ColorHarmonyType.Analogous, 3).Should().HaveCount(3);
        Action bad = () => Color.GenerateHarmony(Color.Red, (ColorHarmonyType)99);
        bad.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Random_Stays_In_Range()
    {
        for (int i = 0; i < 16; i++)
        {
            Color c = Color.Random();
            c.R.Should().BeInRange(0f, 1f);
            c.A.Should().Be(1f);
            Color pastel = Color.RandomPastel();
            pastel.ToHsl().L.Should().BeInRange(0.7f, 1f);
            Color vibrant = Color.RandomVibrant();
            vibrant.ToHsl().S.Should().BeInRange(0.8f, 1f);
        }
    }

    [Fact]
    public void Comparer_Orders_By_Mode()
    {
        var byLuminance = new ColorComparer(ColorComparisonMode.Luminance);
        byLuminance.Compare(Color.Black, Color.White).Should().Be(-1);
        byLuminance.Compare(Color.White, Color.Black).Should().Be(1);
        byLuminance.Compare(Color.Red, Color.Red).Should().Be(0);

        var byAlpha = new ColorComparer(ColorComparisonMode.Alpha);
        byAlpha.Compare(new Color(1f, 0f, 0f, 0f), Color.Red).Should().Be(-1);

        var byHue = new ColorComparer(ColorComparisonMode.Hue);
        byHue.Compare(Color.Red, Color.Red).Should().Be(0);

        var equality = new ColorEqualityComparer(1e-3f);
        equality.Equals(new Color(0.5001f, 0f, 0f, 1f), Color.Red).Should().BeFalse();
        equality.Equals(new Color(1f, 0.0001f, 0f, 1f), Color.Red).Should().BeTrue();
        ColorEqualityComparer.GetHashCode(Color.Red).Should().Be(Color.Red.GetHashCode());
    }
}
