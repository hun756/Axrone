using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainChunkLodTests
{
    [Fact]
    public void LodCountsAreMonotonicAndIncludeSkirts()
    {
        TerrainChunkLod.VertexCount(0).Should().BeGreaterThan(TerrainChunkLayout.VertexCount);
        TerrainChunkLod.VertexCount(1).Should().BeLessThan(TerrainChunkLod.VertexCount(0));
        TerrainChunkLod.VertexCount(2).Should().BeLessThan(TerrainChunkLod.VertexCount(1));
        TerrainChunkLod.IndexCount(0).Should().Be(
            TerrainChunkLod.GridIndexCount(0) + TerrainChunkLod.SkirtIndexCount(0));
    }

    [Fact]
    public void LodMeshUsesChunkLocalPositionsAndSkirtDepth()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new FlatSurface(0.5f), maxHeight: 10f);
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        TerrainVertexMesh lod0 = engine.BuildChunkLodMesh(coord, 0)!;
        TerrainVertexMesh lod2 = engine.BuildChunkLodMesh(coord, 2)!;

        lod0.VertexCount.Should().Be(TerrainChunkLod.VertexCount(0));
        lod0.TriangleCount.Should().Be(TerrainChunkLod.IndexCount(0) / 3);
        lod2.VertexCount.Should().Be(TerrainChunkLod.VertexCount(2));

        lod0.Vertices[0].Position.X.Should().BeApproximately(-16f, 1e-5f);
        lod0.Vertices[0].Position.Y.Should().BeApproximately(5f, 1e-5f);
        int skirtStart = TerrainChunkLod.GridVertexCount(0);
        lod0.Vertices[skirtStart].Position.Y.Should().BeLessThan(lod0.Vertices[0].Position.Y);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void InvalidLodIsRejected(int level)
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        engine.Invoking(e => e.BuildChunkLodMesh(coord, level))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void MissingChunkReturnsNullLodMesh()
    {
        using TerrainChunkEngine engine = TestEngine.Create();

        engine.BuildChunkLodMesh(new TerrainChunkCoord(4, 4), 1).Should().BeNull();
    }
}
