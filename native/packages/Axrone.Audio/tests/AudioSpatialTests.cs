namespace Axrone.Audio.Tests;

public class AudioSpatialTests
{
    [Fact]
    public void DistanceGain_Follows_Model()
    {
        var inverse = new AudioSpatialAttenuation(AudioDistanceModel.Inverse, 1.0f, 100.0f, 1.0f, 0.0f);
        SpatialKinematics.CalculateDistanceGain(inverse, 2.0f).Should().BeApproximately(0.5f, 1e-5f);

        var linear = new AudioSpatialAttenuation(AudioDistanceModel.Linear, 1.0f, 101.0f, 1.0f, 0.0f);
        SpatialKinematics.CalculateDistanceGain(linear, 51.0f).Should().BeApproximately(0.5f, 1e-5f);

        var none = new AudioSpatialAttenuation(AudioDistanceModel.None);
        SpatialKinematics.CalculateDistanceGain(none, 500.0f).Should().Be(1.0f);
    }

    [Fact]
    public void ConeGain_FullCircle_IsUnity_Behind_IsOuter()
    {
        Vector3 source = Vector3.Zero;
        Vector3 forward = -Vector3.UnitZ;
        Vector3 ahead = new(0.0f, 0.0f, -10.0f);
        Vector3 behind = new(0.0f, 0.0f, 10.0f);

        SpatialKinematics.CalculateConeGain(source, forward, ahead, 360.0f, 360.0f, 0.0f).Should().Be(1.0f);
        SpatialKinematics.CalculateConeGain(source, forward, ahead, 30.0f, 60.0f, 0.2f).Should().Be(1.0f);
        SpatialKinematics.CalculateConeGain(source, forward, behind, 30.0f, 60.0f, 0.2f).Should().BeApproximately(0.2f, 1e-5f);
    }

    [Fact]
    public void StereoPan_Sides_Oppose_Center_Neutral()
    {
        Vector3 listener = Vector3.Zero;
        Vector3 right = Vector3.UnitX;

        SpatialKinematics.CalculateStereoPan(listener, right, new Vector3(10.0f, 0.0f, 0.0f)).Should().BeApproximately(1.0f, 1e-5f);
        SpatialKinematics.CalculateStereoPan(listener, right, new Vector3(-10.0f, 0.0f, 0.0f)).Should().BeApproximately(-1.0f, 1e-5f);
        SpatialKinematics.CalculateStereoPan(listener, right, new Vector3(0.0f, 0.0f, -10.0f)).Should().BeApproximately(0.0f, 1e-5f);
    }

    [Fact]
    public void SafeNormalize_Degenerate_IsZero()
    {
        SpatialKinematics.SafeNormalize(Vector3.Zero).Should().Be(Vector3.Zero);
        SpatialKinematics.SafeNormalize(new Vector3(3.0f, 0.0f, 4.0f)).Length().Should().BeApproximately(1.0f, 1e-5f);
    }

    [Fact]
    public void Spatialization_Defaults_Match_Modes()
    {
        AudioSpatialization.Default2D.Mode.Should().Be(AudioSpatialMode.TwoDimensional);
        AudioSpatialization.Default3D.Mode.Should().Be(AudioSpatialMode.ThreeDimensional);
        AudioSpatialization.Default3D.Attenuation.Model.Should().Be(AudioDistanceModel.Inverse);
    }
}
