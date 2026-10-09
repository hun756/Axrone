using System;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class PlaneTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void CreateFromVertices_XyPlane_YieldsPositiveZNormal()
    {
        Plane plane = Plane.CreateFromVertices(new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f));

        plane.Normal.Should().Be(new Vec3(0f, 0f, 1f));
        plane.D.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void Normalize_ScalesNormalAndConstant()
    {
        Plane plane = Plane.Normalize(new Plane(new Vec3(0f, 0f, 4f), 8f));

        plane.Normal.Should().Be(new Vec3(0f, 0f, 1f));
        plane.D.Should().BeApproximately(2f, Tolerance);
    }

    [Fact]
    public void DotCoordinate_SignsSides()
    {
        var plane = new Plane(new Vec3(0f, 1f, 0f), -1f);

        Plane.DotCoordinate(plane, new Vec3(0f, 2f, 0f)).Should().BeApproximately(1f, Tolerance);
        Plane.DotCoordinate(plane, new Vec3(0f, 0f, 0f)).Should().BeApproximately(-1f, Tolerance);
        Plane.DotCoordinate(plane, new Vec3(0f, 1f, 0f)).Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void Transform_Translation_ShiftsConstant()
    {
        var plane = new Plane(new Vec3(0f, 1f, 0f), 0f);
        Mat4 matrix = Mat4.CreateTranslation(new Vec3(0f, 5f, 0f));

        Plane transformed = Plane.Transform(plane, matrix);

        transformed.Normal.X.Should().BeApproximately(0f, Tolerance);
        transformed.Normal.Y.Should().BeApproximately(1f, Tolerance);
        transformed.Normal.Z.Should().BeApproximately(0f, Tolerance);
        transformed.D.Should().BeApproximately(-5f, Tolerance);
    }

    [Fact]
    public void Transform_MatchesSystemNumerics()
    {
        var plane = new Plane(new Vec3(0f, 0f, 1f), -2f);
        Mat4 matrix = Mat4.CreateFromYawPitchRoll(0.4f, -0.2f, 0.9f);

        Plane actual = Plane.Transform(plane, matrix);
        System.Numerics.Plane expected = System.Numerics.Plane.Transform(
            new System.Numerics.Plane(0f, 0f, 1f, -2f), matrix.ToSystemNumerics());

        actual.Normal.X.Should().BeApproximately(expected.Normal.X, Tolerance);
        actual.Normal.Y.Should().BeApproximately(expected.Normal.Y, Tolerance);
        actual.Normal.Z.Should().BeApproximately(expected.Normal.Z, Tolerance);
        actual.D.Should().BeApproximately(expected.D, Tolerance);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        new Plane(new Vec3(0f, 1f, 0f), 2f).Should().Be(new Plane(new Vec3(0f, 1f, 0f), 2f));
        (new Plane(new Vec3(0f, 1f, 0f), 2f) == new Plane(new Vec3(0f, 1f, 0f), 2f)).Should().BeTrue();
        (new Plane(new Vec3(0f, 1f, 0f), 2f) != new Plane(new Vec3(0f, 1f, 0f), 3f)).Should().BeTrue();
    }
}
