namespace Axrone.Numeric.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class Vec3MathTests
{
    private const float Tolerance = 1e-5f;
    private const float LooseTolerance = 1e-3f;

    [Fact]
    public void Dot_SumsComponentProducts()
    {
        Vec3 left = new(1.0f, 2.0f, 3.0f);
        Vec3 right = new(4.0f, -5.0f, 6.0f);

        Vec3.Dot(left, right).Should().Be(((1.0f * 4.0f) + (2.0f * -5.0f)) + (3.0f * 6.0f));
        Vec3.DotStrict(left, right).Should().Be(((1.0f * 4.0f) + (2.0f * -5.0f)) + (3.0f * 6.0f));
        Vec3.Dot(Vec3.UnitX, Vec3.UnitY).Should().Be(0.0f);
        Vec3.Dot(Vec3.Zero, Vec3.One).Should().Be(0.0f);
    }

    [Fact]
    public void Dot_IsFullyFmaContracted_AndDotStrictIsNot()
    {
        Xorshift32 rng = new(0x9E3779B9u);
        int contractedMatches = 0;
        int differsFromStrict = 0;
        int strictMatchesPlainExpression = 0;
        const int Samples = 2048;

        for (int i = 0; i < Samples; i++)
        {
            Vec3 a = rng.NextVector();
            Vec3 b = rng.NextVector();

            float contracted = MathF.FusedMultiplyAdd(a.X, b.X, MathF.FusedMultiplyAdd(a.Y, b.Y, a.Z * b.Z));
            float plain = ((a.X * b.X) + (a.Y * b.Y)) + (a.Z * b.Z);

            if (SameBits(Vec3.Dot(a, b), contracted))
            {
                contractedMatches++;
            }

            if (!SameBits(Vec3.Dot(a, b), plain))
            {
                differsFromStrict++;
            }

            if (SameBits(Vec3.DotStrict(a, b), plain))
            {
                strictMatchesPlainExpression++;
            }
        }

        contractedMatches.Should().Be(Samples, "Dot must fold every term through a fused multiply-add");
        strictMatchesPlainExpression.Should().Be(Samples, "DotStrict must never contract");
        differsFromStrict.Should().BeGreaterThan(0, "contraction is only observable if it changes bits");
    }

    [Fact]
    public void Cross_FollowsRightHandedConvention()
    {
        Vec3.Cross(Vec3.UnitX, Vec3.UnitY).Should().Be(Vec3.UnitZ);
        Vec3.Cross(Vec3.UnitY, Vec3.UnitZ).Should().Be(Vec3.UnitX);
        Vec3.Cross(Vec3.UnitZ, Vec3.UnitX).Should().Be(Vec3.UnitY);
    }

    [Fact]
    public void Cross_IsAntiCommutativeAndPerpendicularToBothInputs()
    {
        Vec3 a = new(1.0f, 2.0f, 3.0f);
        Vec3 b = new(-4.0f, 0.5f, 2.0f);

        Vec3 forward = Vec3.Cross(a, b);
        Vec3 backward = Vec3.Cross(b, a);

        backward.X.Should().BeApproximately(-forward.X, Tolerance);
        backward.Y.Should().BeApproximately(-forward.Y, Tolerance);
        backward.Z.Should().BeApproximately(-forward.Z, Tolerance);

        Vec3.Dot(forward, a).Should().BeApproximately(0.0f, LooseTolerance);
        Vec3.Dot(forward, b).Should().BeApproximately(0.0f, LooseTolerance);

        Vec3.Cross(a, a).Should().Be(Vec3.Zero);
        Vec3.Cross(a, a * 2.0f).Should().Be(Vec3.Zero);
    }

    [Fact]
    public void LengthAndLengthSquared_MatchPythagoras()
    {
        Vec3 v = new(3.0f, 4.0f, 0.0f);

        v.LengthSquared().Should().Be(25.0f);
        v.Length().Should().Be(5.0f);
        Vec3.Zero.Length().Should().Be(0.0f);
        Vec3.One.Length().Should().BeApproximately(MathF.Sqrt(3.0f), Tolerance);
    }

    [Fact]
    public void DistanceSquaredAndDistance_MatchComponentDeltas()
    {
        Vec3 a = new(1.0f, 2.0f, 3.0f);
        Vec3 b = new(4.0f, 6.0f, 3.0f);

        Vec3.DistanceSquared(a, b).Should().Be(25.0f);
        Vec3.Distance(a, b).Should().Be(5.0f);
        Vec3.Distance(a, a).Should().Be(0.0f);
        Vec3.DistanceSquared(a, a).Should().Be(0.0f);

        (Vec3.DistanceSquared(a, b) * Vec3.Distance(a, b)).Should().BeApproximately(125.0f, LooseTolerance);
    }

    [Fact]
    public void Normalize_YieldsUnitLengthAndPreservesDirection()
    {
        Vec3 source = new(3.0f, 4.0f, 12.0f);
        Vec3 unit = Vec3.Normalize(source);

        unit.Length().Should().BeApproximately(1.0f, Tolerance);
        unit.X.Should().BeApproximately(3.0f / 13.0f, Tolerance);
        unit.Y.Should().BeApproximately(4.0f / 13.0f, Tolerance);
        unit.Z.Should().BeApproximately(12.0f / 13.0f, Tolerance);
    }

