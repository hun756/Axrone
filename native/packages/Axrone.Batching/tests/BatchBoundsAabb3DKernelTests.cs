namespace Axrone.Batching.Tests;

using Axrone.Geometry;

/// <summary>
/// Coverage for the canonical <see cref="Aabb3D"/> bound kernel against the
/// scalar center/extent identity.
/// </summary>
public class BatchBoundsAabb3DKernelTests
{
    private const float Tolerance = 1e-3f;

    private static Aabb3D[] BuildBoxes(int count, SeededRng rng)
    {
        var data = new Aabb3D[count];
        for (var i = 0; i < count; i++)
        {
            var minX = rng.Next(-40, 41) / 4f;
            var minY = rng.Next(-40, 41) / 4f;
            var minZ = rng.Next(-40, 41) / 4f;
            data[i] = new Aabb3D(
                minX, minY, minZ,
                minX + rng.Next(0, 41) / 4f, minY + rng.Next(0, 41) / 4f, minZ + rng.Next(0, 41) / 4f);
        }

        return data;
    }

    private static Mat4 BuildMatrix(SeededRng rng) => new(
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 0f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 0f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 0f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 1f);

    private static Aabb3D TransformScalar(in Aabb3D box, in Mat4 matrix)
    {
        Vec3 boxCenter = box.Center;
        var center = Vec3.Transform(boxCenter, matrix);
        Vec3 boxExtent = box.Extents;
        var extent = new Vec3(
            (MathF.Abs(matrix.M11) * boxExtent.X) + (MathF.Abs(matrix.M21) * boxExtent.Y) + (MathF.Abs(matrix.M31) * boxExtent.Z),
            (MathF.Abs(matrix.M12) * boxExtent.X) + (MathF.Abs(matrix.M22) * boxExtent.Y) + (MathF.Abs(matrix.M32) * boxExtent.Z),
            (MathF.Abs(matrix.M13) * boxExtent.X) + (MathF.Abs(matrix.M23) * boxExtent.Y) + (MathF.Abs(matrix.M33) * boxExtent.Z));
        return Aabb3D.FromCenterExtents(center, extent);
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
        var rng = new SeededRng((ulong)count + 701);
        var source = BuildBoxes(count, rng);
        var matrix = BuildMatrix(rng);
        var destination = new Aabb3D[count];

        SimdBatchKernels.TransformAabb(source, destination, in matrix);

        for (var i = 0; i < count; i++)
        {
            Aabb3D expected = TransformScalar(in source[i], in matrix);
            destination[i].Min.X.Should().BeApproximately(expected.Min.X, Tolerance);
            destination[i].Min.Y.Should().BeApproximately(expected.Min.Y, Tolerance);
            destination[i].Min.Z.Should().BeApproximately(expected.Min.Z, Tolerance);
            destination[i].Max.X.Should().BeApproximately(expected.Max.X, Tolerance);
            destination[i].Max.Y.Should().BeApproximately(expected.Max.Y, Tolerance);
            destination[i].Max.Z.Should().BeApproximately(expected.Max.Z, Tolerance);
        }
    }

    [Fact]
    public void TransformAabb_ShortDestination_Throws()
    {
        var source = BuildBoxes(4, new SeededRng(702));
        var destination = new Aabb3D[3];
        var matrix = Mat4.Identity;

        Action act = () => SimdBatchKernels.TransformAabb(source, destination, in matrix);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TransformAabb_Identity_PreservesBoxes()
    {
        var source = BuildBoxes(10, new SeededRng(703));
        var destination = new Aabb3D[source.Length];
        var matrix = Mat4.Identity;

        SimdBatchKernels.TransformAabb(source, destination, in matrix);

        destination.Should().Equal(source);
    }
}
