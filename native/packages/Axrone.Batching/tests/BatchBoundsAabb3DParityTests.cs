namespace Axrone.Batching.Tests;

using Axrone.Geometry;
using Axrone.Numeric;

#pragma warning disable CS0618 // Legacy Aabb overload under test for kernel parity; removal is tracked separately.

/// <summary>
/// Parity coverage for the canonical <see cref="Aabb3D"/> bound kernel against
/// the legacy <c>Aabb</c> kernel and the scalar center/extent identity.
/// </summary>
public class BatchBoundsAabb3DParityTests
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

    private static Matrix4x4 BuildMatrix(SeededRng rng) => new(
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 0f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 0f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 0f,
        rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, rng.Next(-8, 9) / 4f, 1f);

    private static Aabb ToLegacy(in Aabb3D box) =>
        new(box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(17)]
    [InlineData(65)]
    public void TransformAabb_MatchesLegacyKernel(int count)
    {
        var rng = new SeededRng((ulong)count + 701);
        var source = BuildBoxes(count, rng);
        var matrix = BuildMatrix(rng);

        var legacySource = new Aabb[count];
        for (var i = 0; i < count; i++)
        {
            legacySource[i] = ToLegacy(in source[i]);
        }

        var expected = new Aabb[count];
        SimdBatchKernels.TransformAabb(legacySource, expected, in matrix);

        var actual = new Aabb3D[count];
        SimdBatchKernels.TransformAabb(source, actual, in matrix);

        for (var i = 0; i < count; i++)
        {
            actual[i].Min.X.Should().BeApproximately(expected[i].MinX, Tolerance);
            actual[i].Min.Y.Should().BeApproximately(expected[i].MinY, Tolerance);
            actual[i].Min.Z.Should().BeApproximately(expected[i].MinZ, Tolerance);
            actual[i].Max.X.Should().BeApproximately(expected[i].MaxX, Tolerance);
            actual[i].Max.Y.Should().BeApproximately(expected[i].MaxY, Tolerance);
            actual[i].Max.Z.Should().BeApproximately(expected[i].MaxZ, Tolerance);
        }
    }

    [Fact]
    public void TransformAabb_ShortDestination_Throws()
    {
        var source = BuildBoxes(4, new SeededRng(702));
        var destination = new Aabb3D[3];
        var matrix = Matrix4x4.Identity;

        Action act = () => SimdBatchKernels.TransformAabb(source, destination, in matrix);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TransformAabb_Identity_PreservesBoxes()
    {
        var source = BuildBoxes(10, new SeededRng(703));
        var destination = new Aabb3D[source.Length];
        var matrix = Matrix4x4.Identity;

        SimdBatchKernels.TransformAabb(source, destination, in matrix);

        destination.Should().Equal(source);
    }
}
