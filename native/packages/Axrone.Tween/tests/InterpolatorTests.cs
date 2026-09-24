namespace Axrone.Tween.Tests;

using System.Numerics;

public class InterpolatorTests
{
    [Fact]
    public void Float_MidpointAndEndpoints()
    {
        FloatInterpolator.Interpolate(0f, 10f, 0f).Should().BeApproximately(0f, 1e-6f);
        FloatInterpolator.Interpolate(0f, 10f, 0.5f).Should().BeApproximately(5f, 1e-6f);
        FloatInterpolator.Interpolate(0f, 10f, 1f).Should().BeApproximately(10f, 1e-6f);
    }

    [Fact]
    public void Vector2_Midpoint()
    {
        var result = Vector2Interpolator.Interpolate(new Vector2(0f, 0f), new Vector2(4f, 8f), 0.5f);

        result.X.Should().BeApproximately(2f, 1e-6f);
        result.Y.Should().BeApproximately(4f, 1e-6f);
    }

    [Fact]
    public void Vector3_Midpoint()
    {
        var result = Vector3Interpolator.Interpolate(new Vector3(0f, 0f, 0f), new Vector3(2f, 4f, 6f), 0.5f);

        result.X.Should().BeApproximately(1f, 1e-6f);
        result.Y.Should().BeApproximately(2f, 1e-6f);
        result.Z.Should().BeApproximately(3f, 1e-6f);
    }

    [Fact]
    public void Vector4_Midpoint()
    {
        var result = Vector4Interpolator.Interpolate(new Vector4(0f, 0f, 0f, 0f), new Vector4(2f, 4f, 6f, 8f), 0.5f);

        result.W.Should().BeApproximately(4f, 1e-6f);
    }
}
