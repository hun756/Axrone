namespace Axrone.Render.Effects.Tests;

/// <summary>
/// Tests for <see cref="BloomMath"/>: the CPU side of the Karis soft-knee bloom
/// prefilter curve and the mip-pyramid level derivation.
/// </summary>
public sealed class BloomMathTests
{
    // =========================================================================
    // Luminance
    // =========================================================================

    [Theory]
    [InlineData(1f, 0f, 0f, 0.2126f)]
    [InlineData(0f, 1f, 0f, 0.7152f)]
    [InlineData(0f, 0f, 1f, 0.0722f)]
    public void Luminance_UsesRec709Weights(float r, float g, float b, float expected)
    {
        BloomMath.Luminance(r, g, b).Should().BeApproximately(expected, 1e-6f);
    }

    [Fact]
    public void Luminance_BlackIsZeroAndWhiteIsOne()
    {
        BloomMath.Luminance(0f, 0f, 0f).Should().Be(0f);
        BloomMath.Luminance(1f, 1f, 1f).Should().BeApproximately(1f, 1e-6f);
    }

    [Fact]
    public void Luminance_WeightsSumToOne()
    {
        (BloomMath.LumaR + BloomMath.LumaG + BloomMath.LumaB).Should().BeApproximately(1f, 1e-6f);
    }

    [Fact]
    public void Luminance_GreenDominatesEqualEnergyRed()
    {
        BloomMath.Luminance(0f, 1f, 0f).Should().BeGreaterThan(BloomMath.Luminance(1f, 0f, 0f));
    }

    // =========================================================================
    // Karis weight
    // =========================================================================

    [Fact]
    public void KarisWeight_MatchesInverseOnePlusLuminance()
    {
        BloomMath.KarisWeight(0f).Should().Be(1f);
        BloomMath.KarisWeight(1f).Should().BeApproximately(0.5f, 1e-6f);
        BloomMath.KarisWeight(3f).Should().BeApproximately(0.25f, 1e-6f);
    }

    [Fact]
    public void KarisWeight_ClampsNegativeLuminanceToZero()
    {
        BloomMath.KarisWeight(-5f).Should().Be(1f, "a negative (impossible) luminance must not amplify the weight");
    }

    [Fact]
    public void KarisWeight_IsMonotonicallyDecreasing()
    {
        BloomMath.KarisWeight(0.5f).Should().BeLessThan(BloomMath.KarisWeight(0.25f));
        BloomMath.KarisWeight(4f).Should().BeLessThan(BloomMath.KarisWeight(2f));
    }

    // =========================================================================
    // Knee width
    // =========================================================================

    [Theory]
    [InlineData(1f, 0.5f, 0.5f)]
    [InlineData(2f, 0.5f, 1f)]
    [InlineData(1f, 0f, 0f)]
    [InlineData(0f, 0.5f, 0f)]
    [InlineData(-4f, 0.5f, 0f)]
    [InlineData(1f, 1f, 1f)]
    [InlineData(1f, 2f, 1f)]
    [InlineData(1f, -1f, 0f)]
    public void ComputeKneeWidth_MatchesMaxZeroTimesClampedKnee(float threshold, float softKnee, float expected)
    {
        BloomMath.ComputeKneeWidth(threshold, softKnee).Should().BeApproximately(expected, 1e-6f);
    }

    [Fact]
    public void ComputeKneeWidth_DefaultSoftKneeIsHalf()
    {
        BloomMath.DefaultSoftKnee.Should().Be(0.5f);
        BloomMath.ComputeKneeWidth(4f, BloomMath.DefaultSoftKnee).Should().BeApproximately(2f, 1e-6f);
    }

    // =========================================================================
    // Soft-knee contribution
    // =========================================================================

    [Fact]
    public void SoftKneeContribution_ZeroKnee_IsHardThreshold()
    {
        BloomMath.SoftKneeContribution(0.9f, 1f, 0f).Should().Be(0f);
        BloomMath.SoftKneeContribution(1.1f, 1f, 0f).Should().Be(1f);
    }

