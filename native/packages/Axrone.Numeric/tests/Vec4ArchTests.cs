using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Vec4ArchTests
{
    [Fact]
    public void Tolerance_FlowsThroughEqualityAndNormalize()
    {
        var a = new Vec4(1f, 2f, 3f, 4f);
        var b = new Vec4(1f, 2f, 3f, 4.0005f);
        a.Equals(b, Tolerance.Default).Should().BeFalse();
        a.Equals(b, new Tolerance(0.001f)).Should().BeTrue();
        Vec4.Equals(a, b, new Tolerance(0.001f)).Should().BeTrue();
        Vec4.TryNormalize(Vec4.Zero, out _, Tolerance.Zero).Should().BeFalse();
        Vec4.TryNormalize(new Vec4(0f, 0f, 0f, 5f), out Vec4 u, new Tolerance(1e-6f)).Should().BeTrue();
        u.Should().Be(Vec4.UnitW);
    }

    [Fact]
    public void AngleBetween_ReturnsRadians()
    {
        Vec4.AngleBetween(Vec4.UnitX, Vec4.UnitY).Value.Should().BeApproximately(MathF.PI / 2f, 1e-5f);
    }

    [Fact]
    public void UnitVec4_RoundTripsAndClassifies()
    {
        UnitVec4.UnitW.AsVec4().Should().Be(Vec4.UnitW);
        (-UnitVec4.UnitX).Should().Be(UnitVec4.NegativeUnitX);
        Vec4 v = UnitVec4.UnitZ;
        v.Should().Be(Vec4.UnitZ);
        UnitVec4 back = (UnitVec4)new Vec4(0f, 0f, 0f, 1f);
        back.Should().Be(UnitVec4.UnitW);

        NormalizationResult4 ok = Vec4.TryNormalizeUnit(new Vec4(1f, 2f, 2f, 4f), 0f);
        ok.IsSuccess.Should().BeTrue();
        ok.TryGetUnit(out UnitVec4 unit).Should().BeTrue();
        ((Vec4)unit).Length().Should().BeApproximately(1f, 1e-5f);

        NormalizationResult4 zero = Vec4.TryNormalizeUnit(Vec4.Zero, Tolerance.Zero);
        zero.Status.Should().Be(NormalizationStatus.DegenerateZero);
        NormalizationResult4 nan = Vec4.TryNormalizeUnit(new Vec4(float.NaN, 0f, 0f, 0f), Tolerance.Zero);
        nan.Status.Should().Be(NormalizationStatus.NonFinite);
        ok.Match(u => "unit", () => "zero", () => "nan").Should().Be("unit");

        Vec4.Reflect(new Vec4(1f, 1f, 1f, 1f), UnitVec4.UnitY).Should().Be(new Vec4(1f, -1f, 1f, 1f));
        Vec4.Project(new Vec4(3f, 4f, 5f, 6f), UnitVec4.UnitX).Should().Be(new Vec4(3f, 0f, 0f, 0f));
    }

    [Fact]
    public void ComponentIndex4_BoundsAndIndexing()
    {
        ComponentIndex4.W.Value.Should().Be(3);
        ComponentIndex4.From(2).Should().Be(ComponentIndex4.Z);
        Action bad = () => ComponentIndex4.From(4);
        bad.Should().Throw<Exception>();
        ComponentIndex4 ci = 3;
        ci.Should().Be(ComponentIndex4.W);

        var v = new Vec4(10f, 20f, 30f, 40f);
        v[ComponentIndex4.W].Should().Be(40f);
        v[ComponentIndex4.X] = 99f;
        v.X.Should().Be(99f);
    }

    [Fact]
    public void Enumerator_AndBatch_Works()
    {
        var v = new Vec4(1f, 2f, 3f, 4f);
        var seen = new System.Collections.Generic.List<float>();
        foreach (float c in v)
            seen.Add(c);
        seen.Should().Equal(1f, 2f, 3f, 4f);
    }

    [Fact]
    public void NewFormatting_RoundTripsAndRejectsTrailing()
    {
        var v = new Vec4(1.5f, -2.25f, 3f, 4.75f);
        v.ToString().Should().Be("1.5, -2.25, 3, 4.75");
        Vec4.Parse(v.ToString(), null).Should().Be(v);
        Vec4.TryParse("<3, 4, 5, 6>", null, out Vec4 parsed).Should().BeTrue();
        parsed.Should().Be(new Vec4(3f, 4f, 5f, 6f));
        Vec4.TryParse("1, 2, 3, 4,", null, out _).Should().BeFalse();
        Vec4.TryParse("1, 2, 3", null, out _).Should().BeFalse();
    }

    [Fact]
    public void NewCopyAndLoad_Overloads()
    {
        var v = new Vec4(1f, 2f, 3f, 4f);
        byte[] bytes = new byte[16];
        v.CopyTo(bytes);
        v.TryCopyTo(bytes).Should().BeTrue();
        var one = new Vec4[1];
        v.CopyTo(one);
        one[0].Should().Be(v);
        v.CopyTo(one, 0);
        float[] arr = [1f, 2f, 3f, 4f, 5f];
        Vec4.Load(arr.AsSpan()).Should().Be(v);
    }
}
