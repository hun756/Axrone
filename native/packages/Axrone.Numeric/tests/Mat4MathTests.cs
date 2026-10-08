using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Mat4MathTests
{
    [Fact]
    public void Operators_AddSubtractScale_MatchScalarPath()
    {
        var a = Mat4.Identity;
        var b = Mat4.Identity * 2f;
        (a + a).Should().Be(b);
        (b - a).Should().Be(a);
        (2f * a).Should().Be(b);
        Mat4.Negate(a).Should().Be(a * -1f);
        (+a).Should().Be(a);
        Mat4.Add(a, a).Should().Be(b);
        Mat4.Subtract(b, a).Should().Be(a);
        Mat4.Multiply(a, 3f).Should().Be(a + a + a);
    }

    [Fact]
    public void Multiply_IdentityAndKnownProduct()
    {
        var m = new Mat4(
            1f, 2f, 3f, 4f,
            5f, 6f, 7f, 8f,
            9f, 10f, 11f, 12f,
            13f, 14f, 15f, 16f);
        (m * Mat4.Identity).Should().Be(m);
        (Mat4.Identity * m).Should().Be(m);
        Mat4.Multiply(m, Mat4.Identity).Should().Be(m);

        var t = Mat4.CreateTranslation(new Vec3(10f, 20f, 30f));
        var p = new Vec4(1f, 2f, 3f, 1f);
        (t * p).Should().Be(new Vec4(11f, 22f, 33f, 1f));
        (p * t).Should().Be(new Vec4(11f, 22f, 33f, 1f));
    }

    [Fact]
    public void Multiply_ByRef_MatchesOperator_BitExact()
    {
        var cases = new (Mat4 Left, Mat4 Right)[]
        {
            (Mat4.Identity, Mat4.Identity),
            (Mat4.Zero, Mat4.Identity),
            (Mat4.CreateScale(new Vec3(2f, 3f, 4f)), Mat4.CreateTranslation(new Vec3(5f, 6f, 7f))),
            (
                new Mat4(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f, 16f),
                new Mat4(16f, 15f, 14f, 13f, 12f, 11f, 10f, 9f, 8f, 7f, 6f, 5f, 4f, 3f, 2f, 1f)
            ),
        };

        foreach (var (left, right) in cases)
        {
            Mat4.Multiply(in left, in right).Should().Be(left * right);
        }
    }

    [Fact]
    public void Multiply_OutParameter_MatchesOperator_BitExact()
    {
        var cases = new (Mat4 Left, Mat4 Right)[]
        {
            (Mat4.Identity, Mat4.Identity),
            (Mat4.Zero, Mat4.Identity),
            (Mat4.CreateScale(new Vec3(2f, 3f, 4f)), Mat4.CreateTranslation(new Vec3(5f, 6f, 7f))),
            (
                new Mat4(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f, 16f),
                new Mat4(16f, 15f, 14f, 13f, 12f, 11f, 10f, 9f, 8f, 7f, 6f, 5f, 4f, 3f, 2f, 1f)
            ),
        };

        foreach (var (left, right) in cases)
        {
            Mat4.Multiply(in left, in right, out Mat4 product);
            product.Should().Be(left * right);
        }
    }

    [Fact]
    public void Transpose_IsInvolution_AndMatchesScalarPath()
    {
        var m = new Mat4(
            1f, 2f, 3f, 4f,
            5f, 6f, 7f, 8f,
            9f, 10f, 11f, 12f,
            13f, 14f, 15f, 16f);
        var t = m.Transpose();
        t.Row0.Should().Be(new Vec4(1f, 5f, 9f, 13f));
        t.Row3.Should().Be(new Vec4(4f, 8f, 12f, 16f));
        t.Transpose().Should().Be(m);
        Mat4.Transpose(m).Should().Be(t);
        Mat4.Identity.Transpose().Should().Be(Mat4.Identity);
    }

    [Fact]
    public void Determinant_ScaleAndSingular()
    {
        Mat4.Identity.Determinant().Should().Be(1f);
        Mat4.CreateScale(new Vec3(2f, 3f, 4f)).Determinant().Should().BeApproximately(24f, 1e-4f);
        Mat4.Zero.Determinant().Should().Be(0f);
    }

    [Fact]
    public void Invert_RoundTrips_AndRejectsSingular()
    {
        var m = Mat4.CreateScale(new Vec3(2f, 3f, 4f)) * Mat4.CreateTranslation(new Vec3(5f, 6f, 7f));
        m.Invert(out Mat4 inv).Should().BeTrue();
        (m * inv).Equals(Mat4.Identity, 1e-4f).Should().BeTrue();
        Mat4.Invert(m, out Mat4 inv2).Should().BeTrue();
        inv2.Should().Be(inv);
        Mat4.Zero.Invert(out Mat4 bad).Should().BeFalse();
        bad.Should().Be(Mat4.Zero);
    }

    [Fact]
    public void Factories_TranslationScaleRotations()
    {
        Mat4.CreateTranslation(1f, 2f, 3f).Translation.Should().Be(new Vec3(1f, 2f, 3f));
        var s = Mat4.CreateScale(2f, 3f, 4f);
        (s * new Vec4(1f, 1f, 1f, 1f)).Should().Be(new Vec4(2f, 3f, 4f, 1f));

        var rx = Mat4.CreateRotationX(AngleRadians.HalfPi);
        (rx * new Vec4(0f, 1f, 0f, 1f)).Equals(new Vec4(0f, 0f, 1f, 1f), 1e-5f).Should().BeTrue();
        var ry = Mat4.CreateRotationY(AngleRadians.HalfPi);
        (ry * new Vec4(0f, 0f, -1f, 1f)).Equals(new Vec4(-1f, 0f, 0f, 1f), 1e-5f).Should().BeTrue();
        var rz = Mat4.CreateRotationZ(AngleRadians.HalfPi);
        (rz * new Vec4(1f, 0f, 0f, 1f)).Equals(new Vec4(0f, 1f, 0f, 1f), 1e-5f).Should().BeTrue();

        var aa = Mat4.CreateFromAxisAngle(new Vec3(0f, 0f, 1f), MathF.PI * 0.5f);
        aa.Equals(rz, 1e-5f).Should().BeTrue();
        var ypr = Mat4.CreateFromYawPitchRoll(AngleRadians.Zero, AngleRadians.Zero, AngleRadians.Zero);
        ypr.Equals(Mat4.Identity, 1e-5f).Should().BeTrue();
    }

    [Fact]
    public void Projections_GuardAndSanity()
    {
        var persp = Mat4.CreatePerspectiveFieldOfView<RightHandedZeroToOne>(
            AngleRadians.FromDegrees(60f), 16f / 9f, 0.1f, 100f);
        persp.M11.Should().BeGreaterThan(0f);
        persp.M34.Should().Be(-1f);

        var gl = Mat4.CreatePerspectiveFieldOfView<RightHandedNegOneToOne>(
            AngleRadians.FromDegrees(60f), 16f / 9f, 0.1f, 100f);
        gl.M34.Should().Be(-1f);

        var lh = Mat4.CreatePerspectiveFieldOfView<LeftHandedZeroToOne>(
            AngleRadians.FromDegrees(60f), 16f / 9f, 0.1f, 100f);
        lh.M34.Should().Be(1f);

        var ortho = Mat4.CreateOrthographic(16f, 9f, 0f, 1f);
        ortho.M11.Should().BeApproximately(2f / 16f, 1e-6f);
        ortho.M44.Should().Be(1f);

        Action badFov = () => Mat4.CreatePerspectiveFieldOfView<RightHandedZeroToOne>(AngleRadians.Zero, 1f, 0.1f, 10f);
        badFov.Should().Throw<ArgumentOutOfRangeException>();
        Action badNear = () => Mat4.CreatePerspectiveFieldOfView<RightHandedZeroToOne>(AngleRadians.FromDegrees(60f), 1f, 0f, 10f);
        badNear.Should().Throw<ArgumentOutOfRangeException>();
        Action badFar = () => Mat4.CreatePerspectiveFieldOfView<RightHandedZeroToOne>(AngleRadians.FromDegrees(60f), 1f, 10f, 1f);
        badFar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LookAt_AndWorld_FrameCamera()
    {
        var view = Mat4.CreateLookAt(new Vec3(0f, 0f, 5f), new Vec3(0f, 0f, 0f), new Vec3(0f, 1f, 0f));
        (view * new Vec4(0f, 0f, 0f, 1f)).Equals(new Vec4(0f, 0f, -5f, 1f), 1e-5f).Should().BeTrue();

        var world = Mat4.CreateWorld(new Vec3(1f, 2f, 3f), new Vec3(0f, 0f, -1f), new Vec3(0f, 1f, 0f));
        world.Translation.Should().Be(new Vec3(1f, 2f, 3f));
        world.Column2.Should().Be(new Vec4(0f, 0f, 1f, 3f));
    }

    [Fact]
    public void Decompose_RoundTripsAffine_AndFlagsDegenerate()
    {
        var m = Mat4.CreateScale(new Vec3(2f, 3f, 4f))
            * Mat4.CreateFromQuaternion(Quat.CreateFromYawPitchRoll(0.3f, -0.2f, 0.1f))
            * Mat4.CreateTranslation(new Vec3(4f, 5f, 6f));
        MatrixDecompositionResult r = m.Decompose();
        r.IsSuccess.Should().BeTrue();
        r.Translation.Equals(new Vec3(4f, 5f, 6f), 1e-4f).Should().BeTrue();
        r.Scale.Equals(new Vec3(2f, 3f, 4f), 1e-4f).Should().BeTrue();
        r.Match(
            (s, q, t) => (s, q, t),
            _ => (Vec3.Zero, Quat.Identity, Vec3.Zero)).Should().Be((r.Scale, r.Rotation, r.Translation));

        m.Decompose(out Vec3 s2, out _, out Vec3 t2).Should().BeTrue();
        s2.Equals(new Vec3(2f, 3f, 4f), 1e-4f).Should().BeTrue();
        t2.Equals(new Vec3(4f, 5f, 6f), 1e-4f).Should().BeTrue();

        var flat = Mat4.CreateScale(new Vec3(0f, 1f, 1f));
        MatrixDecompositionResult bad = flat.Decompose();
        bad.IsSuccess.Should().BeFalse();
        bad.Status.Should().Be(DecompositionStatus.DegenerateScale);
    }

    [Fact]
    public void Lerp_HitsEndpointsAndMidpoint()
    {
        Mat4.Lerp(Mat4.Zero, Mat4.Identity, 0f).Should().Be(Mat4.Zero);
        Mat4.Lerp(Mat4.Zero, Mat4.Identity, 1f).Should().Be(Mat4.Identity);
        Mat4.Lerp(Mat4.Zero, Mat4.Identity, 0.5f).Should().Be(Mat4.Identity * 0.5f);
    }

    [Fact]
    public void Pipeline_EnforcesScaleRotateTranslateOrder()
    {
        var built = TransformPipeline<EmptyTransform>.Begin()
            .Scale(new Vec3(2f, 2f, 2f))
            .Rotate(Quat.Identity)
            .Translate(new Vec3(1f, 2f, 3f))
            .Build();
        built.Translation.Should().Be(new Vec3(1f, 2f, 3f));
        (built * new Vec4(1f, 0f, 0f, 1f)).Should().Be(new Vec4(3f, 2f, 3f, 1f));

        var scaleOnly = TransformPipeline<EmptyTransform>.Begin().Scale(3f).Build();
        scaleOnly.Should().Be(Mat4.CreateScale(3f));
        var rotateOnly = TransformPipeline<EmptyTransform>.Begin().Rotate(Quat.Identity).Build();
        rotateOnly.Equals(Mat4.Identity, 1e-6f).Should().BeTrue();
    }
}
