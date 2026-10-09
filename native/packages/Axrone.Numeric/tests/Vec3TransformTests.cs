using System;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec3TransformTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Transform_Identity_Preserves()
    {
        Vec3.Transform(new Vec3(1f, 2f, 3f), Mat4.Identity).Should().Be(new Vec3(1f, 2f, 3f));
    }

    [Fact]
    public void Transform_Translation_Shifts()
    {
        Mat4 matrix = Mat4.CreateTranslation(new Vec3(10f, -2f, 0.5f));

        Vec3.Transform(new Vec3(1f, 2f, 3f), matrix).Should().Be(new Vec3(11f, 0f, 3.5f));
    }

    [Fact]
    public void Transform_Scale_Multiplies()
    {
        Mat4 matrix = Mat4.CreateScale(2f);

        Vec3.Transform(new Vec3(1f, 2f, 3f), matrix).Should().Be(new Vec3(2f, 4f, 6f));
    }

    [Fact]
    public void Transform_RotationZ90_MapsXToY()
    {
        Mat4 matrix = Mat4.CreateRotationZ(MathF.PI / 2f);

        Vec3 result = Vec3.Transform(new Vec3(1f, 0f, 0f), matrix);

        result.X.Should().BeApproximately(0f, Tolerance);
        result.Y.Should().BeApproximately(1f, Tolerance);
        result.Z.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void TransformNormal_IgnoresTranslation_AndNormalizes()
    {
        Mat4 matrix = Mat4.CreateTranslation(new Vec3(10f, 20f, 30f));

        Vec3.TransformNormal(new Vec3(0f, 0f, 5f), matrix).Should().Be(new Vec3(0f, 0f, 1f));
    }

    [Fact]
    public void Transform_Quat_RotatesZ90()
    {
        Quat rotation = Quat.CreateFromAxisAngle(new Vec3(0f, 0f, 1f), MathF.PI / 2f);

        Vec3 result = Vec3.Transform(new Vec3(1f, 0f, 0f), rotation);

        result.X.Should().BeApproximately(0f, Tolerance);
        result.Y.Should().BeApproximately(1f, Tolerance);
        result.Z.Should().BeApproximately(0f, Tolerance);
    }

    public static TheoryData<Vec3, Mat4> ParityCases => new()
    {
        { new Vec3(1f, 2f, 3f), Mat4.Identity },
        { new Vec3(-4f, 0.5f, 7f), Mat4.CreateTranslation(new Vec3(10f, -2f, 0.5f)) },
        { new Vec3(1f, -1f, 0.25f), Mat4.CreateScale(2f, 0.5f, -1f) },
        { new Vec3(3f, 1f, -2f), Mat4.CreateRotationZ(0.7f) * Mat4.CreateTranslation(new Vec3(5f, 5f, 5f)) },
        {
            new Vec3(0.3f, -9f, 4.25f),
            new Mat4(
                0.12f, 0.85f, -0.33f, 0f,
                0.61f, -0.07f, 0.44f, 0f,
                -0.29f, 0.53f, 0.91f, 0f,
                3.25f, -1.5f, 0.75f, 1f)
        },
    };

    [Theory]
    [MemberData(nameof(ParityCases))]
    public void Transform_MatchesSystemNumerics(Vec3 position, Mat4 matrix)
    {
        System.Numerics.Vector3 expected =
            System.Numerics.Vector3.Transform(position.ToSystemNumerics(), matrix.ToSystemNumerics());
        Vec3 actual = Vec3.Transform(position, matrix);

        actual.X.Should().BeApproximately(expected.X, Tolerance);
        actual.Y.Should().BeApproximately(expected.Y, Tolerance);
        actual.Z.Should().BeApproximately(expected.Z, Tolerance);
    }
}
