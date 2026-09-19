namespace Axrone.Batching.Tests;

/// <summary>
/// Parity coverage for the vector kernels against <see cref="Quaternion"/>, <see cref="Vector3"/>
/// and <see cref="Vector4"/> BCL operations, straddling the 8-wide vector width.
/// </summary>
public class BatchVectorKernelTests
{
    private const float Tolerance = 1e-4f;

    private static Quaternion[] BuildQuats(int count, SeededRng rng)
    {
        var data = new Quaternion[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Quaternion(
                rng.Next(-40, 41) / 8f, rng.Next(-40, 41) / 8f,
                rng.Next(-40, 41) / 8f, rng.Next(-40, 41) / 8f);
        }

        return data;
    }

    private static Vector3[] BuildVec3(int count, SeededRng rng)
    {
        var data = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Vector3(rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f);
        }

        return data;
    }

    private static Vector4[] BuildVec4(int count, SeededRng rng)
    {
        var data = new Vector4[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Vector4(
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
    public void QuaternionMultiply_MatchesBcl(int count)
    {
        var left = BuildQuats(count, new SeededRng((ulong)count + 11));
        var right = BuildQuats(count, new SeededRng((ulong)count + 77));
        var destination = new Quaternion[count];

        SimdBatchKernels.QuaternionMultiply(left, right, destination);

        for (var i = 0; i < count; i++)
        {
            var expected = Quaternion.Multiply(left[i], right[i]);
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
        var destination = new Quaternion[10];

        SimdBatchKernels.QuaternionMultiply(source, Enumerable.Repeat(Quaternion.Identity, 10).ToArray(), destination);

        destination.Should().Equal(source);
    }

    [Fact]
    public void QuaternionMultiply_ShortDestination_Throws()
    {
        var left = BuildQuats(4, new SeededRng(1));
        var right = BuildQuats(4, new SeededRng(2));

        var act = () => SimdBatchKernels.QuaternionMultiply(left, right, new Quaternion[3]);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(33)]
    public void BatchDot3_MatchesBcl(int count)
    {
        var left = BuildVec3(count, new SeededRng((ulong)count + 21));
        var right = BuildVec3(count, new SeededRng((ulong)count + 55));
        var destination = new float[count];

        SimdBatchKernels.BatchDot3(left, right, destination);

        for (var i = 0; i < count; i++)
        {
            destination[i].Should().BeApproximately(Vector3.Dot(left[i], right[i]), Tolerance, $"index {i}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(33)]
    public void BatchCross3_MatchesBcl(int count)
    {
        var left = BuildVec3(count, new SeededRng((ulong)count + 31));
        var right = BuildVec3(count, new SeededRng((ulong)count + 65));
        var destination = new Vector3[count];

        SimdBatchKernels.BatchCross3(left, right, destination);

        for (var i = 0; i < count; i++)
        {
            var expected = Vector3.Cross(left[i], right[i]);
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
    public void Normalize4_MatchesBcl_ForNonZero(int count)
    {
        // Offset by 2 so no lane is near zero; zero handling has its own case below.
        var source = BuildVec4(count, new SeededRng((ulong)count + 41))
            .Select(v => v + new Vector4(2f, 2f, 2f, 2f)).ToArray();
        var destination = new Vector4[count];

        SimdBatchKernels.Normalize4(source, destination);

        for (var i = 0; i < count; i++)
        {
            var expected = Vector4.Normalize(source[i]);
            destination[i].X.Should().BeApproximately(expected.X, Tolerance, $"index {i}");
            destination[i].Y.Should().BeApproximately(expected.Y, Tolerance, $"index {i}");
            destination[i].Z.Should().BeApproximately(expected.Z, Tolerance, $"index {i}");
            destination[i].W.Should().BeApproximately(expected.W, Tolerance, $"index {i}");
        }
    }

    [Fact]
    public void Normalize4_ZeroVector_WritesZeroInsteadOfNaN()
    {
        var source = new[] { Vector4.Zero, new Vector4(1f, 0f, 0f, 0f), Vector4.Zero };
        var destination = new Vector4[3];

        SimdBatchKernels.Normalize4(source, destination);

        destination[0].Should().Be(Vector4.Zero);
        destination[1].Should().Be(new Vector4(1f, 0f, 0f, 0f));
        destination[2].Should().Be(Vector4.Zero);
        destination.Should().OnlyContain(v =>
            float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W));
    }

    [Fact]
    public void Normalize3_ZeroVector_WritesZeroInsteadOfNaN()
    {
        var source = new[] { Vector3.Zero, new Vector3(0f, 3f, 4f) };
        var destination = new Vector3[2];

        SimdBatchKernels.Normalize3(source, destination);

        destination[0].Should().Be(Vector3.Zero);
        destination[1].X.Should().BeApproximately(0f, Tolerance);
        destination[1].Y.Should().BeApproximately(0.6f, Tolerance);
        destination[1].Z.Should().BeApproximately(0.8f, Tolerance);
    }
}
