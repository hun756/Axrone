
namespace Axrone.Batching.Tests;

/// <summary>
/// Parity coverage for the geometry kernels. Every case compares the vectorised path against
/// <see cref="Vec3.Transform(Vec3, Mat4)"/> / the <see cref="Mat4"/> row-vector product
/// at sizes that straddle the vector width, so a mistake in the tail cannot hide behind a SIMD
/// block that happens to be right.
/// </summary>
public class SimdBatchKernelTests
{
    private const float Tolerance = 1e-4f;

    private static Mat4 BuildMatrix(SeededRng rng) => new(
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f);

    private static Vec3[] BuildPositions(int count, SeededRng rng)
    {
        var data = new Vec3[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Vec3(rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f);
        }

        return data;
    }

    private static Vec4[] BuildVectors(int count, SeededRng rng)
    {
        var data = new Vec4[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Vec4(rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f,
                               rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f);
        }

        return data;
    }

    /// <summary>Row-vector homogeneous transform written out longhand, as the scalar reference.</summary>
    private static Vec4 TransformAffineScalar(in Vec4 v, in Mat4 m) => new(
        (v.X * m.M11) + (v.Y * m.M21) + (v.Z * m.M31) + (v.W * m.M41),
        (v.X * m.M12) + (v.Y * m.M22) + (v.Z * m.M32) + (v.W * m.M42),
        (v.X * m.M13) + (v.Y * m.M23) + (v.Z * m.M33) + (v.W * m.M43),
        (v.X * m.M14) + (v.Y * m.M24) + (v.Z * m.M34) + (v.W * m.M44));

    private static void AssertClose(Vec3 actual, Vec3 expected, string because)
    {
        actual.X.Should().BeApproximately(expected.X, Tolerance, because);
        actual.Y.Should().BeApproximately(expected.Y, Tolerance, because);
        actual.Z.Should().BeApproximately(expected.Z, Tolerance, because);
    }

    private static void AssertClose(Vec4 actual, Vec4 expected, string because)
    {
        actual.X.Should().BeApproximately(expected.X, Tolerance, because);
        actual.Y.Should().BeApproximately(expected.Y, Tolerance, because);
        actual.Z.Should().BeApproximately(expected.Z, Tolerance, because);
        actual.W.Should().BeApproximately(expected.W, Tolerance, because);
    }

    // ── TransformPositions3D ────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(64)]
    [InlineData(100)]
    public void TransformPositions3D_MatchesScalarTransform(int count)
    {
        var rng = new SeededRng((ulong)count + 101);
        var source = BuildPositions(count, rng);
        var destination = new Vec3[count];
        var matrix = BuildMatrix(rng);

        SimdBatchKernels.TransformPositions3D(source, destination, matrix);

        for (var i = 0; i < count; i++)
        {
            AssertClose(destination[i], Vec3.Transform(source[i], matrix), $"index {i}, count {count}");
        }
    }

    [Fact]
    public void TransformPositions3D_InPlace_OverwritesSourceCorrectly()
    {
        var rng = new SeededRng(404);
        var data = BuildPositions(20, rng);
        var matrix = BuildMatrix(rng);
        var expected = new Vec3[20];
        for (var i = 0; i < 20; i++)
        {
            expected[i] = Vec3.Transform(data[i], matrix);
        }

        SimdBatchKernels.TransformPositions3D(data, data, matrix);

        for (var i = 0; i < 20; i++)
        {
            AssertClose(data[i], expected[i], $"in-place index {i}");
        }
    }

    [Fact]
    public void TransformPositions3D_IdentityMatrix_IsAnExactCopy()
    {
        var rng = new SeededRng(7);
        var source = BuildPositions(12, rng);
        var destination = new Vec3[12];

        SimdBatchKernels.TransformPositions3D(source, destination, Mat4.Identity);

        destination.Should().Equal(source);
    }

