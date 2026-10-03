using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class QuatOpsTests
{
    [Fact]
    public void Operators_Arithmetic()
    {
        (new Quat(1f, 2f, 3f, 4f) + new Quat(4f, 3f, 2f, 1f)).Should().Be(new Quat(5f, 5f, 5f, 5f));
        (new Quat(1f, 1f, 1f, 1f) * 2f).Should().Be(new Quat(2f, 2f, 2f, 2f));
        (2f * new Quat(1f, 1f, 1f, 1f)).Should().Be(new Quat(2f, 2f, 2f, 2f));
        (new Quat(4f, 4f, 4f, 4f) / 2f).Should().Be(new Quat(2f, 2f, 2f, 2f));
        (-Quat.Identity).Should().Be(new Quat(0f, 0f, 0f, -1f));
        (+Quat.Identity).Should().Be(Quat.Identity);
        (new Quat(1f, 2f, 3f, 4f) == new Quat(1f, 2f, 3f, 4f)).Should().BeTrue();
        (new Quat(1f, 2f, 3f, 4f) != Quat.Identity).Should().BeTrue();
        Quat.Add(Quat.Identity, Quat.Identity).Should().Be(new Quat(0f, 0f, 0f, 2f));
        Quat.Negate(Quat.Identity).Should().Be(new Quat(0f, 0f, 0f, -1f));
        Quat.Multiply(Quat.Identity, Quat.Identity).Should().Be(Quat.Identity);
    }

    [Fact]
    public void Multiply_RotatesVectors()
    {
        Quat yaw90 = Quat.CreateFromYawPitchRoll(MathF.PI / 2f, 0f, 0f);
        Vec3 turned = yaw90 * Vec3.UnitX;
        turned.Length().Should().BeApproximately(1f, 1e-5f);
        (Quat.Identity * new Quat(1f, 2f, 3f, 4f)).Should().Be(new Quat(1f, 2f, 3f, 4f));
        (new Quat(1f, 2f, 3f, 4f) / Quat.Identity).Length().Should().BeApproximately(new Quat(1f, 2f, 3f, 4f).Length(), 1e-4f);
    }

    [Fact]
    public void Classifiers_MaskSemantics()
    {
        Quat.CountWhereAllBitsSet(Vec4ToQuatMask(Vec4.GreaterThan(new Vec4(2f, 2f, 2f, 2f), Vec4.One))).Should().Be(4);
        Quat.CountWhereAllBitsSet(Quat.IsNaN(new Quat(float.NaN, 0f, 0f, 0f))).Should().Be(1);
        Quat.CountWhereAllBitsSet(Quat.IsFinite(Quat.Identity)).Should().Be(4);
        Quat.CountWhereAllBitsSet(Quat.IsZero(Quat.Zero)).Should().Be(4);
        Quat.CountWhereAllBitsSet(Quat.IsInfinity(new Quat(float.PositiveInfinity, 0f, 0f, 0f))).Should().Be(1);
    }

    private static Quat Vec4ToQuatMask(Vec4 mask) => new(mask.X, mask.Y, mask.Z, mask.W);

    [Fact]
    public void Equality_Tolerance_Bitwise()
    {
        var a = new Quat(1f, 2f, 3f, 4f);
        a.Equals(new Quat(1f, 2f, 3f, 4f)).Should().BeTrue();
        a.Equals(new Quat(1f, 2f, 3f, 4.0001f), 0.001f).Should().BeTrue();
        a.Equals(new Quat(1f, 2f, 3f, 4.0001f), new Tolerance(0.001f)).Should().BeTrue();
        Quat.Equals(a, new Quat(1f, 2f, 3f, 4f)).Should().BeTrue();
        a.BitEquals(new Quat(1f, 2f, 3f, 4f)).Should().BeTrue();
        a.GetHashCode().Should().Be(new Quat(1f, 2f, 3f, 4f).GetHashCode());
    }

    [Fact]
    public void Parse_RoundTrips()
    {
        var q = new Quat(0.5f, -0.5f, 0.5f, 0.5f);
        q.ToString().Should().Be("0.5, -0.5, 0.5, 0.5");
        Quat.Parse(q.ToString(), null).Should().Be(q);
        Quat.TryParse("<0, 0, 0, 1>", null, out Quat parsed).Should().BeTrue();
        parsed.Should().Be(Quat.Identity);
        Quat.TryParse("nope", null, out _).Should().BeFalse();
        Quat.TryParse("1, 2, 3", null, out _).Should().BeFalse();
        Quat.TryParse("1, 2, 3, 4,", null, out _).Should().BeFalse();
        Action bad = () => Quat.Parse("nope", null);
        bad.Should().Throw<Exception>();
    }
}
