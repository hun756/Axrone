namespace Axrone.Asset2D.Tests;

public sealed class Asset2DNominalTests
{
    [Fact]
    public void Pixels_Arithmetic_Comparisons_And_Format()
    {
        Pixels a = 10f;
        Pixels b = 4f;
        (a + b).Value.Should().Be(14f);
        (a - b).Value.Should().Be(6f);
        (a * 2f).Value.Should().Be(20f);
        (a / 4f).Value.Should().Be(2.5f);
        (a > b).Should().BeTrue();
        (a >= b).Should().BeTrue();
        (b < a).Should().BeTrue();
        (b <= a).Should().BeTrue();
        ((float)a).Should().Be(10f);
        Pixels.Zero.Value.Should().Be(0f);
        a.ToString().Should().Be("10.00px");
    }

    [Fact]
    public void Milliseconds_Arithmetic_And_Format()
    {
        Milliseconds a = 100f;
        Milliseconds b = 30f;
        (a + b).Value.Should().Be(130f);
        (a - b).Value.Should().Be(70f);
        (a * 2f).Value.Should().Be(200f);
        (a / 2f).Value.Should().Be(50f);
        (a > b).Should().BeTrue();
        ((float)a).Should().Be(100f);
        Milliseconds.Zero.Value.Should().Be(0f);
        a.ToString().Should().Be("100.0ms");
    }

    [Fact]
    public void NormalizedUv_Bounds_And_Format()
    {
        NormalizedUv.Zero.Value.Should().Be(0f);
        NormalizedUv.One.Value.Should().Be(1f);
        NormalizedUv half = 0.5f;
        (half > NormalizedUv.Zero).Should().BeTrue();
        (half < NormalizedUv.One).Should().BeTrue();
        ((float)half).Should().Be(0.5f);
        half.ToString().Should().Be("0.5000");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void String_Nominals_Reject_Blank(string blank)
    {
        Action atlas = () => _ = new AtlasId(blank);
        Action texture = () => _ = new TextureId(blank);
        Action frame = () => _ = new FrameId(blank);
        Action clip = () => _ = new ClipId(blank);
        atlas.Should().Throw<Asset2DValidationException>();
        texture.Should().Throw<Asset2DValidationException>();
        frame.Should().Throw<Asset2DValidationException>();
        clip.Should().Throw<Asset2DValidationException>();
    }

    [Fact]
    public void String_Nominals_Ordinal_Equality_And_Span_Match()
    {
        var first = new FrameId("hero_idle_0");
        var second = new FrameId("hero_idle_0");
        first.Should().Be(second);
        first.Equals("hero_idle_0".AsSpan()).Should().BeTrue();
        first.Equals("hero_idle_1".AsSpan()).Should().BeFalse();
        first.GetHashCode().Should().Be(second.GetHashCode());
        first.ToString().Should().Be("hero_idle_0");

        var clip = new ClipId("run");
        clip.Equals("run".AsSpan()).Should().BeTrue();
        clip.ToString().Should().Be("run");
        AtlasId atlas = "main";
        atlas.Value.Should().Be("main");
        TextureId texture = "main.png";
        texture.Value.Should().Be("main.png");
    }

    [Fact]
    public void Errors_Carry_Codes()
    {
        var validation = new Asset2DValidationException("bad");
        validation.Code.Should().Be(Asset2DValidationException.ValidationErrorCode);
        validation.Message.Should().Be("bad");

        var coded = new Asset2DException("import.failed", "boom");
        coded.Code.Should().Be("import.failed");
        coded.Message.Should().Be("boom");
    }
}
