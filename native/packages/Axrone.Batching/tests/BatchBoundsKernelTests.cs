namespace Axrone.Batching.Tests;

/// <summary>
/// Parity coverage for the bound kernels against the scalar center/extent identity.
/// </summary>
public class BatchBoundsKernelTests
{
    private const float Tolerance = 1e-3f;

    private static Aabb[] BuildBoxes(int count, SeededRng rng)
    {
        var data = new Aabb[count];
        for (var i = 0; i < count; i++)
        {
            var minX = rng.Next(-40, 41) / 4f;
            var minY = rng.Next(-40, 41) / 4f;
            var minZ = rng.Next(-40, 41) / 4f;
            data[i] = new Aabb(
                minX, minY, minZ,
                minX + rng.Next(0, 41) / 4f, minY + rng.Next(0, 41) / 4f, minZ + rng.Next(0, 41) / 4f);
        }

        return data;
    }

    private static Matrix4x4 BuildMatrix(SeededRng rng) => new(
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 0f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 0f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 0f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 1f);

    private static Aabb TransformScalar(Aabb box, in Matrix4x4 matrix)
    {
        var center = Vector3.Transform(box.Center, matrix);
        var extent = box.Extent;
        var transformed = new Vector3(
            (MathF.Abs(matrix.M11) * extent.X) + (MathF.Abs(matrix.M21) * extent.Y) + (MathF.Abs(matrix.M31) * extent.Z),
            (MathF.Abs(matrix.M12) * extent.X) + (MathF.Abs(matrix.M22) * extent.Y) + (MathF.Abs(matrix.M32) * extent.Z),
            (MathF.Abs(matrix.M13) * extent.X) + (MathF.Abs(matrix.M23) * extent.Y) + (MathF.Abs(matrix.M33) * extent.Z));
        return Aabb.FromCenterExtent(center, transformed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(17)]
    [InlineData(65)]
    public void TransformAabb_MatchesScalarIdentity(int count)
    {
        var rng = new SeededRng((ulong)count + 501);
        var source = BuildBoxes(count, rng);
        var destination = new Aabb[count];
        var matrix = BuildMatrix(rng);

        SimdBatchKernels.TransformAabb(source, destination, matrix);

        for (var i = 0; i < count; i++)
        {
            var expected = TransformScalar(source[i], matrix);
            destination[i].MinX.Should().BeApproximately(expected.MinX, Tolerance, $"index {i}");
            destination[i].MinY.Should().BeApproximately(expected.MinY, Tolerance, $"index {i}");
            destination[i].MinZ.Should().BeApproximately(expected.MinZ, Tolerance, $"index {i}");
            destination[i].MaxX.Should().BeApproximately(expected.MaxX, Tolerance, $"index {i}");
            destination[i].MaxY.Should().BeApproximately(expected.MaxY, Tolerance, $"index {i}");
            destination[i].MaxZ.Should().BeApproximately(expected.MaxZ, Tolerance, $"index {i}");
        }
    }

    [Fact]
    public void TransformAabb_TranslationOnly_ShiftsBothCorners()
    {
        var source = new[] { new Aabb(0f, 0f, 0f, 1f, 2f, 3f) };
        var destination = new Aabb[1];

        SimdBatchKernels.TransformAabb(source, destination, Matrix4x4.CreateTranslation(10f, 20f, 30f));

        destination[0].Should().Be(new Aabb(10f, 20f, 30f, 11f, 22f, 33f));
    }

    [Fact]
    public void TransformAabb_Identity_IsCopy()
    {
        var source = BuildBoxes(12, new SeededRng(9));
        var destination = new Aabb[12];

        SimdBatchKernels.TransformAabb(source, destination, Matrix4x4.Identity);

        destination.Should().Equal(source);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    [InlineData(33)]
    public void TransformBoundingSpheres_MatchesScalar(int count)
    {
        var rng = new SeededRng((ulong)count + 601);
        var source = new Vector4[count];
        for (var i = 0; i < count; i++)
        {
            source[i] = new Vector4(rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f, rng.Next(-40, 41) / 4f, rng.Next(0, 41) / 4f);
        }

        var destination = new Vector4[count];
        var matrix = BuildMatrix(rng);
        const float maxScale = 1.5f;

        SimdBatchKernels.TransformBoundingSpheres(source, destination, matrix, maxScale);

        for (var i = 0; i < count; i++)
        {
            var center = Vector3.Transform(new Vector3(source[i].X, source[i].Y, source[i].Z), matrix);
            destination[i].X.Should().BeApproximately(center.X, Tolerance, $"index {i}");
            destination[i].Y.Should().BeApproximately(center.Y, Tolerance, $"index {i}");
            destination[i].Z.Should().BeApproximately(center.Z, Tolerance, $"index {i}");
            destination[i].W.Should().BeApproximately(source[i].W * maxScale, Tolerance, $"index {i}");
        }
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void TransformBoundingSpheres_BadScale_Throws(float maxScale)
    {
        var source = new[] { new Vector4(0f, 0f, 0f, 1f) };

        var act = () => SimdBatchKernels.TransformBoundingSpheres(source, new Vector4[1], Matrix4x4.Identity, maxScale);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
