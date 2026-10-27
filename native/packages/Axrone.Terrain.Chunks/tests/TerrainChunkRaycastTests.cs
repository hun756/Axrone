using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Terrain.Chunks;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Chunks.Tests;

public sealed class TerrainChunkRaycastTests
{
    [Fact]
    public void StraightDownRayHitsTheFlatChunk()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new FlatSurface(0.5f), maxHeight: 10f);
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        var ray = new Ray3D(new Vec3(16f, 50f, 16f), new Vec3(0f, -1f, 0f));

        bool hit = engine.Raycast(in ray, 100f, out TerrainChunkHit result);

        hit.Should().BeTrue();
        result.Coord.Should().Be(new TerrainChunkCoord(0, 0));
        result.Point.Y.Should().BeApproximately(5f, 1e-4f);
        result.U.Should().BeApproximately(0.5f, 1e-3f);
        result.V.Should().BeApproximately(0.5f, 1e-3f);
        result.Distance.Should().BeApproximately(45f, 1e-3f);
    }

    [Fact]
    public void ResidentChunkInTheNextTileIsHit()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(1, 0));
        var ray = new Ray3D(new Vec3(40f, 50f, 16f), new Vec3(0f, -1f, 0f));

        engine.Raycast(in ray, 100f, out TerrainChunkHit result).Should().BeTrue();

        result.Coord.Should().Be(new TerrainChunkCoord(1, 0));
        result.U.Should().BeApproximately(0.25f, 1e-3f);
    }

    [Fact]
    public void MissingChunksAreNotHit()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var ray = new Ray3D(new Vec3(40f, 50f, 16f), new Vec3(0f, -1f, 0f));

        engine.Raycast(in ray, 100f, out _).Should().BeFalse();
    }

    [Fact]
    public void MaxDistanceLimitsTheQuery()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        var ray = new Ray3D(new Vec3(16f, 50f, 16f), new Vec3(0f, -1f, 0f));

        engine.Raycast(in ray, 40f, out _).Should().BeFalse();
        engine.Raycast(in ray, 50f, out _).Should().BeTrue();
        engine.Raycast(in ray, 0f, out _).Should().BeFalse();
    }

    [Fact]
    public void MissAboveTheTerrainReturnsFalse()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        var ray = new Ray3D(new Vec3(16f, 50f, 16f), new Vec3(0f, 1f, 0f));

        engine.Raycast(in ray, 100f, out _).Should().BeFalse();
    }

    [Fact]
    public void ChunkSeamSelectsTheContainingTile()
    {
        using TerrainChunkEngine engine = TestEngine.Create(poolCapacity: 4);
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        engine.CommitChunk(new TerrainChunkCoord(1, 0));

        var left = new Ray3D(new Vec3(31.9f, 50f, 16f), new Vec3(0f, -1f, 0f));
        var right = new Ray3D(new Vec3(32.1f, 50f, 16f), new Vec3(0f, -1f, 0f));

        engine.Raycast(in left, 100f, out TerrainChunkHit leftHit).Should().BeTrue();
        engine.Raycast(in right, 100f, out TerrainChunkHit rightHit).Should().BeTrue();

        leftHit.Coord.Should().Be(new TerrainChunkCoord(0, 0));
        rightHit.Coord.Should().Be(new TerrainChunkCoord(1, 0));
    }

    [Fact]
    public void ObliqueRayHitsAcrossChunks()
    {
        using TerrainChunkEngine engine = TestEngine.Create(poolCapacity: 8);
        for (var x = 0; x <= 2; x++)
        {
            engine.CommitChunk(new TerrainChunkCoord(x, 0));
        }

        var ray = new Ray3D(new Vec3(-10.5f, 50f, 15.5f), new Vec3(1f, -1f, 0f));

        engine.Raycast(in ray, 200f, out TerrainChunkHit result).Should().BeTrue();
        result.Point.Y.Should().BeApproximately(5f, 1e-3f);
        result.Distance.Should().BeApproximately(45f * MathF.Sqrt(2f), 0.2f);
    }

    [Fact]
    public void SlopedSurfaceReturnsATiltedNormal()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new RampSurface(0.02f), maxHeight: 10f);
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        var ray = new Ray3D(new Vec3(10f, 50f, 16f), new Vec3(0f, -1f, 0f));

        engine.Raycast(in ray, 100f, out TerrainChunkHit result).Should().BeTrue();

        result.Normal.X.Should().BeLessThan(0f);
        result.Normal.Y.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void RaycastAfterDisposeThrows()
    {
        TerrainChunkEngine engine = TestEngine.Create();
        engine.Dispose();
        var ray = new Ray3D(new Vec3(16f, 50f, 16f), new Vec3(0f, -1f, 0f));

        engine.Invoking(e => e.Raycast(in ray, 100f, out _))
            .Should().Throw<ObjectDisposedException>();
    }
}
