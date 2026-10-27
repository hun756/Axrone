using Axrone.Terrain;
using Axrone.Terrain.Chunks;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Chunks.Tests;

public sealed class TerrainChunkEditingTests
{
    [Fact]
    public void HeightRaiseBrushLiftsHeightsInsideTheRadiusOnly()
    {
        using TerrainChunkEngine engine = TestEngine.Create(poolCapacity: 4);
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        var brush = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Raise,
            Radius = 8f,
            Strength = 1f,
        };

        bool changed = engine.ApplyHeightBrush(16f, 16f, brush);

        changed.Should().BeTrue();
        engine.TryGetChunkElevation(16f, 16f, out float center).Should().BeTrue();
        engine.TryGetChunkElevation(30f, 30f, out float outside).Should().BeTrue();
        center.Should().BeGreaterThan(5f);
        outside.Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void LowerBrushPullsHeightsDown()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        var brush = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Lower,
            Radius = 8f,
            Strength = 1f,
        };

        engine.ApplyHeightBrush(16f, 16f, brush).Should().BeTrue();

        engine.TryGetChunkElevation(16f, 16f, out float center).Should().BeTrue();
        center.Should().BeLessThan(5f);
    }

    [Fact]
    public void SmoothBrushSoftensASpike()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        var spike = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Raise,
            Radius = 1f,
            Strength = 1f,
        };

        for (var stamp = 0; stamp < 20; stamp++)
        {
            engine.ApplyHeightBrush(16f, 16f, spike);
        }

        engine.TryGetChunkElevation(16f, 16f, out float beforeSmooth).Should().BeTrue();
        beforeSmooth.Should().BeApproximately(10f, 1e-4f);

        var smooth = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Smooth,
            Radius = 4f,
            Strength = 1f,
        };
        engine.ApplyHeightBrush(16f, 16f, smooth).Should().BeTrue();

        engine.TryGetChunkElevation(16f, 16f, out float afterSmooth).Should().BeTrue();
        afterSmooth.Should().BeLessThan(beforeSmooth);
        afterSmooth.Should().BeGreaterThan(5f);
    }

    [Fact]
    public void FlattenPullsNeighborsTowardTheCenterHeight()
    {
        using TerrainChunkEngine engine = TestEngine.Create(new RampSurface(0.01f), maxHeight: 1f);
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        engine.TryGetChunkElevation(16f, 16f, out float centerBefore).Should().BeTrue();
        engine.TryGetChunkElevation(12f, 16f, out float leftBefore).Should().BeTrue();

        var flatten = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Flatten,
            Radius = 10f,
            Strength = 1f,
        };
        engine.ApplyHeightBrush(16f, 16f, flatten).Should().BeTrue();

        engine.TryGetChunkElevation(12f, 16f, out float leftAfter).Should().BeTrue();
        MathF.Abs(leftAfter - centerBefore).Should().BeLessThan(MathF.Abs(leftBefore - centerBefore));
    }

    [Fact]
    public void BrushAcrossAChunkSeamUpdatesBothChunks()
    {
        using TerrainChunkEngine engine = TestEngine.Create(poolCapacity: 4);
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        engine.CommitChunk(new TerrainChunkCoord(1, 0));
        var brush = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Raise,
            Radius = 6f,
            Strength = 1f,
        };

        engine.ApplyHeightBrush(32f, 16f, brush).Should().BeTrue();

        engine.TryGetChunkElevation(31.9f, 16f, out float left).Should().BeTrue();
        engine.TryGetChunkElevation(32.1f, 16f, out float right).Should().BeTrue();

        left.Should().BeGreaterThan(5f);
        right.Should().BeGreaterThan(5f);
        left.Should().BeApproximately(right, 1e-3f);
    }

    [Fact]
    public void SplatBrushPreservesTheWeightInvariant()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        var brush = new TerrainBrushOptions
        {
            Radius = 12f,
            Strength = 1f,
        };

        engine.ApplySplatBrush(16f, 16f, brush, 1).Should().BeTrue();

        var splat = new byte[TerrainChunkLayout.SplatBytes];
        engine.TryCopyChunkSplat(coord, splat).Should().BeTrue();

        for (var texel = 0; texel < TerrainChunkLayout.VertexCount; texel++)
        {
            int sum = splat[texel * 4] + splat[(texel * 4) + 1] + splat[(texel * 4) + 2] + splat[(texel * 4) + 3];
            sum.Should().Be(TerrainSplatMath.WeightTotal);
        }

        engine.TryGetSplatWeights(16f, 16f, out SplatWeights weights).Should().BeTrue();
        weights.G.Should().BeGreaterThan(0f);
        weights.R.Should().BeLessThan(1f);
    }

    [Fact]
    public void FoliageDensityBrushPaintsAndErases()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        var brush = new TerrainBrushOptions
        {
            Radius = 12f,
            Strength = 1f,
        };

        for (var stamp = 0; stamp < 12; stamp++)
        {
            engine.ApplyFoliageDensityBrush(16f, 16f, brush, 2);
        }

        var density = new byte[TerrainChunkLayout.VertexCount];
        engine.TryCopyChunkFoliageDensity(coord, 2, density).Should().BeTrue();
        density.Should().Contain(value => value > 200);

        for (var stamp = 0; stamp < 24; stamp++)
        {
            engine.ApplyFoliageDensityBrush(16f, 16f, brush, 2, erase: true);
        }

        engine.TryCopyChunkFoliageDensity(coord, 2, density).Should().BeTrue();
        density.Should().OnlyContain(value => value == 0);
    }

    [Fact]
    public void EditingWithoutResidentChunksReturnsFalse()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        var brush = new TerrainBrushOptions();

        engine.ApplyHeightBrush(16f, 16f, brush).Should().BeFalse();
        engine.ApplySplatBrush(16f, 16f, brush, 0).Should().BeFalse();
        engine.ApplyFoliageDensityBrush(16f, 16f, brush, 0).Should().BeFalse();
    }

    [Fact]
    public void InvalidBrushIsRejected()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));
        var brush = new TerrainBrushOptions { Radius = 0f };

        engine.Invoking(e => e.ApplyHeightBrush(16f, 16f, brush))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void InvalidSplatLayerIsRejected()
    {
        using TerrainChunkEngine engine = TestEngine.Create();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));

        engine.Invoking(e => e.ApplySplatBrush(16f, 16f, new TerrainBrushOptions(), 4))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }
}