    [Fact]
    public void Normalize_OfZeroVector_ReturnsZeroInsteadOfNaN()
    {
        Vec3 unit = Vec3.Normalize(Vec3.Zero);

        unit.Should().Be(Vec3.Zero);
        unit.IsAllFinite.Should().BeTrue();
    }

    [Fact]
    public void Normalize_OfVerySmallVector_StaysUnitLength_AndOfUnderflowedLengthReturnsZero()
    {
        Vec3 denormal = new(1e-20f, 0.0f, 0.0f);

        Vec3 unit = Vec3.Normalize(denormal);

        unit.Length().Should().BeApproximately(1.0f, Tolerance);
        unit.X.Should().BeApproximately(1.0f, Tolerance);

        Vec3.Normalize(new Vec3(1e-30f, 0.0f, 0.0f)).Should().Be(Vec3.Zero);
    }

    [Fact]
    public void Normalize_AgreesWithBothCoreStrategiesWithinTolerance()
    {
        Xorshift32 rng = new(0xC0FFEE11u);

        for (int i = 0; i < 512; i++)
        {
            Vec3 value = rng.NextVector() * 100.0f;
            Vec3 strict = Vec3.Normalize<StrictIeeeStrategy>(value);
            Vec3 fast = Vec3.Normalize<FastApproximationStrategy>(value);
            Vec3 total = Vec3.Normalize(value);

            fast.X.Should().BeApproximately(strict.X, 1e-5f);
            fast.Y.Should().BeApproximately(strict.Y, 1e-5f);
            fast.Z.Should().BeApproximately(strict.Z, 1e-5f);

            total.X.Should().BeApproximately(strict.X, Tolerance);
            total.Y.Should().BeApproximately(strict.Y, Tolerance);
            total.Z.Should().BeApproximately(strict.Z, Tolerance);
        }
    }

    [Fact]
    public void TryNormalize_SucceedsAndAgreesWithNormalize()
    {
        Vec3 source = new(-2.0f, 0.0f, 5.0f);

        bool succeeded = Vec3.TryNormalize(source, out Vec3 result);

        succeeded.Should().BeTrue();
        result.Length().Should().BeApproximately(1.0f, Tolerance);
        result.X.Should().BeApproximately(Vec3.Normalize(source).X, Tolerance);
    }

    [Fact]
    public void TryNormalize_RejectsZeroAndWritesZero()
    {
        bool succeeded = Vec3.TryNormalize(Vec3.Zero, out Vec3 result);

        succeeded.Should().BeFalse();
        result.Should().Be(Vec3.Zero);
    }

    [Fact]
    public void TryNormalize_RejectsVectorsAtOrBelowTolerance()
    {
        Vec3 small = new(1e-6f, 0.0f, 0.0f);

        Vec3.TryNormalize(small, out Vec3 strictResult, 1e-4f).Should().BeFalse();
        strictResult.Should().Be(Vec3.Zero);

        Vec3.TryNormalize(new Vec3(1.0f, 0.0f, 0.0f), out Vec3 okResult, 1e-4f).Should().BeTrue();
        okResult.Length().Should().BeApproximately(1.0f, Tolerance);

        Vec3.TryNormalize(new Vec3(1.0f, 0.0f, 0.0f), out Vec3 negativeTolerance, -1.0f).Should().BeTrue();
        negativeTolerance.X.Should().BeApproximately(1.0f, Tolerance);
    }

    [Fact]
    public void Reflect_ReversesOnlyTheNormalComponent()
    {
        Vec3 incident = new(1.0f, -1.0f, 0.5f);
        Vec3 normal = Vec3.UnitY;

        Vec3 reflected = Vec3.Reflect(incident, normal);

        reflected.X.Should().BeApproximately(incident.X, Tolerance);
        reflected.Y.Should().BeApproximately(-incident.Y, Tolerance);
        reflected.Z.Should().BeApproximately(incident.Z, Tolerance);
    }

    [Fact]
    public void Reflect_OfPerpendicularVector_ReturnsItUnchanged()
    {
        Vec3 incident = new(2.0f, 0.0f, -3.0f);

        Vec3 reflected = Vec3.Reflect(incident, Vec3.UnitY);

        reflected.X.Should().BeApproximately(incident.X, Tolerance);
        reflected.Y.Should().BeApproximately(0.0f, Tolerance);
        reflected.Z.Should().BeApproximately(incident.Z, Tolerance);
    }

    [Fact]
    public void Project_IsParallelToNormal_AndZeroForDegenerateNormal()
    {
        Vec3 vector = new(3.0f, 4.0f, 0.0f);
        Vec3 normal = new(0.0f, 0.0f, 7.0f);

        Vec3 projected = Vec3.Project(vector, normal);

        Vec3.Cross(projected, normal).Length().Should().BeApproximately(0.0f, LooseTolerance);
        projected.Z.Should().BeApproximately(0.0f, Tolerance);

        Vec3.Project(vector, Vec3.Zero).Should().Be(Vec3.Zero);
        Vec3.ProjectOnPlane(vector, Vec3.Zero).Should().Be(vector);
    }

