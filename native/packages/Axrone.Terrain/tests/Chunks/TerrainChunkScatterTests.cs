using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainChunkScatterTests
{
    private static void PaintDensity(TerrainChunkEngine engine, int layerIndex = 0)
    {
        var brush = new TerrainBrushOptions { Radius = 100f, Strength = 1f };
        for (var stamp = 0; stamp < 12; stamp++)
        {
            engine.ApplyFoliageDensityBrush(16f, 16f, brush, layerIndex);
        }
    }

    [Fact]
    public void ScatterIsDeterministicForIdenticalSeeds()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        PaintDensity(engine);

        var first = new TerrainFoliageInstance[1_000];
        var second = new TerrainFoliageInstance[1_000];
        TerrainFoliageLayer layer = TestEngine.FoliageLayer();

        int firstCount = engine.ScatterChunkFoliage(coord, 0, layer, 42, first);
        int secondCount = engine.ScatterChunkFoliage(coord, 0, layer, 42, second);

        firstCount.Should().BeGreaterThan(0);
        secondCount.Should().Be(firstCount);
        second.AsSpan(0, firstCount).SequenceEqual(first.AsSpan(0, firstCount)).Should().BeTrue();
    }

    [Fact]
    public void ScatterChangesWithTheSeed()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        PaintDensity(engine);

        var first = new TerrainFoliageInstance[1_000];
        var second = new TerrainFoliageInstance[1_000];
        TerrainFoliageLayer layer = TestEngine.FoliageLayer();

        engine.ScatterChunkFoliage(coord, 0, layer, 42, first);
        engine.ScatterChunkFoliage(coord, 0, layer, 7331, second);

        first.AsSpan().SequenceEqual(second.AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void ScatterPrefixIsIndependentOfDestinationLength()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        PaintDensity(engine);
        TerrainFoliageLayer layer = TestEngine.FoliageLayer();

        var small = new TerrainFoliageInstance[10];
        var large = new TerrainFoliageInstance[1_000];

        int smallCount = engine.ScatterChunkFoliage(coord, 0, layer, 7, small);
        int largeCount = engine.ScatterChunkFoliage(coord, 0, layer, 7, large);

        smallCount.Should().Be(10);
        largeCount.Should().BeGreaterThanOrEqualTo(smallCount);
        large.AsSpan(0, smallCount).SequenceEqual(small.AsSpan(0, smallCount)).Should().BeTrue();
    }

    [Fact]
    public void UnpaintedChunksProduceNoInstances()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        var instances = new TerrainFoliageInstance[128];
        int count = engine.ScatterChunkFoliage(coord, 0, TestEngine.FoliageLayer(), 42, instances);

        count.Should().Be(0);
    }

    [Fact]
    public void ZeroLayerDensityProducesNoInstances()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        PaintDensity(engine);

        var instances = new TerrainFoliageInstance[128];
        int count = engine.ScatterChunkFoliage(coord, 0, TestEngine.FoliageLayer(density: 0f), 42, instances);

        count.Should().Be(0);
    }

    [Fact]
    public void SlopeFilterReducesInstances()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new RampSurface(0.5f), maxHeight: 10f);
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        PaintDensity(engine);

        var permissive = new TerrainFoliageInstance[1_000];
        var blocked = new TerrainFoliageInstance[1_000];

        int permissiveCount = engine.ScatterChunkFoliage(
            coord,
            0,
            TestEngine.FoliageLayer(maxSlopeDeg: 90f),
            42,
            permissive);
        int blockedCount = engine.ScatterChunkFoliage(
            coord,
            0,
            TestEngine.FoliageLayer(maxSlopeDeg: 5f),
            42,
            blocked);

        permissiveCount.Should().BeGreaterThan(0);
        blockedCount.Should().BeLessThan(permissiveCount);
    }

    [Fact]
    public void InstancesStayInsideTheChunkAndOnTheSurface()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        PaintDensity(engine);

        var instances = new TerrainFoliageInstance[1_000];
        int count = engine.ScatterChunkFoliage(coord, 0, TestEngine.FoliageLayer(minScale: 0.5f, maxScale: 2f), 99, instances);

        count.Should().BeGreaterThan(0);
        for (var index = 0; index < count; index++)
        {
            TerrainFoliageInstance instance = instances[index];
            instance.X.Should().BeInRange(0f, 32f);
            instance.Z.Should().BeInRange(0f, 32f);
            instance.Y.Should().BeApproximately(5f, 1e-4f);
            instance.Scale.Should().BeInRange(0.5f, 2f);
            instance.RotationY.Should().BeInRange(0f, MathF.PI * 2f);
        }
    }

    [Fact]
    public void ScatterRejectsInvalidLayerIndex()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        var instances = new TerrainFoliageInstance[8];

        engine.Invoking(e => e.ScatterChunkFoliage(coord, 4, TestEngine.FoliageLayer(), 1, instances))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void ScatterOnMissingChunkReturnsZero()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var instances = new TerrainFoliageInstance[8];

        engine.ScatterChunkFoliage(new TerrainChunkCoord(3, 4), 0, TestEngine.FoliageLayer(), 1, instances)
            .Should().Be(0);
    }
}