    [Fact]
    public void TransformPositions3D_ShortDestination_Throws()
    {
        var source = BuildPositions(9, new SeededRng(1));
        var destination = new Vec3[8];

        var act = () => SimdBatchKernels.TransformPositions3D(source, destination, Mat4.Identity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TransformPositions3D_TranslationOnlyIgnoresPerspectiveRow()
    {
        // Positions are directional-free by contract: M41..M43 translate, M14..M34 must not scale them.
        var matrix = Mat4.CreateTranslation(3f, 4f, 5f);
        var source = BuildPositions(10, new SeededRng(2));
        var destination = new Vec3[10];

        SimdBatchKernels.TransformPositions3D(source, destination, matrix);

        AssertClose(destination[0], source[0] + new Vec3(3f, 4f, 5f), "translation");
    }

    // ── TransformAffine ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(33)]
    public void TransformAffine_MatchesScalarTransform(int count)
    {
        var rng = new SeededRng((ulong)count + 211);
        var source = BuildVectors(count, rng);
        var destination = new Vec4[count];
        var matrix = BuildMatrix(rng);

        SimdBatchKernels.TransformAffine(source, destination, matrix);

        for (var i = 0; i < count; i++)
        {
            AssertClose(destination[i], TransformAffineScalar(in source[i], in matrix), $"index {i}, count {count}");
        }
    }

    [Fact]
    public void TransformAffine_HonoursPerspectiveRow()
    {
        // Unlike positions, the W component must mix in row 4 — this is what separates the two kernels.
        var matrix = new Mat4(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            2f, 3f, 4f, 5f);
        var source = new[] { new Vec4(1f, 1f, 1f, 1f), new Vec4(2f, 0f, 0f, 3f) };
        var destination = new Vec4[2];

        SimdBatchKernels.TransformAffine(source, destination, matrix);

        AssertClose(destination[0], TransformAffineScalar(in source[0], in matrix), "w mixing");
        AssertClose(destination[1], TransformAffineScalar(in source[1], in matrix), "w mixing");
        destination[1].W.Should().BeApproximately(3f * 5f, Tolerance, "M14..M34 are zero, so only v.W feeds W");
    }

    [Fact]
    public void TransformAffine_ShortDestination_Throws()
    {
        var source = BuildVectors(4, new SeededRng(3));
        var destination = new Vec4[3];

        var act = () => SimdBatchKernels.TransformAffine(source, destination, Mat4.Identity);

        act.Should().Throw<ArgumentException>();
    }

    // ── IntegrateVelocity ───────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(43)]
    public void IntegrateVelocity_MatchesScalarAccumulation(int count)
    {
        const float dt = 0.016f;
        var rng = new SeededRng((ulong)count + 307);
        var positions = BuildPositions(count, rng);
        var velocities = BuildPositions(count, rng);
        var expected = new Vec3[count];
        for (var i = 0; i < count; i++)
        {
            expected[i] = positions[i] + (velocities[i] * dt);
        }

        SimdBatchKernels.IntegrateVelocity(positions, velocities, dt);

        for (var i = 0; i < count; i++)
        {
            AssertClose(positions[i], expected[i], $"index {i}, count {count}");
        }
    }

    [Fact]
    public void IntegrateVelocity_ZeroDeltaTime_LeavesPositionsUnchanged()
    {
        var rng = new SeededRng(11);
        var positions = BuildPositions(30, rng);
        var velocities = BuildPositions(30, rng);
        var before = (Vec3[])positions.Clone();

        SimdBatchKernels.IntegrateVelocity(positions, velocities, 0f);

        positions.Should().Equal(before);
    }

    [Fact]
    public void IntegrateVelocity_ShorterVelocitySpan_Throws()
    {
        var rng = new SeededRng(13);
        var positions = BuildPositions(8, rng);
        var velocities = BuildPositions(7, rng);

        var act = () => SimdBatchKernels.IntegrateVelocity(positions, velocities, 0.5f);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IntegrateVelocity_AcrossManySteps_StaysFinite()
    {
        var positions = new[] { Vec3.Zero };
        var velocities = new[] { new Vec3(1f, -2f, 3f) };

        for (var step = 0; step < 1000; step++)
        {
            SimdBatchKernels.IntegrateVelocity(positions, velocities, 0.016f);
        }

        positions[0].X.Should().BeApproximately(16f, 1e-2f);
        positions[0].Y.Should().BeApproximately(-32f, 1e-2f);
        positions[0].Z.Should().BeApproximately(48f, 1e-2f);
    }
}
