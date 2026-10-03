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
        vec3 left = new(1.0f, 2.0f, 3.0f);
        vec3 right = new(4.0f, -5.0f, 6.0f);

        vec3.Dot(left, right).Should().Be(((1.0f * 4.0f) + (2.0f * -5.0f)) + (3.0f * 6.0f));
        vec3.DotStrict(left, right).Should().Be(((1.0f * 4.0f) + (2.0f * -5.0f)) + (3.0f * 6.0f));
        vec3.Dot(vec3.UnitX, vec3.UnitY).Should().Be(0.0f);
        vec3.Dot(vec3.Zero, vec3.One).Should().Be(0.0f);
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
            vec3 a = rng.NextVector();
            vec3 b = rng.NextVector();

            float contracted = MathF.FusedMultiplyAdd(a.X, b.X, MathF.FusedMultiplyAdd(a.Y, b.Y, a.Z * b.Z));
            float plain = ((a.X * b.X) + (a.Y * b.Y)) + (a.Z * b.Z);

            if (SameBits(vec3.Dot(a, b), contracted))
            {
                contractedMatches++;
            }

            if (!SameBits(vec3.Dot(a, b), plain))
            {
                differsFromStrict++;
            }

            if (SameBits(vec3.DotStrict(a, b), plain))
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
        vec3.Cross(vec3.UnitX, vec3.UnitY).Should().Be(vec3.UnitZ);
        vec3.Cross(vec3.UnitY, vec3.UnitZ).Should().Be(vec3.UnitX);
        vec3.Cross(vec3.UnitZ, vec3.UnitX).Should().Be(vec3.UnitY);
    }

    [Fact]
    public void Cross_IsAntiCommutativeAndPerpendicularToBothInputs()
    {
        vec3 a = new(1.0f, 2.0f, 3.0f);
        vec3 b = new(-4.0f, 0.5f, 2.0f);

        vec3 forward = vec3.Cross(a, b);
        vec3 backward = vec3.Cross(b, a);

        backward.X.Should().BeApproximately(-forward.X, Tolerance);
        backward.Y.Should().BeApproximately(-forward.Y, Tolerance);
        backward.Z.Should().BeApproximately(-forward.Z, Tolerance);

        vec3.Dot(forward, a).Should().BeApproximately(0.0f, LooseTolerance);
        vec3.Dot(forward, b).Should().BeApproximately(0.0f, LooseTolerance);

        vec3.Cross(a, a).Should().Be(vec3.Zero);
        vec3.Cross(a, a * 2.0f).Should().Be(vec3.Zero);
    }

    [Fact]
    public void LengthAndLengthSquared_MatchPythagoras()
    {
        vec3 v = new(3.0f, 4.0f, 0.0f);

        v.LengthSquared().Should().Be(25.0f);
        v.Length().Should().Be(5.0f);
        vec3.Zero.Length().Should().Be(0.0f);
        vec3.One.Length().Should().BeApproximately(MathF.Sqrt(3.0f), Tolerance);
    }

    [Fact]
    public void DistanceSquaredAndDistance_MatchComponentDeltas()
    {
        vec3 a = new(1.0f, 2.0f, 3.0f);
        vec3 b = new(4.0f, 6.0f, 3.0f);

        vec3.DistanceSquared(a, b).Should().Be(25.0f);
        vec3.Distance(a, b).Should().Be(5.0f);
        vec3.Distance(a, a).Should().Be(0.0f);
        vec3.DistanceSquared(a, a).Should().Be(0.0f);

        (vec3.DistanceSquared(a, b) * vec3.Distance(a, b)).Should().BeApproximately(125.0f, LooseTolerance);
    }

    [Fact]
    public void Normalize_YieldsUnitLengthAndPreservesDirection()
    {
        vec3 source = new(3.0f, 4.0f, 12.0f);
        vec3 unit = vec3.Normalize(source);

        unit.Length().Should().BeApproximately(1.0f, Tolerance);
        unit.X.Should().BeApproximately(3.0f / 13.0f, Tolerance);
        unit.Y.Should().BeApproximately(4.0f / 13.0f, Tolerance);
        unit.Z.Should().BeApproximately(12.0f / 13.0f, Tolerance);
    }

    [Fact]
    public void Normalize_OfZeroVector_ReturnsZeroInsteadOfNaN()
    {
        vec3 unit = vec3.Normalize(vec3.Zero);

        unit.Should().Be(vec3.Zero);
        unit.IsAllFinite.Should().BeTrue();
    }

    [Fact]
    public void Normalize_OfVerySmallVector_StaysUnitLength_AndOfUnderflowedLengthReturnsZero()
    {
        vec3 denormal = new(1e-20f, 0.0f, 0.0f);

        vec3 unit = vec3.Normalize(denormal);

        unit.Length().Should().BeApproximately(1.0f, Tolerance);
        unit.X.Should().BeApproximately(1.0f, Tolerance);

        vec3.Normalize(new vec3(1e-30f, 0.0f, 0.0f)).Should().Be(vec3.Zero);
    }

    [Fact]
    public void Normalize_AgreesWithBothCoreStrategiesWithinTolerance()
    {
        Xorshift32 rng = new(0xC0FFEE11u);

        for (int i = 0; i < 512; i++)
        {
            vec3 value = rng.NextVector() * 100.0f;
            vec3 strict = vec3.Normalize<StrictIeeeStrategy>(value);
            vec3 fast = vec3.Normalize<FastApproximationStrategy>(value);
            vec3 total = vec3.Normalize(value);

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
        vec3 source = new(-2.0f, 0.0f, 5.0f);

        bool succeeded = vec3.TryNormalize(source, out vec3 result);

        succeeded.Should().BeTrue();
        result.Length().Should().BeApproximately(1.0f, Tolerance);
        result.X.Should().BeApproximately(vec3.Normalize(source).X, Tolerance);
    }

    [Fact]
    public void TryNormalize_RejectsZeroAndWritesZero()
    {
        bool succeeded = vec3.TryNormalize(vec3.Zero, out vec3 result);

        succeeded.Should().BeFalse();
        result.Should().Be(vec3.Zero);
    }

    [Fact]
    public void TryNormalize_RejectsVectorsAtOrBelowTolerance()
    {
        vec3 small = new(1e-6f, 0.0f, 0.0f);

        vec3.TryNormalize(small, out vec3 strictResult, 1e-4f).Should().BeFalse();
        strictResult.Should().Be(vec3.Zero);

        vec3.TryNormalize(new vec3(1.0f, 0.0f, 0.0f), out vec3 okResult, 1e-4f).Should().BeTrue();
        okResult.Length().Should().BeApproximately(1.0f, Tolerance);

        vec3.TryNormalize(new vec3(1.0f, 0.0f, 0.0f), out vec3 negativeTolerance, -1.0f).Should().BeTrue();
        negativeTolerance.X.Should().BeApproximately(1.0f, Tolerance);
    }

    [Fact]
    public void Reflect_ReversesOnlyTheNormalComponent()
    {
        vec3 incident = new(1.0f, -1.0f, 0.5f);
        vec3 normal = vec3.UnitY;

        vec3 reflected = vec3.Reflect(incident, normal);

        reflected.X.Should().BeApproximately(incident.X, Tolerance);
        reflected.Y.Should().BeApproximately(-incident.Y, Tolerance);
        reflected.Z.Should().BeApproximately(incident.Z, Tolerance);
    }

    [Fact]
    public void Reflect_OfPerpendicularVector_ReturnsItUnchanged()
    {
        vec3 incident = new(2.0f, 0.0f, -3.0f);

        vec3 reflected = vec3.Reflect(incident, vec3.UnitY);

        reflected.X.Should().BeApproximately(incident.X, Tolerance);
        reflected.Y.Should().BeApproximately(0.0f, Tolerance);
        reflected.Z.Should().BeApproximately(incident.Z, Tolerance);
    }

    [Fact]
    public void Project_IsParallelToNormal_AndZeroForDegenerateNormal()
    {
        vec3 vector = new(3.0f, 4.0f, 0.0f);
        vec3 normal = new(0.0f, 0.0f, 7.0f);

        vec3 projected = vec3.Project(vector, normal);

        vec3.Cross(projected, normal).Length().Should().BeApproximately(0.0f, LooseTolerance);
        projected.Z.Should().BeApproximately(0.0f, Tolerance);

        vec3.Project(vector, vec3.Zero).Should().Be(vec3.Zero);
        vec3.ProjectOnPlane(vector, vec3.Zero).Should().Be(vector);
    }

    [Fact]
    public void ProjectOnPlane_PlusProject_ReconstructsTheInput()
    {
        vec3 vector = new(1.0f, 2.0f, -3.0f);
        vec3 normal = new(0.0f, 2.0f, 0.0f);

        vec3 inPlane = vec3.ProjectOnPlane(vector, normal);
        vec3 alongNormal = vec3.Project(vector, normal);

        (inPlane.X + alongNormal.X).Should().BeApproximately(vector.X, LooseTolerance);
        (inPlane.Y + alongNormal.Y).Should().BeApproximately(vector.Y, LooseTolerance);
        (inPlane.Z + alongNormal.Z).Should().BeApproximately(vector.Z, LooseTolerance);

        vec3.Dot(inPlane, normal).Should().BeApproximately(0.0f, LooseTolerance);
    }

    [Fact]
    public void Slide_MatchesProjectOnPlane_AndIsPerpendicularToTheNormal()
    {
        vec3 vector = new(1.0f, 1.0f, 0.0f);
        vec3 normal = new(0.0f, 2.0f, 0.0f);

        vec3 slid = vec3.Slide(vector, normal);
        vec3 projected = vec3.ProjectOnPlane(vector, normal);

        slid.X.Should().BeApproximately(projected.X, Tolerance);
        slid.Y.Should().BeApproximately(projected.Y, Tolerance);
        slid.Z.Should().BeApproximately(projected.Z, Tolerance);
        vec3.Dot(slid, normal).Should().BeApproximately(0.0f, LooseTolerance);

        vec3.Slide(vector, vec3.Zero).Should().Be(vector);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1e5f)]
    [InlineData(1e10f)]
    [InlineData(1e19f)]
    public void Angle_OfIdenticalVectors_IsZeroAtAnyMagnitude(float scale)
    {
        vec3 value = new(scale, scale * 0.5f, 0.0f);

        float angle = vec3.Angle(value, value);

        angle.Should().BeApproximately(0.0f, 1e-5f, "both operands are pre-scaled by their largest component");
    }

    [Fact]
    public void Angle_OfOppositeVectors_IsPi()
    {
        vec3 value = new(3.0f, 0.0f, 0.0f);

        vec3.Angle(value, -value).Should().BeApproximately(MathF.PI, 1e-4f);
    }

    [Fact]
    public void Angle_OfOrthogonalVectors_IsHalfPi()
    {
        vec3.Angle(vec3.UnitX, vec3.UnitY).Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
        vec3.Angle(vec3.UnitY, vec3.UnitZ).Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
        vec3.Angle(vec3.UnitZ, vec3.UnitX).Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
    }

    [Fact]
    public void Angle_IsSymmetric_AndZeroForDegenerateInput()
    {
        vec3 a = new(1.0f, 2.0f, 3.0f);
        vec3 b = new(-2.0f, 0.5f, 1.0f);

        vec3.Angle(a, b).Should().BeApproximately(vec3.Angle(b, a), Tolerance);
        vec3.Angle(a, vec3.Zero).Should().Be(0.0f);
        vec3.Angle(vec3.Zero, a).Should().Be(0.0f);
        vec3.Angle(vec3.Zero, vec3.Zero).Should().Be(0.0f);
    }

    [Fact]
    public void SignedAngle_FollowsTheRightHandRuleAroundTheAxis()
    {
        vec3 from = vec3.UnitX;
        vec3 to = vec3.UnitY;
        vec3 axis = vec3.UnitZ;

        float positive = vec3.SignedAngle(from, to, axis);
        float negative = vec3.SignedAngle(to, from, axis);

        positive.Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
        negative.Should().BeApproximately(-MathF.PI / 2.0f, Tolerance);

        vec3.SignedAngle(from, to, -axis).Should().BeApproximately(-positive, Tolerance);
        vec3.SignedAngle(from, to, axis * 10.0f).Should().BeApproximately(positive, Tolerance);
    }

    [Fact]
    public void Clamp_BoundsEachComponentIndependently()
    {
        vec3 value = new(5.0f, -5.0f, 0.5f);
        vec3 min = new(-1.0f, -1.0f, -1.0f);
        vec3 max = new(1.0f, 1.0f, 1.0f);

        vec3 clamped = vec3.Clamp(value, min, max);

        clamped.X.Should().Be(1.0f);
        clamped.Y.Should().Be(-1.0f);
        clamped.Z.Should().Be(0.5f);
    }

    [Fact]
    public void Clamp_WithInvertedBounds_ResolvesToTheUpperBoundWithoutThrowing()
    {
        vec3 value = new(5.0f, 5.0f, 5.0f);
        vec3 min = new(10.0f, 10.0f, 10.0f);
        vec3 max = new(1.0f, 1.0f, 1.0f);

        vec3 clamped = vec3.Clamp(value, min, max);

        clamped.Should().Be(max);
        clamped.IsAllFinite.Should().BeTrue();
    }

    [Fact]
    public void ClampNative_MatchesClamp()
    {
        vec3 value = new(3.0f, -3.0f, 0.0f);
        vec3 min = new(-2.0f, -2.0f, -2.0f);
        vec3 max = new(2.0f, 2.0f, 2.0f);

        vec3.ClampNative(value, min, max).Should().Be(vec3.Clamp(value, min, max));
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(1.0f)]
    [InlineData(5.0f)]
    [InlineData(50.0f)]
    public void ClampLength_KeepsTheResultInsideTheBounds(float scale)
    {
        vec3 value = new(scale, 0.0f, 0.0f);

        vec3 clamped = vec3.ClampLength(value, 1.0f, 10.0f);

        clamped.Length().Should().BeApproximately(Math.Clamp(scale, 1.0f, 10.0f), LooseTolerance);
    }

    [Fact]
    public void ClampLength_OfZeroVector_UsesThePositiveXAxisToHonourMinLength()
    {
        vec3 clamped = vec3.ClampLength(vec3.Zero, 5.0f, 10.0f);

        clamped.X.Should().Be(5.0f);
        clamped.Length().Should().BeApproximately(5.0f, Tolerance);

        vec3.ClampLength(vec3.Zero, 0.0f, 10.0f).Should().Be(vec3.Zero);
        vec3.ClampLength(vec3.Zero, -1.0f, 10.0f).Should().Be(vec3.Zero);
    }

    [Fact]
    public void Lerp_ReturnsEndpointsAtZeroAndOne_AndIsFmaContracted()
    {
        vec3 a = new(1.0f, 2.0f, 3.0f);
        vec3 b = new(4.0f, 6.0f, 8.0f);

        vec3.Lerp(a, b, 0.0f).Should().Be(a);
        vec3.Lerp(a, b, 1.0f).Should().Be(b);

        vec3.Lerp(a, b, 0.5f).X.Should().BeApproximately(2.5f, Tolerance);
        vec3.Lerp(a, b, 0.5f).Y.Should().BeApproximately(4.0f, Tolerance);
        vec3.Lerp(a, b, 0.5f).Z.Should().BeApproximately(5.5f, Tolerance);

        vec3.Lerp(a, b, 0.25f).X.Should().Be(MathF.FusedMultiplyAdd(b.X - a.X, 0.25f, a.X));
    }

    [Fact]
    public void LerpClamped_ClampsTheFactorToTheSegment()
    {
        vec3 a = new(0.0f, 0.0f, 0.0f);
        vec3 b = new(10.0f, 10.0f, 10.0f);

        vec3.LerpClamped(a, b, -1.0f).Should().Be(a);
        vec3.LerpClamped(a, b, 2.0f).Should().Be(b);
        vec3.LerpClamped(a, b, 0.25f).X.Should().BeApproximately(2.5f, Tolerance);
    }

    [Fact]
    public void SmoothStep_MatchesTheHermiteCurve_AndClampsTheAmount()
    {
        vec3 from = new(0.0f, 0.0f, 0.0f);
        vec3 to = new(8.0f, 8.0f, 8.0f);

        vec3.SmoothStep(from, to, 0.0f).Should().Be(from);
        vec3.SmoothStep(from, to, 1.0f).Should().Be(to);
        vec3.SmoothStep(from, to, -1.0f).Should().Be(from);
        vec3.SmoothStep(from, to, 2.0f).Should().Be(to);

        vec3.SmoothStep(from, to, 0.25f).X.Should().BeApproximately(1.25f, Tolerance);
        vec3.SmoothStep(from, to, 0.75f).X.Should().BeApproximately(6.75f, Tolerance);
    }

    [Theory]
    [InlineData(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f)]
    [InlineData(1.0f, 2.0f, 3.0f, -2.0f, 1.0f, 4.0f)]
    [InlineData(1.0f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f)]
    public void Slerp_EndpointsReproduceTheInputs(float ax, float ay, float az, float bx, float by, float bz)
    {
        vec3 a = new(ax, ay, az);
        vec3 b = new(bx, by, bz);

        vec3 atStart = vec3.Slerp(a, b, 0.0f);
        vec3 atEnd = vec3.Slerp(a, b, 1.0f);

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
        vec3 a = new(2.0f, 0.0f, 0.0f);
        vec3 b = new(0.0f, 2.0f, 0.0f);

        for (int step = 0; step <= 10; step++)
        {
            vec3 slerped = vec3.Slerp(a, b, step / 10.0f);

            slerped.Length().Should().BeApproximately(2.0f, 1e-3f, $"at t = {step / 10.0f}");
        }
    }

    [Fact]
    public void Slerp_OfAntipodalInputs_StaysFiniteAndOnTheSphere()
    {
        vec3 a = vec3.UnitX * 3.0f;
        vec3 b = -a;

        for (int step = 0; step <= 4; step++)
        {
            vec3 slerped = vec3.Slerp(a, b, step / 4.0f);

            slerped.IsAllFinite.Should().BeTrue($"the antipodal branch must not divide by sin(pi) at t = {step / 4.0f}");
            slerped.Length().Should().BeApproximately(3.0f, LooseTolerance);
        }
    }

    [Fact]
    public void Slerp_OfDegenerateInput_FallsBackToLerp()
    {
        vec3 a = new(0.0f, 0.0f, 0.0f);
        vec3 b = new(4.0f, 0.0f, 0.0f);

        vec3 slerped = vec3.Slerp(a, b, 0.5f);

        slerped.X.Should().BeApproximately(2.0f, Tolerance);
        slerped.Should().Be(vec3.Lerp(a, b, 0.5f));

        vec3.Slerp(a, b, 0.0f).Should().Be(a);
        vec3.Slerp(a, b, 1.0f).Should().Be(b);
    }

    [Fact]
    public void GetOrthogonal_ReturnsAUnitVectorPerpendicularToTheInput()
    {
        Xorshift32 rng = new(0x5EED1234u);

        for (int i = 0; i < 128; i++)
        {
            vec3 value = rng.NextVector();
            vec3 orthogonal = vec3.GetOrthogonal(value);

            orthogonal.Length().Should().BeApproximately(1.0f, 1e-4f);
            MathF.Abs(vec3.Dot(value, orthogonal)).Should().BeLessThan(1e-3f);
        }
    }

    [Fact]
    public void GetOrthogonal_OfZeroVector_ReturnsTheUnitXAxis()
    {
        vec3 orthogonal = vec3.GetOrthogonal(vec3.Zero);

        orthogonal.Should().Be(vec3.UnitX);
        orthogonal.Length().Should().BeApproximately(1.0f, Tolerance);
    }

    [Fact]
    public void MoveTowards_StepsByAtMostTheDelta()
    {
        vec3 current = new(0.0f, 0.0f, 0.0f);
        vec3 target = new(10.0f, 0.0f, 0.0f);

        vec3 moved = vec3.MoveTowards(current, target, 3.0f);

        moved.X.Should().BeApproximately(3.0f, Tolerance);
        moved.Should().Be(vec3.MoveTowards(current, target, 3.0f));
    }

    [Fact]
    public void MoveTowards_DoesNotOvershootTheTarget()
    {
        vec3 current = new(0.0f, 0.0f, 0.0f);
        vec3 target = new(10.0f, 0.0f, 0.0f);

        vec3.MoveTowards(current, target, 100.0f).Should().Be(target);
        vec3.MoveTowards(current, target, 10.0f).Should().Be(target);
        vec3.MoveTowards(current, current, 5.0f).Should().Be(current);
    }

    [Fact]
    public void MoveTowards_ClampsTheDiagonalStep()
    {
        vec3 current = new(1.0f, 1.0f, 1.0f);
        vec3 target = new(4.0f, 5.0f, 1.0f);

        vec3 moved = vec3.MoveTowards(current, target, 1.0f);

        vec3.Distance(current, moved).Should().BeApproximately(1.0f, Tolerance);
        vec3.Distance(moved, target).Should().BeApproximately(4.0f, LooseTolerance);
    }

    [Fact]
    public void MinAndMax_SelectPerComponent_AndPropagateNaN()
    {
        vec3 left = new(1.0f, 5.0f, -2.0f);
        vec3 right = new(4.0f, 2.0f, -7.0f);

        vec3.Min(left, right).Should().Be(new vec3(1.0f, 2.0f, -7.0f));
        vec3.Max(left, right).Should().Be(new vec3(4.0f, 5.0f, -2.0f));

        vec3.MinNative(left, right).Should().Be(vec3.Min(left, right));
        vec3.MaxNative(left, right).Should().Be(vec3.Max(left, right));

        vec3 withNaN = new(float.NaN, float.NaN, float.NaN);
        vec3.Min(withNaN, left).IsAnyNaN.Should().BeTrue();
        vec3.Max(withNaN, left).IsAnyNaN.Should().BeTrue();
    }

    [Fact]
    public void MinNumberAndMaxNumber_TreatNaNAsMissingData()
    {
        vec3 left = new(float.NaN, 5.0f, -1.0f);
        vec3 right = new(3.0f, float.NaN, 7.0f);

        vec3 minimum = vec3.MinNumber(left, right);
        vec3 maximum = vec3.MaxNumber(left, right);

        minimum.X.Should().Be(3.0f);
        minimum.Y.Should().Be(5.0f);
        minimum.Z.Should().Be(-1.0f);
        maximum.X.Should().Be(3.0f);
        maximum.Y.Should().Be(5.0f);
        maximum.Z.Should().Be(7.0f);

        vec3.MinNumber(vec3.NaN, vec3.NaN).IsAnyNaN.Should().BeTrue();
    }

    [Fact]
    public void MinMagnitudeAndMaxMagnitude_SelectByMagnitudeAndPropagateNaN()
    {
        vec3 left = new(5.0f, -3.0f, 0.0f);
        vec3 right = new(-7.0f, 8.0f, 0.0f);

        vec3.MinMagnitude(left, right).Should().Be(left);
        vec3.MaxMagnitude(left, right).Should().Be(right);

        vec3 withNaN = vec3.NaN;
        vec3.MinMagnitude(withNaN, left).IsAnyNaN.Should().BeTrue();
        vec3.MaxMagnitude(withNaN, left).IsAnyNaN.Should().BeTrue();
    }

    [Fact]
    public void MagnitudeNumberVariants_TreatNaNAsMissingData_AndBreakZeroTies()
    {
        vec3 left = new(float.NaN, -5.0f, 0.0f);
        vec3 right = new(3.0f, 3.0f, -0.0f);

        vec3.MinMagnitudeNumber(left, right).X.Should().Be(3.0f);
        vec3.MinMagnitudeNumber(left, right).Y.Should().Be(3.0f);
        vec3.MaxMagnitudeNumber(left, right).X.Should().Be(3.0f);
        vec3.MaxMagnitudeNumber(left, right).Y.Should().Be(-5.0f);

        vec3 positiveZero = new(0.0f, 0.0f, 0.0f);
        vec3 negativeZero = new(-0.0f, -0.0f, -0.0f);

        BitConverter.SingleToUInt32Bits(vec3.MinMagnitudeNumber(positiveZero, negativeZero).X)
            .Should().Be(0x80000000U, "a magnitude tie resolves to negative zero");
        BitConverter.SingleToUInt32Bits(vec3.MaxMagnitudeNumber(positiveZero, negativeZero).X)
            .Should().Be(0x00000000U, "a magnitude tie resolves to positive zero");
    }

    [Fact]
    public void FusedMultiplyAdd_RoundsOnceAndDiffersFromTheTwoStepExpression()
    {
        vec3 left = new(9.342449f, 0.7648649f, 5807700.0f);
        vec3 right = new(9.11887f, 5.398081f, 7.885076E-06f);
        vec3 addend = new(3.4515495f, 6.5211143f, 556.85046f);

        vec3 fused = vec3.FusedMultiplyAdd(left, right, addend);

        fused.X.Should().Be(MathF.FusedMultiplyAdd(left.X, right.X, addend.X));
        fused.Y.Should().Be(MathF.FusedMultiplyAdd(left.Y, right.Y, addend.Y));
        fused.Z.Should().Be(MathF.FusedMultiplyAdd(left.Z, right.Z, addend.Z));

        SameBits(fused.X, (left.X * right.X) + addend.X).Should().BeFalse("the first component only survives a single rounding");
        SameBits(fused.Y, (left.Y * right.Y) + addend.Y).Should().BeFalse();
        SameBits(fused.Z, (left.Z * right.Z) + addend.Z).Should().BeFalse();

        vec3.MultiplyAddEstimate(left, right, addend).Should().Be(fused);
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

        vec3 fused = vec3.FusedMultiplyAdd(new vec3(Left), new vec3(Right), new vec3(Addend));

        fused.X.Should().Be(oracle);
        fused.Y.Should().Be(oracle);
        fused.Z.Should().Be(oracle);
        fused.X.Should().NotBe(naive);
    }

    [Fact]
    public void SumAll_AddsComponents_AndIsZeroForAnEmptyInput()
    {
        vec3 a = new(1.0f, 2.0f, 3.0f);
        vec3 b = new(4.0f, 5.0f, 6.0f);
        vec3 c = new(7.0f, 8.0f, 9.0f);

        vec3.SumAll(a, b, c).Should().Be(new vec3(12.0f, 15.0f, 18.0f));
        vec3.SumAll(a).Should().Be(a);
        vec3.SumAll(ReadOnlySpan<vec3>.Empty).Should().Be(vec3.Zero);

        vec3.Sum(a).Should().Be(6.0f);
        vec3.Sum(vec3.Zero).Should().Be(0.0f);
    }

    [Fact]
    public void Average_DividesTheSumByTheCount()
    {
        vec3 a = new(1.0f, 2.0f, 3.0f);
        vec3 b = new(4.0f, 5.0f, 6.0f);
        vec3 c = new(7.0f, 8.0f, 9.0f);

        vec3.Average(a, b, c).Should().Be(new vec3(4.0f, 5.0f, 6.0f));
        vec3.Average(a).Should().Be(a);
    }

    [Fact]
    public void Average_OfAnEmptyInput_Throws()
    {
        Action act = () => vec3.Average(ReadOnlySpan<vec3>.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllAnyNoneAndCount_AgreeOnNonZeroComponents()
    {
        vec3 none = vec3.Zero;
        vec3 all = vec3.One;
        vec3 mixed = new(1.0f, 0.0f, -3.0f);

        vec3.All(none).Should().BeFalse();
        vec3.All(all).Should().BeTrue();
        vec3.All(mixed).Should().BeFalse();

        vec3.Any(none).Should().BeFalse();
        vec3.Any(all).Should().BeTrue();
        vec3.Any(mixed).Should().BeTrue();

        vec3.None(none).Should().BeTrue();
        vec3.None(all).Should().BeFalse();
        vec3.None(mixed).Should().BeFalse();

        vec3.Count(none).Should().Be(0);
        vec3.Count(all).Should().Be(3);
        vec3.Count(mixed).Should().Be(2);
    }

    [Fact]
    public void WhereAllBitsSetFamily_CountsMaskedComponents()
    {
        vec3 all = vec3.AllBitsSet;
        vec3 two = new(vec3.AllBitsSet.X, vec3.AllBitsSet.Y, 0.0f);
        vec3 none = vec3.Zero;

        vec3.AllWhereAllBitsSet(all).Should().BeTrue();
        vec3.AllWhereAllBitsSet(two).Should().BeFalse();
        vec3.AnyWhereAllBitsSet(two).Should().BeTrue();
        vec3.AnyWhereAllBitsSet(none).Should().BeFalse();
        vec3.NoneWhereAllBitsSet(none).Should().BeTrue();
        vec3.NoneWhereAllBitsSet(two).Should().BeFalse();

        vec3.CountWhereAllBitsSet(all).Should().Be(3);
        vec3.CountWhereAllBitsSet(two).Should().Be(2);
        vec3.CountWhereAllBitsSet(none).Should().Be(0);
    }

    [Fact]
    public void EqualsAllAndEqualsAny_CompareComponentsExactly()
    {
        vec3 value = new(1.0f, 2.0f, 3.0f);

        vec3.EqualsAll(value, new vec3(1.0f, 2.0f, 3.0f)).Should().BeTrue();
        vec3.EqualsAll(value, new vec3(1.0f, 2.0f, 3.5f)).Should().BeFalse();

        vec3.EqualsAny(value, new vec3(9.0f, 9.0f, 3.0f)).Should().BeTrue();
        vec3.EqualsAny(value, new vec3(9.0f, 9.0f, 9.0f)).Should().BeFalse();
    }

    [Fact]
    public void ToleranceEquals_AcceptsSignedZeroAndRejectsRealDifferences()
    {
        vec3 positive = new(0.0f, 0.0f, 0.0f);
        vec3 negative = new(-0.0f, -0.0f, -0.0f);

        positive.Equals(negative, 0.0f).Should().BeTrue();
        positive.Equals(negative, vec3.DefaultTolerance).Should().BeTrue();
        vec3.Equals(positive, negative).Should().BeTrue();
        vec3.Equals(positive, negative, 0.0f).Should().BeTrue();

        vec3 one = new(1.0f, 0.0f, 0.0f);
        vec3.Equals(positive, one, 1.0f).Should().BeTrue();
        vec3.Equals(positive, one, 0.5f).Should().BeFalse();
        vec3.Equals(positive, one).Should().BeFalse();
    }

    [Fact]
    public void ToleranceEquals_AppliesTheDefaultToleranceWhenTheCallerOmitsIt()
    {
        vec3 left = new(1.0f, 2.0f, 3.0f);
        vec3 right = new(1.0f, 2.0f, 3.0f);

        vec3.Equals(left, right).Should().Be(vec3.Equals(left, right, vec3.DefaultTolerance));
        vec3.Equals(left, right).Should().BeTrue();

        vec3 nudged = new(1.0f, 2.0f, 3.0f + vec3.DefaultTolerance * 0.5f);
        vec3.Equals(left, nudged).Should().BeTrue();
        vec3.Equals(left, new vec3(1.0f, 2.0f, 3.0f + vec3.DefaultTolerance * 4.0f)).Should().BeFalse();
    }

    [Fact]
    public void BitEquals_DistinguishesSignedZeroWhereExactEqualsDoesNot()
    {
        vec3 positive = new(0.0f, 0.0f, 0.0f);
        vec3 negative = new(-0.0f, -0.0f, -0.0f);

        positive.BitEquals(negative).Should().BeFalse();
        (positive == negative).Should().BeTrue("IEEE equality treats +0 and -0 as the same value");
        positive.Equals(negative).Should().BeTrue();

        vec3 value = new(1.0f, -0.0f, float.NaN);
        value.BitEquals(new vec3(1.0f, -0.0f, float.NaN)).Should().BeTrue();
        value.BitEquals(new vec3(1.0f, 0.0f, float.NaN)).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_IsStableForEqualAndBitEqualVectors()
    {
        vec3 left = new(1.0f, -0.0f, 3.5f);
        vec3 same = new(1.0f, -0.0f, 3.5f);

        left.GetHashCode().Should().Be(same.GetHashCode());
        left.BitEquals(same).Should().BeTrue();

        vec3 other = new(1.0f, -0.0f, 3.5000002f);
        left.Equals(other, 1e-5f).Should().BeTrue();
        left.BitEquals(other).Should().BeFalse();

        vec3.Zero.GetHashCode().Should().Be(vec3.Zero.GetHashCode());
        vec3.Zero.GetHashCode().Should().Be(new vec3(0.0f, 0.0f, 0.0f).GetHashCode());
    }

    [Fact]
    public void AbsCopySignSqrtAndSquareRoot_BehaveComponentWise()
    {
        vec3 signed = new(-3.0f, 0.0f, 2.0f);

        vec3.Abs(signed).Should().Be(new vec3(3.0f, 0.0f, 2.0f));
        vec3.CopySign(signed, vec3.NegativeOne).Should().Be(new vec3(-3.0f, -0.0f, -2.0f));
        vec3.Sqrt(new vec3(4.0f, 9.0f, 16.0f)).Should().Be(new vec3(2.0f, 3.0f, 4.0f));
        vec3.SquareRoot(new vec3(4.0f, 9.0f, 16.0f)).Should().Be(vec3.Sqrt(new vec3(4.0f, 9.0f, 16.0f)));
        vec3.Sqrt(new vec3(-1.0f, 0.0f, 0.0f)).X.Should().BeNaN();
    }

    [Fact]
    public void TrigFamily_MatchesTheScalarMathF()
    {
        vec3 angles = new(0.0f, 0.5f, -1.25f);

        vec3.Sin(angles).Should().Be(new vec3(MathF.Sin(0.0f), MathF.Sin(0.5f), MathF.Sin(-1.25f)));
        vec3.Cos(angles).Should().Be(new vec3(MathF.Cos(0.0f), MathF.Cos(0.5f), MathF.Cos(-1.25f)));

        (vec3 sin, vec3 cos) = vec3.SinCos(angles);

        sin.Should().Be(vec3.Sin(angles));
        cos.Should().Be(vec3.Cos(angles));
    }

    [Fact]
    public void ExpLogAndLog2_AreInversesWithinTolerance()
    {
        vec3 values = new(0.5f, 2.0f, 8.0f);

        vec3.Exp(vec3.Log(values)).X.Should().BeApproximately(values.X, Tolerance);
        vec3.Exp(vec3.Log(values)).Y.Should().BeApproximately(values.Y, Tolerance);
        vec3.Exp(vec3.Log(values)).Z.Should().BeApproximately(values.Z, Tolerance);

        vec3.Log(values).Should().Be(new vec3(MathF.Log(0.5f), MathF.Log(2.0f), MathF.Log(8.0f)));
        vec3.Log2(new vec3(8.0f, 1.0f, 1024.0f)).Should().Be(new vec3(3.0f, 0.0f, 10.0f));

        vec3.Exp(vec3.One).X.Should().BeApproximately(MathF.E, Tolerance);
        vec3.Log(vec3.E).X.Should().BeApproximately(1.0f, Tolerance);
        vec3.Log(new vec3(0.0f, -1.0f, 1.0f)).X.Should().Be(float.NegativeInfinity);
        vec3.Log(new vec3(0.0f, -1.0f, 1.0f)).Y.Should().BeNaN();
    }

    [Fact]
    public void HypotDegreesAndRadians_AreConsistent()
    {
        vec3.Hypot(new vec3(3.0f, 0.0f, 5.0f), new vec3(0.0f, 4.0f, 12.0f))
            .Should().Be(new vec3(3.0f, 4.0f, 13.0f));

        vec3.DegreesToRadians(new vec3(180.0f, 0.0f, 90.0f)).X.Should().BeApproximately(MathF.PI, Tolerance);
        vec3.DegreesToRadians(new vec3(180.0f, 0.0f, 90.0f)).Y.Should().Be(0.0f);
        vec3.DegreesToRadians(new vec3(180.0f, 0.0f, 90.0f)).Z.Should().BeApproximately(MathF.PI / 2.0f, Tolerance);
        vec3.RadiansToDegrees(new vec3(MathF.PI, 0.0f, MathF.PI / 2.0f)).X.Should().BeApproximately(180.0f, Tolerance);
        vec3.RadiansToDegrees(new vec3(MathF.PI, 0.0f, MathF.PI / 2.0f)).Y.Should().Be(0.0f);
        vec3.RadiansToDegrees(new vec3(MathF.PI, 0.0f, MathF.PI / 2.0f)).Z.Should().BeApproximately(90.0f, Tolerance);
    }

    [Fact]
    public void RoundingFamily_BehavesComponentWise()
    {
        vec3 values = new(1.7f, -1.7f, 2.5f);

        vec3.Floor(values).Should().Be(new vec3(1.0f, -2.0f, 2.0f));
        vec3.Ceiling(values).Should().Be(new vec3(2.0f, -1.0f, 3.0f));
        vec3.Truncate(values).Should().Be(new vec3(1.0f, -1.0f, 2.0f));
        vec3.Round(new vec3(0.5f, 1.5f, 2.5f)).Should().Be(new vec3(0.0f, 2.0f, 2.0f), "ties round to even");
    }

    [Fact]
    public void TransformCustom_DispatchesToTheTransformerAndMutatesItsState()
    {
        ScaleState state = new(3.0f);

        vec3 result = vec3.TransformCustom<ScaleTransformer, ScaleState>(new vec3(1.0f, -2.0f, 0.5f), ref state);

        result.Should().Be(new vec3(3.0f, -6.0f, 1.5f));
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
        public static vec3 Transform(vec3 value, scoped ref ScaleState state)
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

        public vec3 NextVector() => new(NextFloat(), NextFloat(), NextFloat());

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