    [Fact]
    public void ProjectOnPlane_PlusProject_ReconstructsTheInput()
    {
        Vec3 vector = new(1.0f, 2.0f, -3.0f);
        Vec3 normal = new(0.0f, 2.0f, 0.0f);

        Vec3 inPlane = Vec3.ProjectOnPlane(vector, normal);
        Vec3 alongNormal = Vec3.Project(vector, normal);

        (inPlane.X + alongNormal.X).Should().BeApproximately(vector.X, LooseTolerance);
        (inPlane.Y + alongNormal.Y).Should().BeApproximately(vector.Y, LooseTolerance);
        (inPlane.Z + alongNormal.Z).Should().BeApproximately(vector.Z, LooseTolerance);

        Vec3.Dot(inPlane, normal).Should().BeApproximately(0.0f, LooseTolerance);
    }

    [Fact]
    public void Slide_MatchesProjectOnPlane_AndIsPerpendicularToTheNormal()
    {
        Vec3 vector = new(1.0f, 1.0f, 0.0f);
        Vec3 normal = new(0.0f, 2.0f, 0.0f);

        Vec3 slid = Vec3.Slide(vector, normal);
        Vec3 projected = Vec3.ProjectOnPlane(vector, normal);

        slid.X.Should().BeApproximately(projected.X, Tolerance);
        slid.Y.Should().BeApproximately(projected.Y, Tolerance);
        slid.Z.Should().BeApproximately(projected.Z, Tolerance);
        Vec3.Dot(slid, normal).Should().BeApproximately(0.0f, LooseTolerance);

        Vec3.Slide(vector, Vec3.Zero).Should().Be(vector);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1e5f)]
    [InlineData(1e10f)]
    [InlineData(1e19f)]
    public void Angle_OfIdenticalVectors_IsZeroAtAnyMagnitude(float scale)
    {
        Vec3 value = new(scale, scale * 0.5f, 0.0f);

        float angle = Vec3.Angle(value, value);

        angle.Should().BeApproximately(0.0f, 1e-5f, "both operands are pre-scaled by their largest component");
    }

    [Fact]
    public void Angle_OfOppositeVectors_IsPi()
    {
        Vec3 value = new(3.0f, 0.0f, 0.0f);

        Vec3.Angle(value, -value).Should().BeApproximately(MathF.PI, 1e-4f);
    }

    [Fact]
    public void Angle_OfOrthogonalVectors_IsHalfPi()
    {
        Vec3.Angle(Vec3.UnitX, Vec3.UnitY).Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
        Vec3.Angle(Vec3.UnitY, Vec3.UnitZ).Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
        Vec3.Angle(Vec3.UnitZ, Vec3.UnitX).Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
    }

    [Fact]
    public void Angle_IsSymmetric_AndZeroForDegenerateInput()
    {
        Vec3 a = new(1.0f, 2.0f, 3.0f);
        Vec3 b = new(-2.0f, 0.5f, 1.0f);

        Vec3.Angle(a, b).Should().BeApproximately(Vec3.Angle(b, a), Tolerance);
        Vec3.Angle(a, Vec3.Zero).Should().Be(0.0f);
        Vec3.Angle(Vec3.Zero, a).Should().Be(0.0f);
        Vec3.Angle(Vec3.Zero, Vec3.Zero).Should().Be(0.0f);
    }

    [Fact]
    public void SignedAngle_FollowsTheRightHandRuleAroundTheAxis()
    {
        Vec3 from = Vec3.UnitX;
        Vec3 to = Vec3.UnitY;
        Vec3 axis = Vec3.UnitZ;

        float positive = Vec3.SignedAngle(from, to, axis);
        float negative = Vec3.SignedAngle(to, from, axis);

        positive.Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
        negative.Should().BeApproximately(-MathF.PI / 2.0f, Tolerance);

        Vec3.SignedAngle(from, to, -axis).Should().BeApproximately(-positive, Tolerance);
        Vec3.SignedAngle(from, to, axis * 10.0f).Should().BeApproximately(positive, Tolerance);
    }

    [Fact]
    public void Clamp_BoundsEachComponentIndependently()
    {
        Vec3 value = new(5.0f, -5.0f, 0.5f);
        Vec3 min = new(-1.0f, -1.0f, -1.0f);
        Vec3 max = new(1.0f, 1.0f, 1.0f);

        Vec3 clamped = Vec3.Clamp(value, min, max);

        clamped.X.Should().Be(1.0f);
        clamped.Y.Should().Be(-1.0f);
        clamped.Z.Should().Be(0.5f);
    }

    [Fact]
    public void Clamp_WithInvertedBounds_ResolvesToTheUpperBoundWithoutThrowing()
    {
        Vec3 value = new(5.0f, 5.0f, 5.0f);
        Vec3 min = new(10.0f, 10.0f, 10.0f);
        Vec3 max = new(1.0f, 1.0f, 1.0f);

        Vec3 clamped = Vec3.Clamp(value, min, max);

        clamped.Should().Be(max);
        clamped.IsAllFinite.Should().BeTrue();
    }

    [Fact]
    public void ClampNative_MatchesClamp()
    {
        Vec3 value = new(3.0f, -3.0f, 0.0f);
        Vec3 min = new(-2.0f, -2.0f, -2.0f);
        Vec3 max = new(2.0f, 2.0f, 2.0f);

        Vec3.ClampNative(value, min, max).Should().Be(Vec3.Clamp(value, min, max));
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(1.0f)]
    [InlineData(5.0f)]
    [InlineData(50.0f)]
    public void ClampLength_KeepsTheResultInsideTheBounds(float scale)
    {
        Vec3 value = new(scale, 0.0f, 0.0f);

        Vec3 clamped = Vec3.ClampLength(value, 1.0f, 10.0f);

        clamped.Length().Should().BeApproximately(Math.Clamp(scale, 1.0f, 10.0f), LooseTolerance);
    }

    [Fact]
    public void ClampLength_OfZeroVector_UsesThePositiveXAxisToHonourMinLength()
    {
        Vec3 clamped = Vec3.ClampLength(Vec3.Zero, 5.0f, 10.0f);

        clamped.X.Should().Be(5.0f);
        clamped.Length().Should().BeApproximately(5.0f, Tolerance);

        Vec3.ClampLength(Vec3.Zero, 0.0f, 10.0f).Should().Be(Vec3.Zero);
        Vec3.ClampLength(Vec3.Zero, -1.0f, 10.0f).Should().Be(Vec3.Zero);
    }

    [Fact]
    public void Lerp_ReturnsEndpointsAtZeroAndOne_AndIsFmaContracted()
    {
        Vec3 a = new(1.0f, 2.0f, 3.0f);
        Vec3 b = new(4.0f, 6.0f, 8.0f);

        Vec3.Lerp(a, b, 0.0f).Should().Be(a);
        Vec3.Lerp(a, b, 1.0f).Should().Be(b);

        Vec3.Lerp(a, b, 0.5f).X.Should().BeApproximately(2.5f, Tolerance);
        Vec3.Lerp(a, b, 0.5f).Y.Should().BeApproximately(4.0f, Tolerance);
        Vec3.Lerp(a, b, 0.5f).Z.Should().BeApproximately(5.5f, Tolerance);

        Vec3.Lerp(a, b, 0.25f).X.Should().Be(MathF.FusedMultiplyAdd(b.X - a.X, 0.25f, a.X));
    }

    [Fact]
    public void LerpClamped_ClampsTheFactorToTheSegment()
    {
        Vec3 a = new(0.0f, 0.0f, 0.0f);
        Vec3 b = new(10.0f, 10.0f, 10.0f);

        Vec3.LerpClamped(a, b, -1.0f).Should().Be(a);
        Vec3.LerpClamped(a, b, 2.0f).Should().Be(b);
        Vec3.LerpClamped(a, b, 0.25f).X.Should().BeApproximately(2.5f, Tolerance);
    }

    [Fact]
    public void SmoothStep_MatchesTheHermiteCurve_AndClampsTheAmount()
    {
        Vec3 from = new(0.0f, 0.0f, 0.0f);
        Vec3 to = new(8.0f, 8.0f, 8.0f);

        Vec3.SmoothStep(from, to, 0.0f).Should().Be(from);
        Vec3.SmoothStep(from, to, 1.0f).Should().Be(to);
        Vec3.SmoothStep(from, to, -1.0f).Should().Be(from);
        Vec3.SmoothStep(from, to, 2.0f).Should().Be(to);

        Vec3.SmoothStep(from, to, 0.25f).X.Should().BeApproximately(1.25f, Tolerance);
        Vec3.SmoothStep(from, to, 0.75f).X.Should().BeApproximately(6.75f, Tolerance);
    }

    [Theory]
    [InlineData(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f)]
    [InlineData(1.0f, 2.0f, 3.0f, -2.0f, 1.0f, 4.0f)]
    [InlineData(1.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f)]
    public void Slerp_EndpointsReproduceTheInputs(float ax, float ay, float az, float bx, float by, float bz)
    {
        Vec3 a = new(ax, ay, az);
        Vec3 b = new(bx, by, bz);

        Vec3 atStart = Vec3.Slerp(a, b, 0.0f);
        Vec3 atEnd = Vec3.Slerp(a, b, 1.0f);

        atStart.X.Should().BeApproximately(a.X, LooseTolerance);
        atStart.Y.Should().BeApproximately(a.Y, LooseTolerance);
        atStart.Z.Should().BeApproximately(a.Z, LooseTolerance);
        atEnd.X.Should().BeApproximately(b.X, LooseTolerance);
        atEnd.Y.Should().BeApproximately(b.Y, LooseTolerance);
        atEnd.Z.Should().BeApproximately(b.Z, LooseTolerance);
    }

    [Fact]
    public void Slerp_PreservesLengthForEqualLengthInputs()
    {
        Vec3 a = new(2.0f, 0.0f, 0.0f);
        Vec3 b = new(0.0f, 2.0f, 0.0f);

        for (int step = 0; step <= 10; step++)
        {
            Vec3 slerped = Vec3.Slerp(a, b, step / 10.0f);

            slerped.Length().Should().BeApproximately(2.0f, 1e-3f, $"at t = {step / 10.0f}");
        }
    }

    [Fact]
    public void Slerp_OfAntipodalInputs_StaysFiniteAndOnTheSphere()
    {
        Vec3 a = Vec3.UnitX * 3.0f;
        Vec3 b = -a;

        for (int step = 0; step <= 4; step++)
        {
            Vec3 slerped = Vec3.Slerp(a, b, step / 4.0f);

            slerped.IsAllFinite.Should().BeTrue($"the antipodal branch must not divide by sin(pi) at t = {step / 4.0f}");
            slerped.Length().Should().BeApproximately(3.0f, LooseTolerance);
        }
    }

    [Fact]
    public void Slerp_OfDegenerateInput_FallsBackToLerp()
    {
        Vec3 a = new(0.0f, 0.0f, 0.0f);
        Vec3 b = new(4.0f, 0.0f, 0.0f);

        Vec3 slerped = Vec3.Slerp(a, b, 0.5f);

        slerped.X.Should().BeApproximately(2.0f, Tolerance);
        slerped.Should().Be(Vec3.Lerp(a, b, 0.5f));

        Vec3.Slerp(a, b, 0.0f).Should().Be(a);
        Vec3.Slerp(a, b, 1.0f).Should().Be(b);
    }

    [Fact]
    public void GetOrthogonal_ReturnsAUnitVectorPerpendicularToTheInput()
    {
        Xorshift32 rng = new(0x5EED1234u);

        for (int i = 0; i < 128; i++)
        {
            Vec3 value = rng.NextVector();
            Vec3 orthogonal = Vec3.GetOrthogonal(value);

            orthogonal.Length().Should().BeApproximately(1.0f, 1e-4f);
            MathF.Abs(Vec3.Dot(value, orthogonal)).Should().BeLessThan(1e-3f);
        }
    }

    [Fact]
    public void GetOrthogonal_OfZeroVector_ReturnsTheUnitXAxis()
    {
        Vec3 orthogonal = Vec3.GetOrthogonal(Vec3.Zero);

        orthogonal.Should().Be(Vec3.UnitX);
        orthogonal.Length().Should().BeApproximately(1.0f, Tolerance);
    }

    [Fact]
    public void MoveTowards_StepsByAtMostTheDelta()
    {
        Vec3 current = new(0.0f, 0.0f, 0.0f);
        Vec3 target = new(10.0f, 0.0f, 0.0f);

        Vec3 moved = Vec3.MoveTowards(current, target, 3.0f);

        moved.X.Should().BeApproximately(3.0f, Tolerance);
        moved.Should().Be(Vec3.MoveTowards(current, target, 3.0f));
    }

    [Fact]
    public void MoveTowards_DoesNotOvershootTheTarget()
    {
        Vec3 current = new(0.0f, 0.0f, 0.0f);
        Vec3 target = new(10.0f, 0.0f, 0.0f);

        Vec3.MoveTowards(current, target, 100.0f).Should().Be(target);
        Vec3.MoveTowards(current, target, 10.0f).Should().Be(target);
        Vec3.MoveTowards(current, current, 5.0f).Should().Be(current);
    }

    [Fact]
    public void MoveTowards_ClampsTheDiagonalStep()
    {
        Vec3 current = new(1.0f, 1.0f, 1.0f);
        Vec3 target = new(4.0f, 5.0f, 1.0f);

        Vec3 moved = Vec3.MoveTowards(current, target, 1.0f);

        Vec3.Distance(current, moved).Should().BeApproximately(1.0f, Tolerance);
        Vec3.Distance(moved, target).Should().BeApproximately(4.0f, LooseTolerance);
    }

    [Fact]
    public void MinAndMax_SelectPerComponent_AndPropagateNaN()
    {
        Vec3 left = new(1.0f, 5.0f, -2.0f);
        Vec3 right = new(4.0f, 2.0f, -7.0f);

        Vec3.Min(left, right).Should().Be(new Vec3(1.0f, 2.0f, -7.0f));
        Vec3.Max(left, right).Should().Be(new Vec3(4.0f, 5.0f, -2.0f));

        Vec3.MinNative(left, right).Should().Be(Vec3.Min(left, right));
        Vec3.MaxNative(left, right).Should().Be(Vec3.Max(left, right));

        Vec3 withNaN = new(float.NaN, float.NaN, float.NaN);
        Vec3.Min(withNaN, left).IsAnyNaN.Should().BeTrue();
        Vec3.Max(withNaN, left).IsAnyNaN.Should().BeTrue();
    }

    [Fact]
    public void MinNumberAndMaxNumber_TreatNaNAsMissingData()
    {
        Vec3 left = new(float.NaN, 5.0f, -1.0f);
        Vec3 right = new(3.0f, float.NaN, 7.0f);

        Vec3 minimum = Vec3.MinNumber(left, right);
        Vec3 maximum = Vec3.MaxNumber(left, right);

        minimum.X.Should().Be(3.0f);
        minimum.Y.Should().Be(5.0f);
        minimum.Z.Should().Be(-1.0f);
        maximum.X.Should().Be(3.0f);
        maximum.Y.Should().Be(5.0f);
        maximum.Z.Should().Be(7.0f);

        Vec3.MinNumber(Vec3.NaN, Vec3.NaN).IsAnyNaN.Should().BeTrue();
    }

    [Fact]
    public void MinMagnitudeAndMaxMagnitude_SelectByMagnitudeAndPropagateNaN()
    {
        Vec3 left = new(5.0f, -3.0f, 0.0f);
        Vec3 right = new(-7.0f, 8.0f, 0.0f);

        Vec3.MinMagnitude(left, right).Should().Be(left);
        Vec3.MaxMagnitude(left, right).Should().Be(right);

        Vec3 withNaN = Vec3.NaN;
        Vec3.MinMagnitude(withNaN, left).IsAnyNaN.Should().BeTrue();
        Vec3.MaxMagnitude(withNaN, left).IsAnyNaN.Should().BeTrue();
    }

    [Fact]
    public void MagnitudeNumberVariants_TreatNaNAsMissingData_AndBreakZeroTies()
    {
        Vec3 left = new(float.NaN, -5.0f, 0.0f);
        Vec3 right = new(3.0f, 3.0f, -0.0f);

        Vec3.MinMagnitudeNumber(left, right).X.Should().Be(3.0f);
        Vec3.MinMagnitudeNumber(left, right).Y.Should().Be(3.0f);
        Vec3.MaxMagnitudeNumber(left, right).X.Should().Be(3.0f);
        Vec3.MaxMagnitudeNumber(left, right).Y.Should().Be(-5.0f);

        Vec3 positiveZero = new(0.0f, 0.0f, 0.0f);
        Vec3 negativeZero = new(-0.0f, -0.0f, -0.0f);

        BitConverter.SingleToUInt32Bits(Vec3.MinMagnitudeNumber(positiveZero, negativeZero).X)
            .Should().Be(0x80000000U, "a magnitude tie resolves to negative zero");
        BitConverter.SingleToUInt32Bits(Vec3.MaxMagnitudeNumber(positiveZero, negativeZero).X)
            .Should().Be(0x00000000U, "a magnitude tie resolves to positive zero");
    }

    [Fact]
    public void FusedMultiplyAdd_RoundsOnceAndDiffersFromTheTwoStepExpression()
    {
        Vec3 left = new(9.342449f, 0.7648649f, 5807700.0f);
        Vec3 right = new(9.11887f, 5.398081f, 7.885076E-06f);
        Vec3 addend = new(3.4515495f, 6.5211143f, 556.85046f);

        Vec3 fused = Vec3.FusedMultiplyAdd(left, right, addend);

        fused.X.Should().Be(MathF.FusedMultiplyAdd(left.X, right.X, addend.X));
        fused.Y.Should().Be(MathF.FusedMultiplyAdd(left.Y, right.Y, addend.Y));
        fused.Z.Should().Be(MathF.FusedMultiplyAdd(left.Z, right.Z, addend.Z));

        SameBits(fused.X, (left.X * right.X) + addend.X).Should().BeFalse("the first component only survives a single rounding");
        SameBits(fused.Y, (left.Y * right.Y) + addend.Y).Should().BeFalse();
        SameBits(fused.Z, (left.Z * right.Z) + addend.Z).Should().BeFalse();

        Vec3.MultiplyAddEstimate(left, right, addend).Should().Be(fused);
    }

    [Fact]
    public void FusedMultiplyAdd_IsExactWhenTheTwoStepExpressionIsNot()
    {
        const float Left = 3.0f;
        const float Right = 1.0000001f;
        const float Addend = -3.0000002f;

        float oracle = (float)(((double)Left * (double)Right) + (double)Addend);
        float naive = (Left * Right) + Addend;
        float exact = MathF.BitIncrement(1.0f) - 1.0f;

        oracle.Should().Be(exact, "2^-23 is the exactly rounded result");
        naive.Should().Be(2.0f * exact, "the two-step expression rounds the product up first");

        Vec3 fused = Vec3.FusedMultiplyAdd(new Vec3(Left), new Vec3(Right), new Vec3(Addend));

        fused.X.Should().Be(oracle);
        fused.Y.Should().Be(oracle);
        fused.Z.Should().Be(oracle);
        fused.X.Should().NotBe(naive);
    }

    [Fact]
    public void SumAll_AddsComponents_AndIsZeroForAnEmptyInput()
    {
        Vec3 a = new(1.0f, 2.0f, 3.0f);
        Vec3 b = new(4.0f, 5.0f, 6.0f);
        Vec3 c = new(7.0f, 8.0f, 9.0f);

        Vec3.SumAll(a, b, c).Should().Be(new Vec3(12.0f, 15.0f, 18.0f));
        Vec3.SumAll(a).Should().Be(a);
        Vec3.SumAll(ReadOnlySpan<Vec3>.Empty).Should().Be(Vec3.Zero);

        Vec3.Sum(a).Should().Be(6.0f);
        Vec3.Sum(Vec3.Zero).Should().Be(0.0f);
    }

    [Fact]
    public void Average_DividesTheSumByTheCount()
    {
        Vec3 a = new(1.0f, 2.0f, 3.0f);
        Vec3 b = new(4.0f, 5.0f, 6.0f);
        Vec3 c = new(7.0f, 8.0f, 9.0f);

        Vec3.Average(a, b, c).Should().Be(new Vec3(4.0f, 5.0f, 6.0f));
        Vec3.Average(a).Should().Be(a);
    }

    [Fact]
    public void Average_OfAnEmptyInput_Throws()
    {
        Action act = () => Vec3.Average(ReadOnlySpan<Vec3>.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllAnyNoneAndCount_AgreeOnNonZeroComponents()
    {
        Vec3 none = Vec3.Zero;
        Vec3 all = Vec3.One;
        Vec3 mixed = new(1.0f, 0.0f, -3.0f);

        Vec3.All(none).Should().BeFalse();
        Vec3.All(all).Should().BeTrue();
        Vec3.All(mixed).Should().BeFalse();

        Vec3.Any(none).Should().BeFalse();
        Vec3.Any(all).Should().BeTrue();
        Vec3.Any(mixed).Should().BeTrue();

        Vec3.None(none).Should().BeTrue();
        Vec3.None(all).Should().BeFalse();
        Vec3.None(mixed).Should().BeFalse();

        Vec3.Count(none).Should().Be(0);
        Vec3.Count(all).Should().Be(3);
        Vec3.Count(mixed).Should().Be(2);
    }

    [Fact]
    public void WhereAllBitsSetFamily_CountsMaskedComponents()
    {
        Vec3 all = Vec3.AllBitsSet;
        Vec3 two = new(Vec3.AllBitsSet.X, Vec3.AllBitsSet.Y, 0.0f);
        Vec3 none = Vec3.Zero;

        Vec3.AllWhereAllBitsSet(all).Should().BeTrue();
        Vec3.AllWhereAllBitsSet(two).Should().BeFalse();
        Vec3.AnyWhereAllBitsSet(two).Should().BeTrue();
        Vec3.AnyWhereAllBitsSet(none).Should().BeFalse();
        Vec3.NoneWhereAllBitsSet(none).Should().BeTrue();
        Vec3.NoneWhereAllBitsSet(two).Should().BeFalse();

        Vec3.CountWhereAllBitsSet(all).Should().Be(3);
        Vec3.CountWhereAllBitsSet(two).Should().Be(2);
        Vec3.CountWhereAllBitsSet(none).Should().Be(0);
    }

    [Fact]
    public void EqualsAllAndEqualsAny_CompareComponentsExactly()
    {
        Vec3 value = new(1.0f, 2.0f, 3.0f);

        Vec3.EqualsAll(value, new Vec3(1.0f, 2.0f, 3.0f)).Should().BeTrue();
        Vec3.EqualsAll(value, new Vec3(1.0f, 2.0f, 3.5f)).Should().BeFalse();

        Vec3.EqualsAny(value, new Vec3(9.0f, 9.0f, 3.0f)).Should().BeTrue();
        Vec3.EqualsAny(value, new Vec3(9.0f, 9.0f, 9.0f)).Should().BeFalse();
    }

    [Fact]
    public void ToleranceEquals_AcceptsSignedZeroAndRejectsRealDifferences()
    {
        Vec3 positive = new(0.0f, 0.0f, 0.0f);
        Vec3 negative = new(-0.0f, -0.0f, -0.0f);

        positive.Equals(negative, 0.0f).Should().BeTrue();
        positive.Equals(negative, Vec3.DefaultTolerance).Should().BeTrue();
        Vec3.Equals(positive, negative).Should().BeTrue();
        Vec3.Equals(positive, negative, 0.0f).Should().BeTrue();

        Vec3 one = new(1.0f, 0.0f, 0.0f);
        Vec3.Equals(positive, one, 1.0f).Should().BeTrue();
        Vec3.Equals(positive, one, 0.5f).Should().BeFalse();
        Vec3.Equals(positive, one).Should().BeFalse();
    }

    [Fact]
    public void ToleranceEquals_AppliesTheDefaultToleranceWhenTheCallerOmitsIt()
    {
        Vec3 left = new(1.0f, 2.0f, 3.0f);
        Vec3 right = new(1.0f, 2.0f, 3.0f);

        Vec3.Equals(left, right).Should().Be(Vec3.Equals(left, right, Vec3.DefaultTolerance));
        Vec3.Equals(left, right).Should().BeTrue();

        Vec3 nudged = new(1.0f, 2.0f, 3.0f + Vec3.DefaultTolerance * 0.5f);
        Vec3.Equals(left, nudged).Should().BeTrue();
        Vec3.Equals(left, new Vec3(1.0f, 2.0f, 3.0f + Vec3.DefaultTolerance * 4.0f)).Should().BeFalse();
    }

    [Fact]
    public void BitEquals_DistinguishesSignedZeroWhereExactEqualsDoesNot()
    {
        Vec3 positive = new(0.0f, 0.0f, 0.0f);
        Vec3 negative = new(-0.0f, -0.0f, -0.0f);

        positive.BitEquals(negative).Should().BeFalse();
        (positive == negative).Should().BeTrue("IEEE equality treats +0 and -0 as the same value");
        positive.Equals(negative).Should().BeTrue();

        Vec3 value = new(1.0f, -0.0f, float.NaN);
        value.BitEquals(new Vec3(1.0f, -0.0f, float.NaN)).Should().BeTrue();
        value.BitEquals(new Vec3(1.0f, 0.0f, float.NaN)).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_IsStableForEqualAndBitEqualVectors()
    {
        Vec3 left = new(1.0f, -0.0f, 3.5f);
        Vec3 same = new(1.0f, -0.0f, 3.5f);

        left.GetHashCode().Should().Be(same.GetHashCode());
        left.BitEquals(same).Should().BeTrue();

        Vec3 other = new(1.0f, -0.0f, 3.5000002f);
        left.Equals(other, 1e-5f).Should().BeTrue();
        left.BitEquals(other).Should().BeFalse();

        Vec3.Zero.GetHashCode().Should().Be(Vec3.Zero.GetHashCode());
        Vec3.Zero.GetHashCode().Should().Be(new Vec3(0.0f, 0.0f, 0.0f).GetHashCode());
    }

    [Fact]
    public void AbsCopySignSqrtAndSquareRoot_BehaveComponentWise()
    {
        Vec3 signed = new(-3.0f, 0.0f, 2.0f);

        Vec3.Abs(signed).Should().Be(new Vec3(3.0f, 0.0f, 2.0f));
        Vec3.CopySign(signed, Vec3.NegativeOne).Should().Be(new Vec3(-3.0f, -0.0f, -2.0f));
        Vec3.Sqrt(new Vec3(4.0f, 9.0f, 16.0f)).Should().Be(new Vec3(2.0f, 3.0f, 4.0f));
        Vec3.SquareRoot(new Vec3(4.0f, 9.0f, 16.0f)).Should().Be(Vec3.Sqrt(new Vec3(4.0f, 9.0f, 16.0f)));
        Vec3.Sqrt(new Vec3(-1.0f, 0.0f, 0.0f)).X.Should().BeNaN();
    }

    [Fact]
    public void TrigFamily_MatchesTheScalarMathF()
    {
        Vec3 angles = new(0.0f, 0.5f, -1.25f);

        Vec3.Sin(angles).Should().Be(new Vec3(MathF.Sin(0.0f), MathF.Sin(0.5f), MathF.Sin(-1.25f)));
        Vec3.Cos(angles).Should().Be(new Vec3(MathF.Cos(0.0f), MathF.Cos(0.5f), MathF.Cos(-1.25f)));

        (Vec3 sin, Vec3 cos) = Vec3.SinCos(angles);

        sin.Should().Be(Vec3.Sin(angles));
        cos.Should().Be(Vec3.Cos(angles));
    }

    [Fact]
    public void ExpLogAndLog2_AreInversesWithinTolerance()
    {
        Vec3 values = new(0.5f, 2.0f, 8.0f);

        Vec3.Exp(Vec3.Log(values)).X.Should().BeApproximately(values.X, Tolerance);
        Vec3.Exp(Vec3.Log(values)).Y.Should().BeApproximately(values.Y, Tolerance);
        Vec3.Exp(Vec3.Log(values)).Z.Should().BeApproximately(values.Z, Tolerance);

        Vec3.Log(values).Should().Be(new Vec3(MathF.Log(0.5f), MathF.Log(2.0f), MathF.Log(8.0f)));
        Vec3.Log2(new Vec3(8.0f, 1.0f, 1024.0f)).Should().Be(new Vec3(3.0f, 0.0f, 10.0f));

        Vec3.Exp(Vec3.One).X.Should().BeApproximately(MathF.E, Tolerance);
        Vec3.Log(Vec3.E).X.Should().BeApproximately(1.0f, Tolerance);
        Vec3.Log(new Vec3(0.0f, -1.0f, 1.0f)).X.Should().Be(float.NegativeInfinity);
        Vec3.Log(new Vec3(0.0f, -1.0f, 1.0f)).Y.Should().BeNaN();
    }

    [Fact]
    public void HypotDegreesAndRadians_AreConsistent()
    {
        Vec3.Hypot(new Vec3(3.0f, 0.0f, 5.0f), new Vec3(0.0f, 4.0f, 12.0f))
            .Should().Be(new Vec3(3.0f, 4.0f, 13.0f));

        Vec3.DegreesToRadians(new Vec3(180.0f, 0.0f, 90.0f)).X.Should().BeApproximately(MathF.PI, Tolerance);
        Vec3.DegreesToRadians(new Vec3(180.0f, 0.0f, 90.0f)).Y.Should().Be(0.0f);
        Vec3.DegreesToRadians(new Vec3(180.0f, 0.0f, 90.0f)).Z.Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
        Vec3.RadiansToDegrees(new Vec3(MathF.PI, 0.0f, MathF.PI / 2.0f)).X.Should().BeApproximately(180.0f, Tolerance);
        Vec3.RadiansToDegrees(new Vec3(MathF.PI, 0.0f, MathF.PI / 2.0f)).Y.Should().Be(0.0f);
        Vec3.RadiansToDegrees(new Vec3(MathF.PI, 0.0f, MathF.PI / 2.0f)).Z.Should().BeApproximately(90.0f, Tolerance);
    }

    [Fact]
    public void RoundingFamily_BehavesComponentWise()
    {
        Vec3 values = new(1.7f, -1.7f, 2.5f);

        Vec3.Floor(values).Should().Be(new Vec3(1.0f, -2.0f, 2.0f));
        Vec3.Ceiling(values).Should().Be(new Vec3(2.0f, -1.0f, 3.0f));
        Vec3.Truncate(values).Should().Be(new Vec3(1.0f, -1.0f, 2.0f));
        Vec3.Round(new Vec3(0.5f, 1.5f, 2.5f)).Should().Be(new Vec3(0.0f, 2.0f, 2.0f), "ties round to even");
    }

    [Fact]
    public void TransformCustom_DispatchesToTheTransformerAndMutatesItsState()
    {
        ScaleState state = new(3.0f);

        Vec3 result = Vec3.TransformCustom<ScaleTransformer, ScaleState>(new Vec3(1.0f, -2.0f, 0.5f), ref state);

        result.Should().Be(new Vec3(3.0f, -6.0f, 1.5f));
        state.Calls.Should().Be(1);
    }

    private struct ScaleState
    {
        public ScaleState(float scale) => Scale = scale;

        public float Scale { get; set; }

        public int Calls { get; set; }
    }

    private readonly struct ScaleTransformer : IVectorTransformer<ScaleState>
    {
        public static Vec3 Transform(Vec3 value, scoped ref ScaleState state)
        {
            state.Calls++;
            return value * state.Scale;
        }
    }

    private static bool SameBits(float left, float right) =>
        BitConverter.SingleToUInt32Bits(left) == BitConverter.SingleToUInt32Bits(right);

    private struct Xorshift32
    {
        private uint _state;

        public Xorshift32(uint seed) => _state = seed;

        public Vec3 NextVector() => new(NextFloat(), NextFloat(), NextFloat());

        private uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        private float NextFloat() => (NextUInt() / 4294967295.0f) * 20.0f - 10.0f;
    }
}
