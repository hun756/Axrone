using System;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class QuatMatrixTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void ToMat4_Identity_IsIdentity()
    {
        Quat.Identity.ToMat4().Should().Be(Mat4.Identity);
    }

    [Fact]
    public void ToMat4_RotationZ90_MatchesHandValues()
    {
        Quat rotation = Quat.CreateFromAxisAngle(new Vec3(0f, 0f, 1f), MathF.PI / 2f);

        Mat4 matrix = rotation.ToMat4();

        matrix.M11.Should().BeApproximately(0f, Tolerance);
        matrix.M12.Should().BeApproximately(1f, Tolerance);
        matrix.M21.Should().BeApproximately(-1f, Tolerance);
        matrix.M22.Should().BeApproximately(0f, Tolerance);
        matrix.M33.Should().BeApproximately(1f, Tolerance);
        matrix.M44.Should().BeApproximately(1f, Tolerance);
    }

    [Fact]
    public void ToMat4_RoundTripsThroughCreateFromRotationMatrix()
    {
        Quat rotation = Quat.Normalize(new Quat(0.3f, -0.5f, 0.2f, 0.9f));

        Quat recovered = Quat.CreateFromRotationMatrix(rotation.ToMat4());

        recovered.X.Should().BeApproximately(rotation.X, Tolerance);
        recovered.Y.Should().BeApproximately(rotation.Y, Tolerance);
        recovered.Z.Should().BeApproximately(rotation.Z, Tolerance);
        recovered.W.Should().BeApproximately(rotation.W, Tolerance);
    }

    [Fact]
    public void CreateFromRotationMatrix_Mat4_AgreesWithMatrix4x4Overload()
    {
        Mat4 matrix = Mat4.CreateFromYawPitchRoll(0.4f, -0.2f, 0.9f);

        Quat fromMat4 = Quat.CreateFromRotationMatrix(matrix);
        Quat fromSystem = Quat.CreateFromRotationMatrix(matrix.ToSystemNumerics());

        fromMat4.X.Should().BeApproximately(fromSystem.X, Tolerance);
        fromMat4.Y.Should().BeApproximately(fromSystem.Y, Tolerance);
        fromMat4.Z.Should().BeApproximately(fromSystem.Z, Tolerance);
        fromMat4.W.Should().BeApproximately(fromSystem.W, Tolerance);
    }
}
