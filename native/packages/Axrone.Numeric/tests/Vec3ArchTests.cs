using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec3ArchTests
{
    [Fact]
    public void SpaceContracts_AreSatisfied()
    {
        static float Len<T>(T v) where T : struct, ISpatialVector<T> => v.Length();
        static float DotOf<T>(T a, T b) where T : struct, IInnerProductSpace<T, float> => T.Dot(a, b);
        static Vec3 CrossOf<T>(T a, T b) where T : struct, ICrossProductSpace<T> => (Vec3)(object)T.Cross(a, b)!;
        static Vec3 SlerpOf<T>(T a, T b, float t) where T : struct, IInterpolatableSpace<T, float> => (Vec3)(object)T.Slerp(a, b, t)!;

        var v = new Vec3(3f, 4f, 0f);
        Len(v).Should().BeApproximately(5f, 1e-5f);
        v.LengthSquared().Should().Be(25f);
        DotOf(v, v).Should().Be(25f);
        CrossOf(v, Vec3.UnitZ).Should().Be(new Vec3(4f, -3f, 0f));
        Vec3.Lerp(Vec3.Zero, Vec3.One, 0.5f).Should().Be(new Vec3(0.5f, 0.5f, 0.5f));
        SlerpOf(Vec3.UnitX, Vec3.UnitY, 0.5f).Length().Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void HardwareStrategy_AgreesWithStrict()
    {
        var v = new Vec3(3f, -4f, 12f);
        Vec3.Normalize<HardwareIntrinsicsStrategy>(v).Length().Should().BeApproximately(1f, 1e-5f);
        Vec3.Normalize<HardwareIntrinsicsStrategy>(Vec3.Zero).Should().Be(Vec3.Zero);
    }

    [Fact]
    public void ComponentIndex_BoundsAndConversions()
    {
        ComponentIndex.X.Value.Should().Be(0);
        ComponentIndex.Z.Value.Should().Be(2);
        ComponentIndex.From(1).Should().Be(ComponentIndex.Y);
        Action bad = () => ComponentIndex.From(3);
        bad.Should().Throw<Exception>();
        Action neg = () => ComponentIndex.From(-1);
        neg.Should().Throw<Exception>();
        ComponentIndex ci = 2;
        ci.Should().Be(ComponentIndex.Z);
        int back = ComponentIndex.Y;
        back.Should().Be(1);

        var v = new Vec3(10f, 20f, 30f);
        v[ComponentIndex.Y].Should().Be(20f);
        v[ComponentIndex.Z] = 99f;
        v.Z.Should().Be(99f);
        (ComponentIndex.X < ComponentIndex.Y).Should().BeTrue();
    }

    [Fact]
    public void Tolerance_ValidatesAndConverts()
    {
        Tolerance.Default.Value.Should().BeApproximately(Vec3.DefaultTolerance, 1e-12f);
        Tolerance.Zero.Value.Should().Be(0f);
        Tolerance t = 0.01f;
        t.Value.Should().Be(0.01f);
        float back = t;
        back.Should().Be(0.01f);
        (Tolerance.Zero < Tolerance.Default).Should().BeTrue();
        Action neg = () => _ = new Tolerance(-1f);
        neg.Should().Throw<Exception>();
        Action nan = () => _ = new Tolerance(float.NaN);
        nan.Should().Throw<Exception>();
    }

    [Fact]
    public void AngleRadians_ConvertsAndCompares()
    {
        AngleRadians.FromDegrees(180f).Value.Should().BeApproximately(MathF.PI, 1e-6f);
        new AngleRadians(1f).ToDegrees().Should().BeApproximately(57.29578f, 1e-4f);
        (AngleRadians.FromDegrees(10f) + AngleRadians.FromDegrees(20f)).Value.Should().BeApproximately(AngleRadians.FromDegrees(30f).Value, 1e-6f);
        (-AngleRadians.FromDegrees(10f)).Value.Should().BeApproximately(AngleRadians.FromDegrees(-10f).Value, 1e-6f);
        (AngleRadians.FromDegrees(1f) < AngleRadians.FromDegrees(2f)).Should().BeTrue();
        float back = AngleRadians.FromDegrees(90f);
        back.Should().BeApproximately(MathF.PI / 2f, 1e-6f);
    }

    [Fact]
    public void UnitVec3_RoundTrips()
    {
        UnitVec3.UnitX.AsVec3().Should().Be(Vec3.UnitX);
        (-UnitVec3.UnitY).Should().Be(UnitVec3.NegativeUnitY);
        Vec3 v = UnitVec3.UnitZ;
        v.Should().Be(Vec3.UnitZ);
        UnitVec3 back = (UnitVec3)new Vec3(1f, 0f, 0f);
        back.Should().Be(UnitVec3.UnitX);
        UnitVec3.UnitX.ToString().Should().Be(Vec3.UnitX.ToString());
    }

    [Fact]
    public void TryNormalizeUnit_Classifies()
    {
        NormalizationResult ok = Vec3.TryNormalizeUnit(new Vec3(3f, 4f, 0f), 0f);
        ok.IsSuccess.Should().BeTrue();
        ok.TryGetUnit(out UnitVec3 unit).Should().BeTrue();
        ((Vec3)unit).Length().Should().BeApproximately(1f, 1e-5f);

        NormalizationResult zero = Vec3.TryNormalizeUnit(Vec3.Zero, Tolerance.Zero);
        zero.Status.Should().Be(NormalizationStatus.DegenerateZero);
        zero.TryGetUnit(out _).Should().BeFalse();

        NormalizationResult nan = Vec3.TryNormalizeUnit(new Vec3(float.NaN, 0f, 0f), Tolerance.Zero);
        nan.Status.Should().Be(NormalizationStatus.NonFinite);

        ok.Match(u => "unit", () => "zero", () => "nan").Should().Be("unit");
        zero.Match(u => "unit", () => "zero", () => "nan").Should().Be("zero");
        nan.Match(u => "unit", () => "zero", () => "nan").Should().Be("nan");

        (ok == new NormalizationResult(unit)).Should().BeTrue();
        (zero != nan).Should().BeTrue();
    }

    [Fact]
    public void UnitOverloads_AgreeWithPlain()
    {
        var v = new Vec3(1f, 2f, 3f);
        var n = new UnitVec3(0f, 1f, 0f);
        Vec3.Reflect(v, n).Should().Be(Vec3.Reflect(v, new Vec3(0f, 1f, 0f)));
        Vec3.Project(v, n).Should().Be(Vec3.Project(v, new Vec3(0f, 1f, 0f)));
        Vec3.ProjectOnPlane(v, n).Should().Be(Vec3.ProjectOnPlane(v, new Vec3(0f, 1f, 0f)));
        Vec3.AngleBetween(Vec3.UnitX, Vec3.UnitY).Value.Should().BeApproximately(MathF.PI / 2f, 1e-5f);
        Vec3.SignedAngleBetween(Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ).Value.Should().BeApproximately(MathF.PI / 2f, 1e-5f);
    }

    [Fact]
    public void UnitPaths_MatchPlainPaths()
    {
        var v = new Vec3(1f, 2f, 3f);
        var n = Vec3.ToUnit(new Vec3(0f, 1f, 0f));
        Vec3.Slide(v, n).Should().Be(Vec3.Slide(v, new Vec3(0f, 1f, 0f)));
        Vec3.Angle(UnitVec3.UnitX, UnitVec3.UnitY).Should().BeApproximately(
            Vec3.Angle(Vec3.UnitX, Vec3.UnitY), 1e-6f);
        Vec3 u = Vec3.Slerp(UnitVec3.UnitX, UnitVec3.UnitY, 0.5f);
        u.Length().Should().BeApproximately(1f, 1e-5f);
        u.Should().Be(Vec3.Slerp(Vec3.UnitX, Vec3.UnitY, 0.5f));
        Vec3.Slerp(UnitVec3.UnitX, UnitVec3.NegativeUnitX, 0.5f).Length().Should().BeApproximately(1f, 1e-4f);
        Vec3.Slerp(UnitVec3.UnitX, UnitVec3.UnitX, 0.3f).Should().Be(Vec3.UnitX);
    }

    [Fact]
    public void Tolerance_FlowsThroughEqualityAndNormalize()
    {
        var a = new Vec3(1f, 2f, 3f);
        var b = new Vec3(1f, 2f, 3.0005f);
        a.Equals(b, Tolerance.Default).Should().BeFalse();
        a.Equals(b, new Tolerance(0.001f)).Should().BeTrue();
        Vec3.Equals(a, b, new Tolerance(0.001f)).Should().BeTrue();
        Vec3.TryNormalize(Vec3.Zero, out _, Tolerance.Zero).Should().BeFalse();
        Vec3.TryNormalize(new Vec3(1e-9f, 0f, 0f), out Vec3 tiny, Tolerance.Zero).Should().BeTrue();
        tiny.Should().Be(Vec3.UnitX);
        Vec3.TryNormalize(new Vec3(3f, 4f, 0f), out Vec3 u, new Tolerance(1e-6f)).Should().BeTrue();
        u.Length().Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void AngleRadians_FlowsThroughRotations()
    {
        var v = Vec3.UnitX;
        Vec3.RotateZ(v, AngleRadians.FromDegrees(90f)).Y.Should().BeApproximately(1f, 1e-5f);
        v.RotateX(AngleRadians.FromDegrees(0f)).Should().Be(v);
        Vec3.RotateAxis(Vec3.UnitX, Vec3.UnitZ, AngleRadians.FromDegrees(90f)).Y.Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void FormattingOptions_ShapeOutput()
    {
        var opts = new Vec3FormattingOptions { Separator = "|", Prefix = "[", Suffix = "]" };
        new Vec3(1f, 2f, 3f).ToString(opts).Should().Be("[1|2|3]");
        new Vec3(1f, 2f, 3f).ToString(new Vec3FormattingOptions()).Should().Be("1, 2, 3");
    }

    [Fact]
    public void SpaceContracts_CoverAllThreeTypes()
    {
        static float Len<T>(T v) where T : struct, ISpatialVector<T> => v.Length();
        static float DotOf<T>(T a, T b) where T : struct, IInnerProductSpace<T, float> => T.Dot(a, b);
        Len(new Vec2(3f, 4f)).Should().BeApproximately(5f, 1e-5f);
        Len(new Vec4(1f, 2f, 2f, 4f)).Should().BeApproximately(5f, 1e-5f);
        DotOf(new Vec2(1f, 2f), new Vec2(3f, 4f)).Should().Be(11f);
        DotOf(new Vec4(1f, 1f, 1f, 1f), new Vec4(1f, 1f, 1f, 1f)).Should().Be(4f);
        Vec2.Normalize(new Vec2(3f, 4f)).Length().Should().BeApproximately(1f, 1e-6f);
        Vec4.Normalize(new Vec4(0f, 0f, 0f, 5f)).Should().Be(Vec4.UnitW);
    }

    [Fact]
    public void Enumerator_YieldsComponents()
    {
        var v = new Vec3(1f, 2f, 3f);
        var seen = new System.Collections.Generic.List<float>();
        foreach (float c in v)
            seen.Add(c);
        seen.Should().Equal(1f, 2f, 3f);
    }

    [Fact]
    public void FormattingOptions_Defaults()
    {
        var opts = new Vec3FormattingOptions();
        opts.Separator.Should().Be(", ");
        opts.Prefix.Should().Be("");
        opts.Suffix.Should().Be("");
        opts.Separator = "; ";
        opts.Separator.Should().Be("; ");
        Action nullSep = () => opts.Separator = null!;
        nullSep.Should().Throw<Exception>();
    }
}
