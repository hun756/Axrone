using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec3ExtendedTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void RotateX_NinetyDegrees_MapsYToZ()
    {
        Vec3 result = Vec3.RotateX(Vec3.UnitY, MathF.PI / 2f);
        result.X.Should().BeApproximately(0f, Tolerance);
        result.Y.Should().BeApproximately(0f, Tolerance);
        result.Z.Should().BeApproximately(1f, Tolerance);
    }

    [Fact]
    public void RotateY_NinetyDegrees_MapsZToX()
    {
        Vec3 result = Vec3.RotateY(Vec3.UnitZ, MathF.PI / 2f);
        result.X.Should().BeApproximately(1f, Tolerance);
        result.Y.Should().BeApproximately(0f, Tolerance);
        result.Z.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void RotateZ_NinetyDegrees_MapsXToY()
    {
        Vec3 result = Vec3.RotateZ(Vec3.UnitX, MathF.PI / 2f);
        result.X.Should().BeApproximately(0f, Tolerance);
        result.Y.Should().BeApproximately(1f, Tolerance);
        result.Z.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void RotateAxis_AroundZ_MatchesRotateZ()
    {
        var vector = new Vec3(1f, 2f, 3f);
        Vec3 a = Vec3.RotateAxis(vector, Vec3.UnitZ, 0.7f);
        Vec3 b = Vec3.RotateZ(vector, 0.7f);
        a.Equals(b, Tolerance).Should().BeTrue();
    }

    [Fact]
    public void Rotations_PreserveLength()
    {
        var vector = new Vec3(1f, 2f, 3f);
        float length = vector.Length();
        Vec3.RotateX(vector, 1.1f).Length().Should().BeApproximately(length, 1e-4f);
        Vec3.RotateY(vector, 2.2f).Length().Should().BeApproximately(length, 1e-4f);
        Vec3.RotateZ(vector, 3.3f).Length().Should().BeApproximately(length, 1e-4f);
        Vec3.RotateAxis(vector, Vec3.Normalize(new Vec3(1f, 1f, 1f)), 0.5f).Length().Should().BeApproximately(length, 1e-4f);
    }

    [Fact]
    public void Inverse_ReciprocatesComponents()
    {
        Vec3.Inverse(new Vec3(2f, 4f, 0.5f)).Should().Be(new Vec3(0.5f, 0.25f, 2f));
    }

    [Fact]
    public void InverseSafe_FallsBackOnZero()
    {
        Vec3.InverseSafe(new Vec3(2f, 0f, -0f)).Should().Be(new Vec3(0.5f, 0f, 0f));
        Vec3.InverseSafe(new Vec3(0f, 0f, 0f), -1f).Should().Be(new Vec3(-1f, -1f, -1f));
    }

    [Fact]
    public void DivideSafe_HandlesZeroDivisor()
    {
        Vec3.DivideSafe(new Vec3(1f, 2f, 3f), new Vec3(2f, 0f, 4f)).Should().Be(new Vec3(0.5f, 0f, 0.75f));
        Vec3.DivideSafe(new Vec3(1f, 2f, 3f), 0f).Should().Be(Vec3.Zero);
        Vec3.DivideSafe(new Vec3(1f, 2f, 3f), 2f).Should().Be(new Vec3(0.5f, 1f, 1.5f));
    }

    [Fact]
    public void Distances_MatchDefinitions()
    {
        var a = new Vec3(1f, 2f, 3f);
        var b = new Vec3(4f, 0f, 3f);
        Vec3.ManhattanDistance(a, b).Should().Be(5f);
        Vec3.ChebyshevDistance(a, b).Should().Be(3f);
    }

    [Fact]
    public void SmootherStep_EndointsAndMidpoint()
    {
        Vec3.SmootherStep(Vec3.Zero, Vec3.One, 0f).Should().Be(Vec3.Zero);
        Vec3.SmootherStep(Vec3.Zero, Vec3.One, 1f).Should().Be(Vec3.One);
        Vec3 mid = Vec3.SmootherStep(Vec3.Zero, Vec3.One, 0.5f);
        mid.X.Should().BeApproximately(0.5f, Tolerance);
        Vec3.SmootherStep(Vec3.Zero, Vec3.One, -1f).Should().Be(Vec3.Zero);
        Vec3.SmootherStep(Vec3.Zero, Vec3.One, 2f).Should().Be(Vec3.One);
    }

    [Fact]
    public void ScalarAddSub_Works()
    {
        new Vec3(1f, 2f, 3f).AddScalar(10f).Should().Be(new Vec3(11f, 12f, 13f));
        new Vec3(1f, 2f, 3f).SubtractScalar(1f).Should().Be(new Vec3(0f, 1f, 2f));
    }
}
