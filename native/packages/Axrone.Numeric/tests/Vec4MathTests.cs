using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec4MathTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Dot_Length_Distance()
    {
        Vec4.Dot(new Vec4(1f, 2f, 3f, 4f), new Vec4(5f, 6f, 7f, 8f)).Should().Be(70f);
        Vec4.DotStrict(new Vec4(1f, 1f, 1f, 1f), new Vec4(1f, 1f, 1f, 1f)).Should().Be(4f);
        new Vec4(1f, 2f, 2f, 4f).Length().Should().BeApproximately(5f, Tolerance);
        Vec4.Distance(Vec4.Zero, new Vec4(1f, 2f, 2f, 4f)).Should().BeApproximately(5f, Tolerance);
    }

    [Fact]
    public void Normalize_UnitAndDegenerate()
    {
        Vec4.Normalize(new Vec4(1f, 2f, 2f, 4f)).Length().Should().BeApproximately(1f, 1e-6f);
        Vec4.Normalize(Vec4.Zero).Should().Be(Vec4.Zero);
        Vec4.TryNormalize(Vec4.Zero, out _).Should().BeFalse();
        Vec4.TryNormalize(new Vec4(0f, 0f, 0f, 5f), out Vec4 unit).Should().BeTrue();
        unit.Should().Be(Vec4.UnitW);
    }

    [Fact]
    public void Reflect_Project_Slide()
    {
        Vec4.Reflect(new Vec4(1f, -1f, 0f, 0f), Vec4.UnitY).Should().Be(new Vec4(1f, 1f, 0f, 0f));
        Vec4.Project(new Vec4(3f, 4f, 5f, 6f), Vec4.UnitX).Should().Be(new Vec4(3f, 0f, 0f, 0f));
        Vec4.Slide(new Vec4(3f, 4f, 5f, 6f), Vec4.UnitY).Should().Be(new Vec4(3f, 0f, 5f, 6f));
    }

    [Fact]
    public void Angle_Clamp_Lerp()
    {
        Vec4.Angle(Vec4.UnitX, Vec4.UnitY).Should().BeApproximately(MathF.PI / 2f, Tolerance);
        Vec4.Angle(Vec4.Zero, Vec4.UnitY).Should().Be(0f);
        Vec4.Clamp(new Vec4(-1f, 5f, 0.5f, 2f), Vec4.Zero, Vec4.One).Should().Be(new Vec4(0f, 1f, 0.5f, 1f));
        Vec4.Lerp(Vec4.Zero, Vec4.One, 0.25f).Should().Be(new Vec4(0.25f, 0.25f, 0.25f, 0.25f));
        Vec4.LerpClamped(Vec4.Zero, Vec4.One, 2f).Should().Be(Vec4.One);
        Vec4.SmootherStep(Vec4.Zero, Vec4.One, 0.5f).X.Should().BeApproximately(0.5f, Tolerance);
        Vec4.MoveTowards(Vec4.Zero, new Vec4(10f, 0f, 0f, 0f), 3f).Should().Be(new Vec4(3f, 0f, 0f, 0f));
    }

    [Fact]
    public void MinMax_Fma_SinCos()
    {
        Vec4.Min(new Vec4(1f, 5f, 0f, 9f), new Vec4(2f, 3f, 4f, 1f)).Should().Be(new Vec4(1f, 3f, 0f, 1f));
        Vec4.FusedMultiplyAdd(new Vec4(2f, 2f, 2f, 2f), new Vec4(3f, 3f, 3f, 3f), Vec4.One).Should().Be(new Vec4(7f, 7f, 7f, 7f));
        var (sin, cos) = Vec4.SinCos(Vec4.Zero);
        sin.Should().Be(Vec4.Zero);
        cos.Should().Be(Vec4.One);
        Vec4.Floor(new Vec4(1.7f, -1.7f, 0.5f, 2.5f)).Should().Be(new Vec4(1f, -2f, 0f, 2f));
    }

    [Fact]
    public void InverseSafe_DivideSafe_Distances()
    {
        Vec4.Inverse(new Vec4(2f, 4f, 0.5f, 1f)).Should().Be(new Vec4(0.5f, 0.25f, 2f, 1f));
        Vec4.InverseSafe(new Vec4(2f, 0f, 1f, 1f)).Should().Be(new Vec4(0.5f, 0f, 1f, 1f));
        Vec4.DivideSafe(new Vec4(1f, 2f, 3f, 4f), new Vec4(2f, 0f, 4f, 4f)).Should().Be(new Vec4(0.5f, 0f, 0.75f, 1f));
        Vec4.ManhattanDistance(Vec4.Zero, Vec4.One).Should().Be(4f);
        Vec4.ChebyshevDistance(new Vec4(1f, 1f, 1f, 1f), new Vec4(4f, 2f, 1f, 9f)).Should().Be(8f);
        new Vec4(1f, 2f, 3f, 4f).AddScalar(10f).Should().Be(new Vec4(11f, 12f, 13f, 14f));
    }

    [Fact]
    public void Transform_MatrixAndQuaternion()
    {
        var m = System.Numerics.Matrix4x4.CreateTranslation(5f, 6f, 7f);
        Vec4.Transform(new Vec3(1f, 2f, 3f), m).Should().Be(new Vec4(6f, 8f, 10f, 1f));
        Vec4.Transform(new Vec2(1f, 2f), m).X.Should().Be(6f);
        Vec4.Transform(Vec4.UnitX, System.Numerics.Quaternion.Identity).Should().Be(Vec4.UnitX);
        Vec4.Transform(new Vec3(1f, 0f, 0f), System.Numerics.Quaternion.Identity).Should().Be(new Vec4(1f, 0f, 0f, 1f));
    }

    [Fact]
    public void SumAll_Average_Aggregates()
    {
        Vec4.SumAll(new Vec4(1f, 1f, 1f, 1f), new Vec4(2f, 2f, 2f, 2f)).Should().Be(new Vec4(3f, 3f, 3f, 3f));
        Vec4.Average(new Vec4(0f, 0f, 0f, 0f), new Vec4(2f, 4f, 6f, 8f)).Should().Be(new Vec4(1f, 2f, 3f, 4f));
        Action empty = () => Vec4.Average();
        empty.Should().Throw<Exception>();
    }
}
