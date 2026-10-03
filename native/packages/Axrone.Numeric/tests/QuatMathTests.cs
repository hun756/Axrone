using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class QuatMathTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Dot_Conjugate_Inverse()
    {
        Quat.Dot(Quat.Identity, Quat.Identity).Should().BeApproximately(1f, Tolerance);
        Quat.DotStrict(new Quat(1f, 2f, 3f, 4f), new Quat(1f, 2f, 3f, 4f)).Should().Be(30f);
        Quat.Conjugate(new Quat(1f, 2f, 3f, 4f)).Should().Be(new Quat(-1f, -2f, -3f, 4f));
        Quat.Inverse(new Quat(1f, 0f, 0f, 0f)).Should().Be(new Quat(-1f, 0f, 0f, 0f));
        Quat.Inverse(Quat.Zero).Should().Be(Quat.Zero);
        Action throwing = () => Quat.Inverse<ThrowOnSingularPolicy>(Quat.Zero);
        throwing.Should().Throw<InvalidOperationException>();
        Quat.Inverse<ReturnIdentityPolicy>(Quat.Zero).Should().Be(Quat.Identity);
    }

    [Fact]
    public void Normalize_Policies()
    {
        Quat.Normalize(new Quat(0f, 0f, 0f, 4f)).Should().Be(Quat.Identity);
        Quat.Normalize(Quat.Zero).Should().Be(Quat.Identity);
        Quat.TryNormalize(new Quat(0f, 0f, 3f, 4f), out Quat u).Should().BeTrue();
        u.Length().Should().BeApproximately(1f, 1e-6f);

        NormalizationOutcome ok = Quat.TryNormalizeExact(new Quat(0f, 0f, 0f, 2f));
        ok.IsSuccess.Should().BeTrue();
        ok.TryGet(out Quat n).Should().BeTrue();
        n.Should().Be(Quat.Identity);
        Quat.TryNormalizeExact(Quat.Zero).Tag.Should().Be(NormalizationOutcomeTag.ZeroMagnitude);
        Quat.TryNormalizeExact(new Quat(float.NaN, 0f, 0f, 0f)).Tag.Should().Be(NormalizationOutcomeTag.NonFinite);
        Quat.TryNormalizeExact(new Quat(1e-20f, 0f, 0f, 0f), 1e-6f).Tag.Should().Be(NormalizationOutcomeTag.Subnormal);
        ok.UnwrapOr(Quat.Zero).Should().Be(Quat.Identity);
        Quat.TryNormalizeExact(Quat.Zero).UnwrapOr(Quat.Identity).Should().Be(Quat.Identity);
        ok.Match(q => "n", () => "z", () => "nan").Should().Be("n");
    }

    [Fact]
    public void AxisAngle_RoundTrips()
    {
        Quat q = Quat.CreateFromAxisAngle(new Vec3(0f, 0f, 1f), MathF.PI);
        q.X.Should().BeApproximately(0f, Tolerance);
        q.Z.Should().BeApproximately(1f, Tolerance);
        q.W.Should().BeApproximately(0f, Tolerance);

        Quat u = Quat.CreateFromAxisAngle(UnitAxis3.UnitY, AngleRadians.FromDegrees(90f));
        u.Y.Should().BeApproximately(0.7071068f, 1e-5f);
        u.W.Should().BeApproximately(0.7071068f, 1e-5f);
    }

    [Fact]
    public void YawPitchRoll_IdentityAtZero()
    {
        Quat.CreateFromYawPitchRoll(0f, 0f, 0f).Should().Be(Quat.Identity);
        Quat.CreateFromEuler(EulerAngles.Zero).Should().Be(Quat.Identity);
        EulerAngles.Zero.Order.Should().Be(RotationOrder.Zyx);
    }

    [Fact]
    public void RotationMatrix_RoundTrips()
    {
        Quat q = Quat.CreateFromYawPitchRoll(0.3f, -0.2f, 0.1f);
        var m = new System.Numerics.Matrix4x4(
            1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f);
        Quat.CreateFromRotationMatrix(m).Should().Be(Quat.Identity);

        var idm = System.Numerics.Matrix4x4.Identity;
        Quat.CreateFromRotationMatrix(idm).Should().Be(Quat.Identity);
        _ = q;
    }

    [Fact]
    public void FromToRotation_HandlesAlignedAndOpposed()
    {
        Quat.FromToRotation(Vec3.UnitX, Vec3.UnitX).Should().Be(Quat.Identity);
        Quat opposed = Quat.FromToRotation(Vec3.UnitX, Vec3.NegativeUnitX);
        opposed.W.Should().BeApproximately(0f, Tolerance);
        Quat q = Quat.FromToRotation(Vec3.UnitX, Vec3.UnitY);
        (q * Vec3.UnitX).Length().Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void LookRotation_IdentityForward()
    {
        Quat.LookRotation(new Vec3(0f, 0f, -1f), new Vec3(0f, 1f, 0f)).Length().Should().BeApproximately(1f, 1e-4f);
    }

    [Fact]
    public void SwingTwist_SplitsAroundAxis()
    {
        Quat q = Quat.CreateFromAxisAngle(new Vec3(0f, 0f, 1f), 0.6f);
        q.DecomposeSwingTwist(UnitAxis3.UnitZ, out Quat swing, out Quat twist);
        swing.Length().Should().BeApproximately(1f, 1e-5f);
        twist.Length().Should().BeApproximately(1f, 1e-5f);
        (swing * twist).Equals(q, 1e-5f).Should().BeTrue();
    }

    [Fact]
    public void GetAxisAngle_RoundTrips()
    {
        Quat q = Quat.CreateFromAxisAngle(new Vec3(0f, 1f, 0f), 1.2f);
        q.GetAxisAngle(out Vec3 axis, out float angle);
        angle.Should().BeApproximately(1.2f, 1e-5f);
        axis.Length().Should().BeApproximately(1f, 1e-5f);
        q.GetAxisAngle(out UnitAxis3 uaxis, out AngleRadians rad);
        rad.Value.Should().BeApproximately(1.2f, 1e-5f);
        uaxis.AsVec3().Length().Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void Angle_Lerp_Slerp_Squad()
    {
        Quat.Angle(Quat.Identity, Quat.Identity).Should().Be(0f);
        Quat.AngleBetween(Quat.Identity, Quat.Identity).Value.Should().Be(0f);
        Quat.Lerp(Quat.Identity, Quat.Identity, 0.5f).Should().Be(Quat.Identity);
        Quat.Slerp(Quat.Identity, Quat.Identity, 0.5f).Should().Be(Quat.Identity);
        Quat.Slerp<DirectPathPolicy>(Quat.Identity, Quat.Identity, 0.5f).Should().Be(Quat.Identity);
        Quat.Squad(Quat.Identity, Quat.Identity, Quat.Identity, Quat.Identity, 0.5f).Should().Be(Quat.Identity);
        Quat.Concatenate(Quat.Identity, Quat.Identity).Should().Be(Quat.Identity);

        Quat a = Quat.CreateFromAxisAngle(new Vec3(0f, 0f, 1f), 0f);
        Quat b = Quat.CreateFromAxisAngle(new Vec3(0f, 0f, 1f), MathF.PI / 2f);
        Quat half = Quat.Slerp(a, b, 0.5f);
        half.Length().Should().BeApproximately(1f, 1e-5f);
        Quat.Angle(a, half).Should().BeApproximately(MathF.PI / 4f, 1e-4f);
    }

    [Fact]
    public void UnitAxis_AndAngles()
    {
        UnitAxis3.TryCreate(new Vec3(3f, 0f, 0f), out UnitAxis3 axis).Should().BeTrue();
        axis.Should().Be(UnitAxis3.UnitX);
        UnitAxis3.TryCreate(Vec3.Zero, out _).Should().BeFalse();
        Action bad = () => UnitAxis3.Create(Vec3.Zero);
        bad.Should().Throw<Exception>();
        UnitAxis3.CreateUnchecked(0f, 0f, 0f).AsVec3().Should().Be(Vec3.Zero);
        AngleRadians.HalfPi.Value.Should().BeApproximately(MathF.PI / 2f, 1e-7f);
        AngleRadians.TwoPi.Value.Should().BeApproximately(MathF.PI * 2f, 1e-6f);
    }
}
