using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec2ArchTests
{
    [Fact]
    public void Tolerance_FlowsThroughEqualityAndNormalize()
    {
        var a = new Vec2(1f, 2f);
        var b = new Vec2(1f, 2.0005f);
        a.Equals(b, Tolerance.Default).Should().BeFalse();
        a.Equals(b, new Tolerance(0.001f)).Should().BeTrue();
        Vec2.Equals(a, b, new Tolerance(0.001f)).Should().BeTrue();
        Vec2.TryNormalize(Vec2.Zero, out _, Tolerance.Zero).Should().BeFalse();
        Vec2.TryNormalize(new Vec2(3f, 4f), out Vec2 u, new Tolerance(1e-6f)).Should().BeTrue();
        u.Length().Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void AngleRadians_FlowsThroughRotations()
    {
        Vec2.Rotate(Vec2.UnitX, AngleRadians.FromDegrees(90f)).Y.Should().BeApproximately(1f, 1e-5f);
        Vec2.UnitX.Rotate(AngleRadians.FromDegrees(0f)).Should().Be(Vec2.UnitX);
        Vec2.RotateAround(new Vec2(2f, 0f), new Vec2(1f, 0f), AngleRadians.FromDegrees(180f)).X.Should().BeApproximately(0f, 1e-5f);
        Vec2.AngleBetween(Vec2.UnitX, Vec2.UnitY).Value.Should().BeApproximately(MathF.PI / 2f, 1e-5f);
        Vec2.SignedAngleBetween(Vec2.UnitX, Vec2.UnitY).Value.Should().BeApproximately(MathF.PI / 2f, 1e-5f);
    }

    [Fact]
    public void UnitVec2_RoundTripsAndClassifies()
    {
        UnitVec2.UnitX.AsVec2().Should().Be(Vec2.UnitX);
        (-UnitVec2.UnitY).Should().Be(UnitVec2.NegativeUnitY);
        Vec2 v = UnitVec2.UnitY;
        v.Should().Be(Vec2.UnitY);
        UnitVec2 back = (UnitVec2)new Vec2(0f, 1f);
        back.Should().Be(UnitVec2.UnitY);

        NormalizationResult2D ok = Vec2.TryNormalizeUnit(new Vec2(3f, 4f), 0f);
        ok.IsSuccess.Should().BeTrue();
        ok.TryGetUnit(out UnitVec2 unit).Should().BeTrue();
        ((Vec2)unit).Length().Should().BeApproximately(1f, 1e-5f);

        NormalizationResult2D zero = Vec2.TryNormalizeUnit(Vec2.Zero, Tolerance.Zero);
        zero.Status.Should().Be(NormalizationStatus.DegenerateZero);
        NormalizationResult2D nan = Vec2.TryNormalizeUnit(new Vec2(float.NaN, 0f), Tolerance.Zero);
        nan.Status.Should().Be(NormalizationStatus.NonFinite);
        ok.Match(u => "unit", () => "zero", () => "nan").Should().Be("unit");

        Vec2.Reflect(new Vec2(1f, 1f), UnitVec2.UnitY).Should().Be(new Vec2(1f, -1f));
        Vec2.Project(new Vec2(3f, 4f), UnitVec2.UnitX).Should().Be(new Vec2(3f, 0f));
        Vec2.ProjectOnLine(new Vec2(3f, 4f), UnitVec2.UnitY).Should().Be(new Vec2(3f, 0f));
    }

    [Fact]
    public void ComponentIndex2D_BoundsAndIndexing()
    {
        ComponentIndex2D.X.Value.Should().Be(0);
        ComponentIndex2D.From(1).Should().Be(ComponentIndex2D.Y);
        Action bad = () => ComponentIndex2D.From(2);
        bad.Should().Throw<Exception>();
        ComponentIndex2D ci = 1;
        ci.Should().Be(ComponentIndex2D.Y);

        var v = new Vec2(10f, 20f);
        v[ComponentIndex2D.Y].Should().Be(20f);
        v[ComponentIndex2D.X] = 99f;
        v.X.Should().Be(99f);
    }

    [Fact]
    public void Enumerator_AndBatch_Works()
    {
        var v = new Vec2(1f, 2f);
        var seen = new System.Collections.Generic.List<float>();
        foreach (float c in v)
            seen.Add(c);
        seen.Should().Equal(1f, 2f);
    }

    [Fact]
    public void NewFormatting_RoundTripsAndRejectsTrailing()
    {
        var v = new Vec2(1.5f, -2.25f);
        v.ToString().Should().Be("1.5, -2.25");
        Vec2.Parse(v.ToString(), null).Should().Be(v);
        Vec2.TryParse("<3, 4>", null, out Vec2 parsed).Should().BeTrue();
        parsed.Should().Be(new Vec2(3f, 4f));
        Vec2.TryParse("1, 2,", null, out _).Should().BeFalse();
        Vec2.TryParse("1, 2, 3", null, out _).Should().BeFalse();
    }

    [Fact]
    public void NewCopyAndLoad_Overloads()
    {
        var v = new Vec2(1f, 2f);
        byte[] bytes = new byte[8];
        v.CopyTo(bytes);
        bytes.Length.Should().Be(8);
        v.TryCopyTo(bytes).Should().BeTrue();
        var one = new Vec2[1];
        v.CopyTo(one);
        one[0].Should().Be(v);
        v.CopyTo(one, 0);
        float[] arr = [1f, 2f, 3f];
        Vec2.Load(arr.AsSpan()).Should().Be(new Vec2(1f, 2f));
    }
}
