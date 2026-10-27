using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainChunkMeshTests
{
    private static readonly float[] Positions = new float[TerrainChunkLayout.VertexCount * 3];
    private static readonly float[] Normals = new float[TerrainChunkLayout.VertexCount * 3];
    private static readonly float[] Uvs = new float[TerrainChunkLayout.VertexCount * 2];
    private static readonly uint[] Indices = new uint[TerrainChunkLayout.IndexCount];

    [Fact]
    public void FlatChunkMeshIsCenteredAndScaled()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new FlatSurface(0.5f), maxHeight: 10f);
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        bool built = engine.TryBuildChunkMesh(coord, Positions, Normals, Uvs, Indices);

        built.Should().BeTrue();
        Positions[0].Should().BeApproximately(-16f, 1e-5f);
        Positions[1].Should().BeApproximately(5f, 1e-5f);
        Positions[2].Should().BeApproximately(-16f, 1e-5f);

        int last = (TerrainChunkLayout.VertexCount - 1) * 3;
        Positions[last].Should().BeApproximately(16f, 1e-5f);
        Positions[last + 1].Should().BeApproximately(5f, 1e-5f);
        Positions[last + 2].Should().BeApproximately(16f, 1e-5f);
    }

    [Fact]
    public void FlatChunkNormalsPointUp()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        engine.TryBuildChunkMesh(coord, Positions, Normals, Uvs, Indices).Should().BeTrue();

        for (var vertex = 0; vertex < TerrainChunkLayout.VertexCount; vertex++)
        {
            Normals[vertex * 3].Should().BeApproximately(0f, 1e-6f);
            Normals[(vertex * 3) + 1].Should().BeApproximately(1f, 1e-6f);
            Normals[(vertex * 3) + 2].Should().BeApproximately(0f, 1e-6f);
        }
    }

    [Fact]
    public void UvsSpanTheChunk()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        engine.TryBuildChunkMesh(coord, Positions, Normals, Uvs, Indices).Should().BeTrue();

        Uvs[0].Should().Be(0f);
        Uvs[1].Should().Be(0f);
        int last = TerrainChunkLayout.VertexCount - 1;
        Uvs[last * 2].Should().Be(1f);
        Uvs[(last * 2) + 1].Should().Be(1f);
    }

    [Fact]
    public void IndicesFollowTheSharedWinding()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        engine.TryBuildChunkMesh(coord, Positions, Normals, Uvs, Indices).Should().BeTrue();

        Indices[0].Should().Be(0u);
        Indices[1].Should().Be(TerrainChunkLayout.VerticesPerAxis);
        Indices[2].Should().Be(1u);
        Indices[3].Should().Be(1u);
        Indices[4].Should().Be(TerrainChunkLayout.VerticesPerAxis);
        Indices[5].Should().Be(TerrainChunkLayout.VerticesPerAxis + 1u);
        Indices.Should().HaveCount(TerrainChunkLayout.IndexCount);
    }

    [Fact]
    public void MissingChunkReturnsFalse()
    {
        using TerrainChunkEngine engine = TestEngine.Create();

        engine.TryBuildChunkMesh(new TerrainChunkCoord(2, 2), Positions, Normals, Uvs, Indices)
            .Should().BeFalse();
    }

    [Fact]
    public void SmallBuffersAreRejected()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));

        engine.Invoking(e => e.TryBuildChunkMesh(
                new TerrainChunkCoord(0, 0),
                new float[4],
                Normals,
                Uvs,
                Indices))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void HeightsAndHeightfieldMatchTheChunk()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new FlatSurface(0.5f), maxHeight: 10f);
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        var heights = new float[TerrainChunkLayout.VertexCount];
        engine.TryCopyChunkHeights(coord, heights).Should().BeTrue();
        heights.Should().OnlyContain(value => MathF.Abs(value - 0.5f) < 1e-6f);

        TerrainHeightfieldSource source = engine.CreateHeightfieldSource(coord)!;
        source.Width.Should().Be(33);
        source.Depth.Should().Be(33);
        source.ScaleX.Should().Be(1f);
        source.ScaleY.Should().Be(10f);
        source.ScaleZ.Should().Be(1f);
        source.Heights[0].Should().BeApproximately(0.5f, 1e-6f);
    }

    [Fact]
    public void SplatAndDensityCopiesMatchTheBuffers()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        var splat = new byte[TerrainChunkLayout.SplatBytes];
        var density = new byte[TerrainChunkLayout.VertexCount];

        engine.TryCopyChunkSplat(coord, splat).Should().BeTrue();
        engine.TryCopyChunkFoliageDensity(coord, 0, density).Should().BeTrue();

        splat[0].Should().Be((byte)TerrainSplatMath.WeightTotal);
        splat[1].Should().Be((byte)0);
        density.Should().OnlyContain(value => value == 0);
    }
}
