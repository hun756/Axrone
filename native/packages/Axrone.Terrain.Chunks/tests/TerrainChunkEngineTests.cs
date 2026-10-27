using Axrone.Numeric;
using Axrone.Terrain;
using Axrone.Terrain.Chunks;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Chunks.Tests;

public sealed class TerrainChunkEngineTests
{
    [Fact]
    public void LayoutIsCacheLineAligned()
    {
        (TerrainChunkLayout.SlotBytes % 64).Should().Be(0);
        (TerrainChunkLayout.SlotBlocks * 64).Should().Be(TerrainChunkLayout.SlotBytes);
        TerrainChunkLayout.HeightsOffset.Should().Be(TerrainChunkLayout.HeaderBytes);
        TerrainChunkLayout.NormalsOffset.Should().BeGreaterThan(TerrainChunkLayout.HeightsOffset);
        TerrainChunkLayout.SplatOffset.Should().BeGreaterThan(TerrainChunkLayout.NormalsOffset);
        TerrainChunkLayout.DensityOffset.Should().BeGreaterThan(TerrainChunkLayout.SplatOffset);
    }

    [Fact]
    public void CommitAndDecommitTrackResidency()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);

        engine.IsChunkResident(coord).Should().BeFalse();
        engine.CommitChunk(coord).Should().BeTrue();
        engine.IsChunkResident(coord).Should().BeTrue();
        engine.ChunkCount.Should().Be(1);
        engine.CommitChunk(coord).Should().BeFalse();

        engine.DecommitChunk(coord).Should().BeTrue();
        engine.DecommitChunk(coord).Should().BeFalse();
        engine.ChunkCount.Should().Be(0);
    }

    [Fact]
    public void ChunkInfoReportsExtentsAndState()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new FlatSurface(0.25f));
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        engine.TryGetChunkInfo(coord, out TerrainChunkInfo info).Should().BeTrue();

        info.Coord.Should().Be(coord);
        info.MinHeight.Should().BeApproximately(0.25f, 1e-6f);
        info.MaxHeight.Should().BeApproximately(0.25f, 1e-6f);
        info.State.Should().Be(TerrainChunkState.Ready);
        info.Generation.Should().BeGreaterThan(0u);
    }

    [Fact]
    public void ElevationUsesWorldScaleForResidentChunks()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new FlatSurface(0.5f), maxHeight: 10f);
        engine.CommitChunk(new TerrainChunkCoord(0, 0));

        engine.SampleElevation(16f, 16f).Should().BeApproximately(5f, 1e-5f);
        engine.TryGetChunkElevation(16f, 16f, out float height).Should().BeTrue();
        height.Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void MissingChunksFallBackToTheProceduralSurface()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new FlatSurface(0.5f));

        engine.TryGetChunkElevation(16f, 16f, out _).Should().BeFalse();
        engine.SampleElevation(16f, 16f).Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void BilinearSamplingMatchesAGridRamp()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new RampSurface(0.01f), maxHeight: 1f);
        engine.CommitChunk(new TerrainChunkCoord(0, 0));

        engine.TryGetChunkElevation(0f, 0f, out float atZero).Should().BeTrue();
        engine.TryGetChunkElevation(16f, 0f, out float atMiddle).Should().BeTrue();
        engine.TryGetChunkElevation(0.5f, 0f, out float between).Should().BeTrue();

        atZero.Should().BeApproximately(0f, 1e-6f);
        atMiddle.Should().BeApproximately(0.16f, 1e-5f);
        between.Should().BeApproximately(0.005f, 1e-5f);
    }

    [Fact]
    public void ChunkBoundariesAreContinuous()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        engine.CommitChunk(new TerrainChunkCoord(1, 0));

        float left = engine.SampleElevation(31.9f, 16f);
        float right = engine.SampleElevation(32.1f, 16f);

        left.Should().BeApproximately(5f, 1e-5f);
        right.Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void FlatTerrainProducesUpwardNormals()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));

        engine.TryGetNormal(16f, 16f, out Vec3 normal).Should().BeTrue();

        normal.X.Should().BeApproximately(0f, 1e-6f);
        normal.Y.Should().BeApproximately(1f, 1e-6f);
        normal.Z.Should().BeApproximately(0f, 1e-6f);
    }

    [Fact]
    public void MissingChunkNormalsFallBackToTheSurface()
    {
        using TerrainChunkEngine engine = TestEngine.Create();

        engine.TryGetNormal(16f, 16f, out Vec3 normal).Should().BeFalse();

        normal.Y.Should().BeApproximately(1f, 1e-6f);
    }

    [Fact]
    public void SplatWeightsDefaultToTheFirstLayer()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));

        engine.TryGetSplatWeights(16f, 16f, out SplatWeights weights).Should().BeTrue();

        weights.R.Should().BeApproximately(1f, 1e-6f);
        weights.G.Should().BeApproximately(0f, 1e-6f);
        weights.Sum.Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void StatisticsMatchTheChunkContents()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new FlatSurface(0.25f));
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        engine.TryGetChunkStatistics(coord, out TerrainStatistics statistics).Should().BeTrue();

        statistics.Min.Should().BeApproximately(0.25f, 1e-6f);
        statistics.Max.Should().BeApproximately(0.25f, 1e-6f);
        statistics.Mean.Should().BeApproximately(0.25f, 1e-6f);
    }

    [Fact]
    public void PoolExhaustionReturnsFalseUntilSlotIsReleased()
    {
        using TerrainChunkEngine engine = TestEngine.Create(poolCapacity: 1);

        engine.CommitChunk(new TerrainChunkCoord(0, 0)).Should().BeTrue();
        engine.CommitChunk(new TerrainChunkCoord(1, 0)).Should().BeFalse();
        engine.DecommitChunk(new TerrainChunkCoord(0, 0)).Should().BeTrue();
        engine.CommitChunk(new TerrainChunkCoord(1, 0)).Should().BeTrue();
    }

    [Fact]
    public void DisposeReleasesChunksAndRejectsFurtherUse()
    {
        TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        engine.Dispose();
        engine.Dispose();

        engine.Invoking(e => e.CommitChunk(new TerrainChunkCoord(1, 0)))
            .Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ParallelSamplingIsSafe()
    {
        using TerrainChunkEngine engine = TestEngine.Create(poolCapacity: 32);
        for (var z = -1; z <= 1; z++)
        {
            for (var x = -1; x <= 1; x++)
            {
                engine.CommitChunk(new TerrainChunkCoord(x, z));
            }
        }

        Parallel.For(0, 10_000, index =>
        {
            float x = (index % 96) - 48;
            float z = ((index / 96) % 96) - 48;
            float height = engine.SampleElevation(x, z);
            if (height < 4.99f || height > 5.01f)
            {
                throw new InvalidOperationException($"Unexpected height {height}.");
            }
        });
    }

    [Fact]
    public void SamplingDoesNotAllocateInSteadyState()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        engine.SampleElevation(16f, 16f);
        engine.TryGetNormal(16f, 16f, out _);

        long before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0f;
        for (var index = 0; index < 1_000; index++)
        {
            sum += engine.SampleElevation((index % 32) + 0.5f, 16.5f);
            engine.TryGetNormal(16.5f, 16.5f, out _);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();

        sum.Should().BeGreaterThan(0f);
        (after - before).Should().Be(0);
    }
}
