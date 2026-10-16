namespace Axrone.Audio.Tests;

public class AudioPolicyTests
{
    [Fact]
    public void Attenuation_Inside_RefDistance_IsUnity()
    {
        InverseAttenuationPolicy.ComputeGain(0.5f, 1.0f, 100.0f, 1.0f, 0.0f).Should().Be(1.0f);
        LinearAttenuationPolicy.ComputeGain(1.0f, 1.0f, 100.0f, 1.0f, 0.0f).Should().Be(1.0f);
        ExponentialAttenuationPolicy.ComputeGain(0.0f, 1.0f, 100.0f, 1.0f, 0.0f).Should().Be(1.0f);
        NoneAttenuationPolicy.ComputeGain(500.0f, 1.0f, 100.0f, 1.0f, 0.0f).Should().Be(1.0f);
    }

    [Fact]
    public void Attenuation_Decays_AndClamps()
    {
        float inverse = InverseAttenuationPolicy.ComputeGain(2.0f, 1.0f, 100.0f, 1.0f, 0.0f);
        inverse.Should().BeApproximately(0.5f, 1e-5f);

        float linear = LinearAttenuationPolicy.ComputeGain(51.0f, 1.0f, 101.0f, 1.0f, 0.0f);
        linear.Should().BeApproximately(0.5f, 1e-5f);

        float expo = ExponentialAttenuationPolicy.ComputeGain(2.0f, 1.0f, 100.0f, 1.0f, 0.0f);
        expo.Should().BeApproximately(0.5f, 1e-5f);

        InverseAttenuationPolicy.ComputeGain(1e6f, 1.0f, 100.0f, 1.0f, 0.1f).Should().BeApproximately(0.1f, 1e-5f);
    }

    [Fact]
    public void Panning_Center_Splits_Evenly_Edges_Isolate()
    {
        EqualPowerPanningPolicy.ComputeGains(0.0f, out float left, out float right);
        left.Should().BeApproximately(right, 1e-5f);

        EqualPowerPanningPolicy.ComputeGains(-1.0f, out left, out right);
        left.Should().BeApproximately(1.0f, 1e-5f);
        right.Should().BeApproximately(0.0f, 1e-5f);

        EqualPowerPanningPolicy.ComputeGains(1.0f, out left, out right);
        left.Should().BeApproximately(0.0f, 1e-5f);
        right.Should().BeApproximately(1.0f, 1e-5f);

        LinearPanningPolicy.ComputeGains(0.0f, out left, out right);
        left.Should().BeApproximately(0.5f, 1e-5f);
        right.Should().BeApproximately(0.5f, 1e-5f);

        LinearPanningPolicy.ComputeGains(-2.0f, out left, out right);
        left.Should().BeApproximately(1.0f, 1e-5f);
    }
}
