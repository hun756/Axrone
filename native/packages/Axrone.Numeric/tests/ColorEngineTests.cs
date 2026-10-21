namespace Axrone.Numeric.Tests;

public sealed class ColorEngineTests
{
    [Fact]
    public void Universal_RoundTrips_Each_Arm()
    {
        var packed = new UniversalColor(new Rgba32(255, 0, 0, 255));
        packed.Kind.Should().Be(ColorSpaceKind.Rgba32);
        packed.TryGetRgba32(out Rgba32 back).Should().BeTrue();
        back.Should().Be(new Rgba32(255, 0, 0, 255));
        packed.TryGetOklab(out _).Should().BeFalse();
        packed.ToLinearRgb().R.Should().BeApproximately(1f, 1e-5f);

        var linear = new UniversalColor(new Rgba128Float(1f, 0f, 0f, 1f));
        linear.ToRgba32().Should().Be(new Rgba32(255, 0, 0, 255));

        var lab = new UniversalColor(new Oklab(0.5f, 0.1f, -0.1f, 1f));
        lab.TryGetOklab(out Oklab labBack).Should().BeTrue();
        labBack.L.Should().BeApproximately(0.5f, 1e-6f);
        float linearR = lab.ToLinearRgb().R;
        float.IsFinite(linearR).Should().BeTrue();

        var lch = new UniversalColor(new Oklch(0.5f, 0.1f, 90f, 1f));
        lch.TryGetOklch(out _).Should().BeTrue();
        lch.ToRgba32().A.Should().Be(255);

        packed.Should().Be(new UniversalColor(new Rgba32(255, 0, 0, 255)));
        packed.Should().NotBe(linear);
    }

    [Fact]
    public void Typestate_Enforces_Pipeline_Order()
    {
        var straight = TypestateColor<StraightAlpha, LinearTransfer>.CreateLinearStraight(new Rgba128Float(0.5f, 0.25f, 0.125f, 0.5f));
        TypestateColor<PremultipliedAlpha, LinearTransfer> premul = straight.Premultiply();
        premul.Value.R.Should().BeApproximately(0.25f, 1e-6f);
        TypestateColor<StraightAlpha, LinearTransfer> back = premul.Unpremultiply();
        back.Value.R.Should().BeApproximately(0.5f, 1e-6f);

        TypestateColor<StraightAlpha, NonLinearTransfer> encoded = straight.EncodeSrgb();
        TypestateColor<StraightAlpha, LinearTransfer> decoded = encoded.DecodeSrgb();
        decoded.Value.R.Should().BeApproximately(0.5f, 1e-2f);

        TypestateColor<PremultipliedAlpha, LinearTransfer> over = premul.BlendOver(premul);
        over.Value.A.Should().BeApproximately(0.75f, 1e-6f);

        var gamma = TypestateColor<StraightAlpha, NonLinearTransfer>.CreateGammaStraight(new Rgba32(255, 0, 0, 255));
        gamma.Value.R.Should().Be(1f);
    }

    [Fact]
    public void Transformers_RoundTrip_Batches()
    {
        Rgba32[] src = [new(255, 0, 0, 255), new(0, 255, 0, 255)];
        Rgba128Float[] linear = new Rgba128Float[2];
        SrgbToLinearTransformer.TransformBatch(src, linear);
        linear[0].R.Should().BeApproximately(1f, 1e-6f);
        linear[1].G.Should().BeApproximately(1f, 1e-6f);

        Rgba32[] back = new Rgba32[2];
        LinearToSrgbTransformer.TransformBatch(linear, back);
        back.Should().BeEquivalentTo(src);

        Oklab[] lab = new Oklab[2];
        LinearToOklabTransformer.TransformBatch(linear, lab);
        Rgba128Float[] round = new Rgba128Float[2];
        OklabToLinearTransformer.TransformBatch(lab, round);
        round[0].R.Should().BeApproximately(1f, 1e-3f);
    }

    [Fact]
    public void Spans_Iterate_Without_Allocating()
    {
        Rgba128Float[] colors = [new(1f, 0f, 0f, 1f), new(0f, 1f, 0f, 1f)];
        var span = new ColorSpan<Rgba128Float>(colors);
        span.Length.Should().Be(2);
        int count = 0;
        foreach (ref Rgba128Float c in span)
        {
            count++;
            c.A.Should().Be(1f);
        }

        count.Should().Be(2);
        var readOnly = new ReadOnlyColorSpan<Rgba128Float>(colors);
        int seen = 0;
        foreach (ref readonly Rgba128Float c in readOnly)
        {
            seen++;
        }

        seen.Should().Be(2);
    }

    [Fact]
    public void Config_Validates()
    {
        var config = new ColorRenderingConfig();
        config.Exposure.Should().Be(1f);
        config.Gamma.Should().Be(2.4f);
        Action bad = () => config.Exposure = 0f;
        bad.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Palette_Matches_Reference()
    {
        Colors.Red.Should().Be(new Rgba32(255, 0, 0, 255));
        Colors.Lime.Should().Be(new Rgba32(0, 255, 0, 255));
        Colors.Gray.Should().Be(new Rgba32(128, 128, 128, 255));
    }

    [Fact]
    public void Premultiply_RoundTrips()
    {
        var color = new Rgba128Float(1f, 0.5f, 0.25f, 0.5f);
        Rgba128Float premul = ColorBlending.Premultiply(color);
        premul.R.Should().BeApproximately(0.5f, 1e-6f);
        ColorBlending.Unpremultiply(premul).R.Should().BeApproximately(1f, 1e-6f);
        ColorBlending.BlendStraightOver(color, new Rgba128Float(0f, 0f, 1f, 1f)).A.Should().Be(1f);
    }

    [Fact]
    public void Batch_Composite_Matches_Scalar()
    {
        Rgba128Float[] src = [new(1f, 0f, 0f, 0.5f), new(0f, 1f, 0f, 1f)];
        Rgba128Float[] dst = [new(0f, 0f, 1f, 1f), new(0f, 0f, 1f, 1f)];
        Rgba128Float[] target = new Rgba128Float[2];
        ColorBlending.BatchBlendPremultipliedOver(src, dst, target);
        target[0].Should().Be(ColorBlending.BlendPremultipliedOver(src[0], dst[0]));
        target[1].Should().Be(ColorBlending.BlendPremultipliedOver(src[1], dst[1]));
    }
}
