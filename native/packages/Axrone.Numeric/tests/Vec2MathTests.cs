using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec2MathTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void DotCross_Length_AreCorrect()
    {
        Vec2.Dot(new Vec2(1f, 2f), new Vec2(3f, 4f)).Should().Be(11f);
        Vec2.Cross(Vec2.UnitX, Vec2.UnitY).Should().Be(1f);
        Vec2.Cross(Vec2.UnitY, Vec2.UnitX).Should().Be(-1f);
        new Vec2(3f, 4f).Length().Should().BeApproximately(5f, Tolerance);
        Vec2.Distance(Vec2.Zero, new Vec2(6f, 8f)).Should().BeApproximately(10f, Tolerance);
    }

    [Fact]
    public void Normalize_UnitAndDegenerate()
    {
        Vec2.Normalize(new Vec2(3f, 4f)).Length().Should().BeApproximately(1f, 1e-6f);
        Vec2.Normalize(Vec2.Zero).Should().Be(Vec2.Zero);
        Vec2.TryNormalize(Vec2.Zero, out _).Should().BeFalse();
        Vec2.TryNormalize(new Vec2(3f, 4f), out Vec2 unit).Should().BeTrue();
        unit.Length().Should().BeApproximately(1f, 1e-6f);
    }

    [Fact]
    public void Reflect_Project_Slide_Identities()
    {
        Vec2.Reflect(new Vec2(1f, -1f), Vec2.UnitY).Should().Be(new Vec2(1f, 1f));
        Vec2.Project(new Vec2(3f, 4f), Vec2.UnitX).Should().Be(new Vec2(3f, 0f));
        Vec2.Slide(new Vec2(3f, 4f), Vec2.UnitY).Should().Be(new Vec2(3f, 0f));
    }

    [Fact]
    public void Perpendicular_Rotate_Angle()
    {
        var v = new Vec2(1f, 0f);
        v.Perpendicular().Should().Be(new Vec2(0f, 1f));
        v.PerpendicularClockwise().Should().Be(new Vec2(0f, -1f));
        Vec2.Rotate(Vec2.UnitX, MathF.PI / 2f).Y.Should().BeApproximately(1f, Tolerance);
        Vec2.RotateAround(new Vec2(2f, 0f), new Vec2(1f, 0f), MathF.PI).X.Should().BeApproximately(0f, Tolerance);
        Vec2.Angle(Vec2.UnitX, Vec2.UnitY).Should().BeApproximately(MathF.PI / 2f, Tolerance);
        Vec2.SignedAngle(Vec2.UnitX, Vec2.UnitY).Should().BeApproximately(MathF.PI / 2f, Tolerance);
        Vec2.SignedAngle(Vec2.UnitY, Vec2.UnitX).Should().BeApproximately(-MathF.PI / 2f, Tolerance);
    }

    [Fact]
    public void Clamp_Lerp_Smooth_MoveTowards()
    {
        Vec2.Clamp(new Vec2(-1f, 5f), Vec2.Zero, Vec2.One).Should().Be(new Vec2(0f, 1f));
        Vec2.Lerp(Vec2.Zero, Vec2.One, 0.5f).Should().Be(new Vec2(0.5f, 0.5f));
        Vec2.LerpClamped(Vec2.Zero, Vec2.One, 2f).Should().Be(Vec2.One);
        Vec2.SmoothStep(Vec2.Zero, Vec2.One, 0.5f).X.Should().BeApproximately(0.5f, Tolerance);
        Vec2.SmootherStep(Vec2.Zero, Vec2.One, 0.5f).X.Should().BeApproximately(0.5f, Tolerance);
        Vec2.MoveTowards(Vec2.Zero, new Vec2(10f, 0f), 3f).Should().Be(new Vec2(3f, 0f));
        Vec2.MoveTowards(Vec2.Zero, new Vec2(1f, 0f), 5f).Should().Be(new Vec2(1f, 0f));
    }

    [Fact]
    public void MinMax_Fma_Abs()
    {
        Vec2.Min(new Vec2(1f, 5f), new Vec2(2f, 3f)).Should().Be(new Vec2(1f, 3f));
        Vec2.Max(new Vec2(1f, 5f), new Vec2(2f, 3f)).Should().Be(new Vec2(2f, 5f));
        Vec2.FusedMultiplyAdd(new Vec2(2f, 3f), new Vec2(4f, 5f), new Vec2(1f, 1f)).Should().Be(new Vec2(9f, 16f));
        Vec2.Abs(new Vec2(-1f, 2f)).Should().Be(new Vec2(1f, 2f));
    }

    [Fact]
    public void InverseSafe_DivideSafe_Distances_Scalars()
    {
        Vec2.Inverse(new Vec2(2f, 4f)).Should().Be(new Vec2(0.5f, 0.25f));
        Vec2.InverseSafe(new Vec2(2f, 0f)).Should().Be(new Vec2(0.5f, 0f));
        Vec2.DivideSafe(new Vec2(1f, 2f), new Vec2(2f, 0f)).Should().Be(new Vec2(0.5f, 0f));
        Vec2.DivideSafe(new Vec2(1f, 2f), 0f).Should().Be(Vec2.Zero);
        Vec2.ManhattanDistance(new Vec2(1f, 1f), new Vec2(4f, 5f)).Should().Be(7f);
        Vec2.ChebyshevDistance(new Vec2(1f, 1f), new Vec2(4f, 5f)).Should().Be(4f);
        new Vec2(1f, 2f).AddScalar(10f).Should().Be(new Vec2(11f, 12f));
        new Vec2(1f, 2f).SubtractScalar(1f).Should().Be(new Vec2(0f, 1f));
    }

    [Fact]
    public void Transform_MatrixAndQuaternion()
    {
        var m = System.Numerics.Matrix3x2.CreateTranslation(5f, 6f);
        Vec2.Transform(Vec2.Zero, m).Should().Be(new Vec2(5f, 6f));
        Vec2.TransformNormal(Vec2.UnitX, System.Numerics.Matrix3x2.Identity).Should().Be(Vec2.UnitX);
        Vec2.Transform(Vec2.UnitX, System.Numerics.Quaternion.Identity).Should().Be(Vec2.UnitX);
    }

    [Fact]
    public void Trig_Exp_Log_Rounding()
    {
        Vec2.Sin(Vec2.Zero).Should().Be(Vec2.Zero);
        Vec2.Cos(Vec2.Zero).Should().Be(Vec2.One);
        Vec2.Floor(new Vec2(1.7f, -1.7f)).Should().Be(new Vec2(1f, -2f));
        Vec2.Ceiling(new Vec2(1.2f, -1.2f)).Should().Be(new Vec2(2f, -1f));
        var (sin, cos) = Vec2.SinCos(new Vec2(0f, 0f));
        sin.Should().Be(Vec2.Zero);
        cos.Should().Be(Vec2.One);
    }

    [Fact]
    public void SumAll_Average_Aggregates()
    {
        Vec2.SumAll(new Vec2(1f, 2f), new Vec2(3f, 4f)).Should().Be(new Vec2(4f, 6f));
        Vec2.Average(new Vec2(1f, 2f), new Vec2(3f, 4f)).Should().Be(new Vec2(2f, 3f));
        Action empty = () => Vec2.Average();
        empty.Should().Throw<Exception>();
    }
}
