
namespace Axrone.Batching.Tests;

/// <summary>
/// Parity coverage for the geometry kernels. Every case compares the vectorised path against
/// <see cref="Vector3.Transform(Vector3, Matrix4x4)"/> / <see cref="Vector4.Transform(Vector4, Matrix4x4)"/>
/// at sizes that straddle the vector width, so a mistake in the tail cannot hide behind a SIMD
/// block that happens to be right.
/// </summary>
public class SimdBatchKernelTests
{
    private const float Tolerance = 1e-4f;

    private static Matrix4x4 BuildMatrix(SeededRng rng) => new(
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f);

    private static Vector3[] BuildPositions(int count, SeededRng rng)
    {
        var data = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Vector3(rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f);
        }

        return data;
    }

    private static Vector4[] BuildVectors(int count, SeededRng rng)
    {
        var data = new Vector4[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Vector4(rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f,
                                  rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f);
        }

        return data;
    }

    private static void AssertClose(Vector3 actual, Vector3 expected, string because)
    {
        actual.X.Should().BeApproximately(expected.X, Tolerance, because);
        actual.Y.Should().BeApproximately(expected.Y, Tolerance, because);
        actual.Z.Should().BeApproximately(expected.Z, Tolerance, because);
    }

    private static void AssertClose(Vector4 actual, Vector4 expected, string because)
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
    public void TransformPositions3D_MatchesBclTransform(int count)
    {
        var rng = new SeededRng((ulong)count + 101);
        var source = BuildPositions(count, rng);
        var destination = new Vector3[count];
        var matrix = BuildMatrix(rng);

        SimdBatchKernels.TransformPositions3D(source, destination, matrix);

        for (var i = 0; i < count; i++)
        {
            AssertClose(destination[i], Vector3.Transform(source[i], matrix), $"index {i}, count {count}");
        }
    }

    [Fact]
    public void TransformPositions3D_InPlace_OverwritesSourceCorrectly()
    {
        var rng = new SeededRng(404);
        var data = BuildPositions(20, rng);
        var matrix = BuildMatrix(rng);
        var expected = new Vector3[20];
        for (var i = 0; i < 20; i++)
        {
            expected[i] = Vector3.Transform(data[i], matrix);
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
        var destination = new Vector3[12];

        SimdBatchKernels.TransformPositions3D(source, destination, Matrix4x4.Identity);

        destination.Should().Equal(source);
    }

    [Fact]
    public void TransformPositions3D_ShortDestination_Throws()
    {
        var source = BuildPositions(9, new SeededRng(1));
        var destination = new Vector3[8];

        var act = () => SimdBatchKernels.TransformPositions3D(source, destination, Matrix4x4.Identity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TransformPositions3D_TranslationOnlyIgnoresPerspectiveRow()
    {
        // Positions are directional-free by contract: M41..M43 translate, M14..M34 must not scale them.
        var matrix = Matrix4x4.CreateTranslation(3f, 4f, 5f);
        var source = BuildPositions(10, new SeededRng(2));
        var destination = new Vector3[10];

        SimdBatchKernels.TransformPositions3D(source, destination, matrix);

        AssertClose(destination[0], source[0] + new Vector3(3f, 4f, 5f), "translation");
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
    public void TransformAffine_MatchesBclTransform(int count)
    {
        var rng = new SeededRng((ulong)count + 211);
        var source = BuildVectors(count, rng);
        var destination = new Vector4[count];
        var matrix = BuildMatrix(rng);

        SimdBatchKernels.TransformAffine(source, destination, matrix);

        for (var i = 0; i < count; i++)
        {
            AssertClose(destination[i], Vector4.Transform(source[i], matrix), $"index {i}, count {count}");
        }
    }

    [Fact]
    public void TransformAffine_HonoursPerspectiveRow()
    {
        // Unlike positions, the W component must mix in row 4 — this is what separates the two kernels.
        var matrix = new Matrix4x4(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            2f, 3f, 4f, 5f);
        var source = new[] { new Vector4(1f, 1f, 1f, 1f), new Vector4(2f, 0f, 0f, 3f) };
        var destination = new Vector4[2];

        SimdBatchKernels.TransformAffine(source, destination, matrix);

        AssertClose(destination[0], Vector4.Transform(source[0], matrix), "w mixing");
        AssertClose(destination[1], Vector4.Transform(source[1], matrix), "w mixing");
        destination[1].W.Should().BeApproximately(3f * 5f, Tolerance, "M14..M34 are zero, so only v.W feeds W");
    }

    [Fact]
    public void TransformAffine_ShortDestination_Throws()
    {
        var source = BuildVectors(4, new SeededRng(3));
        var destination = new Vector4[3];

        var act = () => SimdBatchKernels.TransformAffine(source, destination, Matrix4x4.Identity);

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
        var expected = new Vector3[count];
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
        var before = (Vector3[])positions.Clone();

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
        var positions = new[] { Vector3.Zero };
        var velocities = new[] { new Vector3(1f, -2f, 3f) };

        for (var step = 0; step < 1000; step++)
        {
            SimdBatchKernels.IntegrateVelocity(positions, velocities, 0.016f);
        }

        positions[0].X.Should().BeApproximately(16f, 1e-2f);
        positions[0].Y.Should().BeApproximately(-32f, 1e-2f);
        positions[0].Z.Should().BeApproximately(48f, 1e-2f);
    }
}
