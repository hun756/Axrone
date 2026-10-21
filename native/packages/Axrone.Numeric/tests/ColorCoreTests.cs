namespace Axrone.Numeric.Tests;

public sealed class ColorCoreTests
{
    [Fact]
    public void Constants_Match_Web_Values()
    {
        Color.Red.Should().Be(new Color(1f, 0f, 0f, 1f));
        Color.Silver.R.Should().BeApproximately(0.7529f, 1e-4f);
        Color.Transparent.A.Should().Be(0f);
    }

    [Fact]
    public void Constructor_Clamps()
    {
        new Color(2f, -1f, 0.5f, 3f).Should().Be(new Color(1f, 0f, 0.5f, 1f));
    }

    [Fact]
    public void FromHex_Parses_All_Lengths()
    {
        Color.FromHex("#ff0000").Should().Be(Color.Red);
        Color.FromHex("#f00").Should().Be(Color.Red);
        Color half = Color.FromHex("#ff000080");
        half.A.Should().BeApproximately(0.502f, 1e-3f);
        Color.FromHex("#FF0000").Should().Be(Color.Red);
        Action badLength = () => Color.FromHex("#ff000");
        badLength.Should().Throw<FormatException>();
        Action badChars = () => Color.FromHex("#gggggg");
        badChars.Should().Throw<FormatException>();
    }

    [Fact]
    public void FromRgb_Auto_Detects_Bytes()
    {
        Color.FromRgb(255f, 0f, 0f).Should().Be(Color.Red);
        Color.FromRgb(1f, 0f, 0f).Should().Be(Color.Red);
    }

    [Fact]
    public void FromNamedColor_Resolves_And_Rejects()
    {
        Color.FromNamedColor("  Light Gray ").Should().Be(new Color(0.75f, 0.75f, 0.75f, 1f));
        Action unknown = () => Color.FromNamedColor("blurple");
        unknown.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void FromArray_Validates()
    {
        float[] values = [0.25f, 0.5f, 0.75f, 1f];
        Color.FromArray(values).Should().Be(new Color(0.25f, 0.5f, 0.75f, 1f));
        Color.FromArray(values, 1).Should().Be(new Color(0.5f, 0.75f, 1f, 1f));
        Action badOffset = () => Color.FromArray(values, -1);
        badOffset.Should().Throw<ArgumentOutOfRangeException>();
        Action shortArray = () => Color.FromArray([0.1f, 0.2f]);
        shortArray.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Space_RoundTrips()
    {
        var original = new Color(0.2f, 0.4f, 0.6f, 1f);
        HslColor hsl = original.ToHsl();
        Color.FromHsl(hsl.H, hsl.S, hsl.L, hsl.A).ApproximatelyEquals(original, 1e-5f).Should().BeTrue();
        HsvColor hsv = original.ToHsv();
        Color.FromHsv(hsv.H, hsv.S, hsv.V, hsv.A).ApproximatelyEquals(original, 1e-5f).Should().BeTrue();
        CmykColor cmyk = original.ToCmyk();
        Color.FromCmyk(cmyk.C, cmyk.M, cmyk.Y, cmyk.K, cmyk.A).ApproximatelyEquals(original, 1e-5f).Should().BeTrue();
        LabColor lab = original.ToLab();
        Color.FromLab(lab.L, lab.A, lab.B, lab.Alpha).ApproximatelyEquals(original, 1e-2f).Should().BeTrue();
        XyzColor xyz = original.ToXyz();
        Color.FromXyz(xyz.X, xyz.Y, xyz.Z, xyz.Alpha).ApproximatelyEquals(original, 1e-3f).Should().BeTrue();
    }

    [Fact]
    public void Temperature_Is_Warm_And_Cool()
    {
        Color warm = Color.FromTemperature(2700f);
        Color cool = Color.FromTemperature(7500f);
        (warm.R > cool.R).Should().BeTrue();
        (cool.B > warm.B).Should().BeTrue();
    }

    [Fact]
    public void Formatting_Matches_Web()
    {
        Color.Red.ToHex().Should().Be("#FF0000");
        new Color(1f, 0f, 0f, 0.5f).ToHex(includeAlpha: true).Should().Be("#FF000080");
        Color.Red.ToRgb().Should().Be("rgb(255, 0, 0)");
        Color.Red.ToString().Should().Be("#FF0000FF");
        Color.Red.ToHslString().Should().Be("hsla(0, 100%, 50%, 1)");
    }
}