    [Fact]
    public void SoftKneeContribution_ZeroKnee_BoundaryIsGuarded()
    {
        // The epsilon deadband keeps a tap sitting exactly on the threshold from
        // flickering: neither strictly-above nor exactly-at counts as bright.
        BloomMath.SoftKneeContribution(1f, 1f, 0f).Should().Be(0f);
        BloomMath.SoftKneeContribution(1f + (BloomMath.Epsilon * 0.5f), 1f, 0f).Should().Be(0f);
        BloomMath.SoftKneeContribution(1f + (BloomMath.Epsilon * 2f), 1f, 0f).Should().Be(1f);
    }

    [Fact]
    public void SoftKneeContribution_AtThreshold_IsQuarter()
    {
        BloomMath.SoftKneeContribution(2f, 2f, 0.5f).Should().BeApproximately(0.25f, 1e-4f);
    }

    [Fact]
    public void SoftKneeContribution_BelowKneeStart_IsZero()
    {
        // Knee width for threshold 2 at softKnee 0.5 is 1, so the ramp starts at 1.75.
        BloomMath.SoftKneeContribution(1.7f, 2f, 0.5f).Should().Be(0f);
    }

    [Fact]
    public void SoftKneeContribution_AboveKneeEnd_IsOne()
    {
        BloomMath.SoftKneeContribution(2.3f, 2f, 0.5f).Should().Be(1f);
    }

    [Fact]
    public void SoftKneeContribution_RampIsMonotonicAndBounded()
    {
        float previous = -1f;
        for (float luma = 1.6f; luma <= 2.4f; luma += 0.02f)
        {
            float c = BloomMath.SoftKneeContribution(luma, 2f, 0.5f);
            c.Should().BeGreaterThanOrEqualTo(0f);
            c.Should().BeLessThanOrEqualTo(1f);
            c.Should().BeGreaterThanOrEqualTo(previous);
            previous = c;
        }
    }

    [Fact]
    public void SoftKneeContribution_NegativeThresholdDegeneratesToHardCut()
    {
        // Threshold clamps to 0, so a negative knee is the hard-threshold branch.
        BloomMath.SoftKneeContribution(0.5f, -1f, 0.5f).Should().Be(1f);
        BloomMath.SoftKneeContribution(0f, -1f, 0.5f).Should().Be(0f);
    }

    // =========================================================================
    // Mip count
    // =========================================================================

    [Fact]
    public void ComputeMipCount_HdSource_ClampsToMax()
    {
        BloomMath.ComputeMipCount(1280, 720, BloomMath.MaxMipCount).Should().Be(8);
        BloomMath.ComputeMipCount(1280, 720, 99).Should().Be(8, "an over-large request clamps to the pyramid bound");
    }

    [Fact]
    public void ComputeMipCount_TinySource_StopsAtTwoLevels()
    {
        BloomMath.ComputeMipCount(4, 4, BloomMath.MaxMipCount).Should().Be(2);
    }

    [Fact]
    public void ComputeMipCount_SinglePixel_StillYieldsTwoLevels()
    {
        BloomMath.ComputeMipCount(1, 1, BloomMath.MaxMipCount).Should().Be(2);
    }

    [Fact]
    public void ComputeMipCount_UnderRequest_ClampsToMin()
    {
        BloomMath.ComputeMipCount(1280, 720, 0).Should().Be(2);
        BloomMath.ComputeMipCount(1280, 720, -5).Should().Be(2);
    }

    [Fact]
    public void ComputeMipCount_SmallSource_IsSizeLimitedNotRequestLimited()
    {
        // 64x64 halves to 32, 16, 8, 4, 2, 1 -> six levels before hitting 1x1.
        BloomMath.ComputeMipCount(64, 64, BloomMath.MaxMipCount).Should().Be(6);
    }

    [Fact]
    public void ComputeMipCount_NonSquareSource_TracksTheLongerAxis()
    {
        // 1024x4: level 0 is 512x2, and the chain ends once both axes reach 1.
        int levels = BloomMath.ComputeMipCount(1024, 4, BloomMath.MaxMipCount);
        levels.Should().Be(BloomMath.MaxMipCount);
    }

    [Fact]
    public void ComputeMipCount_IsDeterministicForTheSameInputs()
    {
        BloomMath.ComputeMipCount(1920, 1080, 6).Should().Be(BloomMath.ComputeMipCount(1920, 1080, 6));
    }
}
