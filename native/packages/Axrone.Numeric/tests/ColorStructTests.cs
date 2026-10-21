using System.Runtime.Intrinsics;

namespace Axrone.Numeric.Tests;

public sealed class ColorNominalTests
{
    [Fact]
    public void Nominals_Clamp_And_Compare()
    {
        ((float)new NormalizedFloat(1.5f)).Should().Be(1f);
        ((float)new NormalizedFloat(-1f)).Should().Be(0f);
        ((float)new HueDegrees(720f)).Should().Be(0f);
        ((float)new HueDegrees(-90f)).Should().Be(270f);
        ((float)new ChromaFloat(-2f)).Should().Be(0f);
        new HueDegrees(90f).ToRadians().Should().BeApproximately(MathF.PI / 2f, 1e-6f);
        HueDegrees.FromRadians(MathF.PI).Value.Should().BeApproximately(180f, 1e-4f);
        (new NormalizedFloat(0.1f) < new NormalizedFloat(0.2f)).Should().BeTrue();
        (new HueDegrees(10f) <= new HueDegrees(10f)).Should().BeTrue();
        new ChromaFloat(0.5f).ToString().Should().Be("0.5000");
    }
}

public sealed class Rgba32Tests
{
    [Fact]
    public void Packs_And_Compares_Bitwise()
    {
        var color = new Rgba32(255, 128, 0, 255);
        color.ToPackedUInt32().Should().Be(0xFF0080FFu);
        new Rgba32(color.ToPackedUInt32()).Should().Be(color);
        (color == new Rgba32(255, 128, 0, 255)).Should().BeTrue();
        (color != Rgba32Tests_Black()).Should().BeTrue();
    }

    private static Rgba32 Rgba32Tests_Black() => new(0, 0, 0, 255);

    [Fact]
    public void Formats_Hex_With_Alpha_Rules()
    {
        new Rgba32(255, 0, 0, 255).ToString().Should().Be("#FF0000");
        new Rgba32(255, 0, 0, 128).ToString().Should().Be("#FF000080");
        Span<char> buffer = stackalloc char[9];
        new Rgba32(255, 0, 0, 255).TryFormat(buffer, out int written, "x6".AsSpan()).Should().BeTrue();
        new string(buffer[..written]).Should().Be("#ff0000");

        Span<byte> utf8 = stackalloc byte[9];
        new Rgba32(255, 0, 0, 128).TryFormat(utf8, out int utf8Written, "X8".AsSpan()).Should().BeTrue();
        System.Text.Encoding.UTF8.GetString(utf8[..utf8Written]).Should().Be("#FF000080");
    }

    [Theory]
    [InlineData("#f00", 255, 0, 0, 255)]
    [InlineData("#f008", 255, 0, 0, 136)]
    [InlineData("#ff0000", 255, 0, 0, 255)]
    [InlineData("#ff000080", 255, 0, 0, 128)]
    [InlineData("00ff00", 0, 255, 0, 255)]
    public void Parses_Hex_Forms(string text, byte r, byte g, byte b, byte a)
    {
        Rgba32.Parse(text).Should().Be(new Rgba32(r, g, b, a));
        Rgba32.Parse(System.Text.Encoding.UTF8.GetBytes(text)).Should().Be(new Rgba32(r, g, b, a));
    }

    [Fact]
    public void Rejects_Bad_Input()
    {
        Rgba32.TryParse("".AsSpan(), null, out _).Should().BeFalse();
        Rgba32.TryParse("#ggg".AsSpan(), null, out _).Should().BeFalse();
        Rgba32.TryParse("#ff000".AsSpan(), null, out _).Should().BeFalse();
        Action bad = () => Rgba32.Parse("nope");
        bad.Should().Throw<FormatException>();
    }
}

public sealed class Rgba128FloatTests
{
    [Fact]
    public void Simd_RoundTrip_And_Clamp()
    {
        var color = new Rgba128Float(2f, -1f, 0.5f, 1.5f);
        Vector128.GetElement(color.AsVector128(), 0).Should().Be(2f);
        new Rgba128Float(color.AsVector128()).Should().Be(color);
        Rgba128Float clamped = color.Clamp();
        clamped.R.Should().Be(1f);
        clamped.G.Should().Be(0f);
        clamped.A.Should().Be(1f);
        clamped.ToString().Should().Contain("Rgba128Float");
    }

    [Fact]
    public void Formats_Through_Srgb()
    {
        var color = new Rgba128Float(1f, 0f, 0f, 1f);
        Span<char> buffer = stackalloc char[9];
        color.TryFormat(buffer, out int written).Should().BeTrue();
        new string(buffer[..written]).Should().Be("#FF0000");
    }
}

public sealed class OklabTests
{
    [Fact]
    public void Clamp_Bounds_Channels()
    {
        var clamped = new Oklab(2f, 9f, -9f, 3f).Clamp();
        clamped.L.Should().Be(1f);
        clamped.A.Should().Be(0.4f);
        clamped.B.Should().Be(-0.4f);
        clamped.Alpha.Should().Be(1f);
    }

    [Fact]
    public void Oklch_Normalizes_Hue()
    {
        new Oklch(0.5f, 0.1f, 720f).H.Should().Be(0f);
        new Oklch(0.5f, 0.1f, -90f).H.Should().Be(270f);
        var left = new Oklch(0.5f, 0.1f, 10f);
        var right = new Oklch(0.5f, 0.1f, 10f);
        (left == right).Should().BeTrue();
        (left != new Oklch(0.5f, 0.1f, 11f)).Should().BeTrue();
    }
}
