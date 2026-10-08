namespace Axrone.Batching.Tests;

/// <summary>
/// Parity coverage for the vector kernels against the <see cref="Quat"/>, <see cref="Vec3"/>
/// and <see cref="Vec4"/> reference operations, straddling the 8-wide vector width.
/// </summary>
public class BatchVectorKernelTests
{
    private const float Tolerance = 1e-4f;

    private static Quat[] BuildQuats(int count, SeededRng rng)
    {
        var data = new Quat[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Quat(
                rng.Next(-40, 41) / 8f, rng.Next(-40, 41) / 8f,
                rng.Next(-40, 41) / 8f, rng.Next(-40, 41) / 8f);
        }

        return data;
    }

    private static Vec3[] BuildVec3(int count, SeededRng rng)
    {
        var data = new Vec3[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Vec3(rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f);
        }

        return data;
    }

    private static Vec4[] BuildVec4(int count, SeededRng rng)
    {
        var data = new Vec4[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Vec4(
                rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f,
                rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f);
        }

        return data;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(17)]
    [InlineData(64)]
    public void QuaternionMultiply_MatchesScalarIdentity(int count)
    {
        var left = BuildQuats(count, new SeededRng((ulong)count + 11));
        var right = BuildQuats(count, new SeededRng((ulong)count + 77));
        var destination = new Quat[count];

        SimdBatchKernels.QuaternionMultiply(left, right, destination);

        for (var i = 0; i < count; i++)
        {
            var expected = Quat.Multiply(left[i], right[i]);
            destination[i].X.Should().BeApproximately(expected.X, Tolerance, $"index {i}");
            destination[i].Y.Should().BeApproximately(expected.Y, Tolerance, $"index {i}");
            destination[i].Z.Should().BeApproximately(expected.Z, Tolerance, $"index {i}");
            destination[i].W.Should().BeApproximately(expected.W, Tolerance, $"index {i}");
        }
    }

    [Fact]
    public void QuaternionMultiply_Identity_IsCopy()
    {
        var source = BuildQuats(10, new SeededRng(5));
        var destination = new Quat[10];

        SimdBatchKernels.QuaternionMultiply(source, Enumerable.Repeat(Quat.Identity, 10).ToArray(), destination);

        destination.Should().Equal(source);
    }

    [Fact]
    public void QuaternionMultiply_ShortDestination_Throws()
    {
        var left = BuildQuats(4, new SeededRng(1));
        var right = BuildQuats(4, new SeededRng(2));

        var act = () => SimdBatchKernels.QuaternionMultiply(left, right, new Quat[3]);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(33)]
    public void BatchDot3_MatchesScalarIdentity(int count)
    {
        var left = BuildVec3(count, new SeededRng((ulong)count + 21));
        var right = BuildVec3(count, new SeededRng((ulong)count + 55));
        var destination = new float[count];

        SimdBatchKernels.BatchDot3(left, right, destination);

        for (var i = 0; i < count; i++)
        {
            destination[i].Should().BeApproximately(Vec3.Dot(left[i], right[i]), Tolerance, $"index {i}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(33)]
    public void BatchCross3_MatchesScalarIdentity(int count)
    {
        var left = BuildVec3(count, new SeededRng((ulong)count + 31));
        var right = BuildVec3(count, new SeededRng((ulong)count + 65));
        var destination = new Vec3[count];

        SimdBatchKernels.BatchCross3(left, right, destination);

        for (var i = 0; i < count; i++)
        {
            var expected = Vec3.Cross(left[i], right[i]);
            destination[i].X.Should().BeApproximately(expected.X, Tolerance, $"index {i}");
            destination[i].Y.Should().BeApproximately(expected.Y, Tolerance, $"index {i}");
            destination[i].Z.Should().BeApproximately(expected.Z, Tolerance, $"index {i}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(25)]
    public void Normalize4_MatchesScalarIdentity_ForNonZero(int count)
    {
        // Offset by 2 so no lane is near zero; zero handling has its own case below.
        var source = BuildVec4(count, new SeededRng((ulong)count + 41))
            .Select(v => v + new Vec4(2f, 2f, 2f, 2f)).ToArray();
        var destination = new Vec4[count];

        SimdBatchKernels.Normalize4(source, destination);

        for (var i = 0; i < count; i++)
        {
            var expected = Vec4.Normalize(source[i]);
            destination[i].X.Should().BeApproximately(expected.X, Tolerance, $"index {i}");
            destination[i].Y.Should().BeApproximately(expected.Y, Tolerance, $"index {i}");
            destination[i].Z.Should().BeApproximately(expected.Z, Tolerance, $"index {i}");
            destination[i].W.Should().BeApproximately(expected.W, Tolerance, $"index {i}");
        }
    }

    [Fact]
    public void Normalize4_ZeroVector_WritesZeroInsteadOfNaN()
    {
        var source = new[] { Vec4.Zero, new Vec4(1f, 0f, 0f, 0f), Vec4.Zero };
        var destination = new Vec4[3];

        SimdBatchKernels.Normalize4(source, destination);

        destination[0].Should().Be(Vec4.Zero);
        destination[1].Should().Be(new Vec4(1f, 0f, 0f, 0f));
        destination[2].Should().Be(Vec4.Zero);
        destination.Should().OnlyContain(v =>
            float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W));
    }

    [Fact]
    public void Normalize3_ZeroVector_WritesZeroInsteadOfNaN()
    {
        var source = new[] { Vec3.Zero, new Vec3(0f, 3f, 4f) };
        var destination = new Vec3[2];

        SimdBatchKernels.Normalize3(source, destination);

        destination[0].Should().Be(Vec3.Zero);
        destination[1].X.Should().BeApproximately(0f, Tolerance);
        destination[1].Y.Should().BeApproximately(0.6f, Tolerance);
        destination[1].Z.Should().BeApproximately(0.8f, Tolerance);
    }
}
