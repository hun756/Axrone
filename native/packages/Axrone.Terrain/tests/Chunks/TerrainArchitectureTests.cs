using Axrone.Numeric;
using Axrone.Terrain;
using Axrone.Utility.Backoff.SpinPolicies;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainArchitectureTests
{
    private struct CountingObserver : IChunkLifecyclePolicy<CountingObserver>
    {
        internal static int Mounted;
        internal static int Unmounted;

        public static void OnMounted(TerrainChunkCoord coord) => Interlocked.Increment(ref Mounted);

        public static void OnUnmounted(TerrainChunkCoord coord) => Interlocked.Increment(ref Unmounted);

        internal static void Reset()
        {
            Mounted = 0;
            Unmounted = 0;
        }
    }

    [Fact]
    public void FastFbmRowMatchesIndividualSamples()
    {
        var surface = default(FastFbmSurfaceEvaluator);
        var row = new float[16];

        surface.EvaluateRow(new WorldPosition2D(-4f, 2.5f), 0.75f, row);

        for (var index = 0; index < row.Length; index++)
        {
            row[index].Should().BeApproximately(
                surface.EvaluateHeight(new WorldPosition2D(-4f + (index * 0.75f), 2.5f)),
                1e-6f);
        }
    }

    [Fact]
    public void FastFbmStaysNormalized()
    {
        var surface = default(FastFbmSurfaceEvaluator);

        for (var z = 0; z < 32; z++)
        {
            for (var x = 0; x < 32; x++)
            {
                surface.EvaluateHeight(new WorldPosition2D(x * 7.3f, z * 5.1f))
                    .Should().BeInRange(0f, 1f);
            }
        }
    }

    [Fact]
    public void SeededSurfaceDelegatesToTheNoiseField()
    {
        var field = new TerrainNoiseField(new TerrainNoiseOptions { Seed = 7 });
        var surface = new SeededNoiseSurface(field);
        var position = new WorldPosition2D(3.5f, -4.25f);

        surface.EvaluateHeight(position).Should().BeApproximately(
            field.SampleHeight(position.X, position.Z),
            1e-6f);

        var row = new float[8];
        surface.EvaluateRow(new WorldPosition2D(0f, 2f), 0.5f, row);
        for (var index = 0; index < row.Length; index++)
        {
            row[index].Should().BeApproximately(field.SampleHeight(index * 0.5f, 2f), 1e-6f);
        }
    }

    [Fact]
    public void BuilderBuildsAWorkingEngine()
    {
        using TerrainEngine<FastFbmSurfaceEvaluator, AdaptiveSpinBackoff, NoOpLifecyclePolicy> engine =
            new TerrainEngineBuilder<FastFbmSurfaceEvaluator, AdaptiveSpinBackoff, NoOpLifecyclePolicy>()
                .WithSurface(default)
                .WithCellSize(1f)
                .WithMaxHeight(10f)
                .WithPoolCapacity(4)
                .Build();

        engine.CommitChunk(new TerrainChunkCoord(0, 0)).Should().BeTrue();

        engine.SampleElevation(16f, 16f).Should().BeInRange(0f, 10f);
    }

    [Fact]
    public void LifecycleObserverReceivesMountAndUnmount()
    {
        CountingObserver.Reset();
        using TerrainEngine<FastFbmSurfaceEvaluator, AdaptiveSpinBackoff, CountingObserver> engine =
            new TerrainEngineBuilder<FastFbmSurfaceEvaluator, AdaptiveSpinBackoff, CountingObserver>()
                .WithSurface(default)
                .WithPoolCapacity(2)
                .Build();

        var coord = new TerrainChunkCoord(1, 2);
        engine.CommitChunk(coord);
        engine.DecommitChunk(coord);

        CountingObserver.Mounted.Should().Be(1);
        CountingObserver.Unmounted.Should().Be(1);
    }

    [Fact]
    public void TypestateStagesExist()
    {
        default(ChunkStages.Uninitialized).Should().BeOfType<ChunkStages.Uninitialized>();
        default(ChunkStages.HeightsPopulated).Should().BeOfType<ChunkStages.HeightsPopulated>();
        default(ChunkStages.NormalsComputed).Should().BeOfType<ChunkStages.NormalsComputed>();
        default(ChunkStages.Materialized).Should().BeOfType<ChunkStages.Materialized>();
    }

    [Fact]
    public void TerminalFaultIsRethrownByLaterCalls()
    {
        using TerrainEngine<FastFbmSurfaceEvaluator, AdaptiveSpinBackoff, NoOpLifecyclePolicy> engine =
            new TerrainEngineBuilder<FastFbmSurfaceEvaluator, AdaptiveSpinBackoff, NoOpLifecyclePolicy>()
                .WithSurface(default)
                .Build();

        engine.Complete(new InvalidOperationException("boom"));

        engine.Invoking(e => e.SampleElevation(1f, 1f))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("boom");
    }

    [Fact]
    public async Task DrainAsyncCompletesAfterCompletion()
    {
        using TerrainEngine<FastFbmSurfaceEvaluator, AdaptiveSpinBackoff, NoOpLifecyclePolicy> engine =
            new TerrainEngineBuilder<FastFbmSurfaceEvaluator, AdaptiveSpinBackoff, NoOpLifecyclePolicy>()
                .WithSurface(default)
                .Build();

        await engine.DrainAsync();

        engine.Invoking(e => e.SampleElevation(1f, 1f))
            .Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsyncReleasesTheEngine()
    {
        var engine = new TerrainEngineBuilder<FastFbmSurfaceEvaluator, AdaptiveSpinBackoff, NoOpLifecyclePolicy>()
            .WithSurface(default)
            .WithPoolCapacity(2)
            .Build();
        engine.CommitChunk(new TerrainChunkCoord(0, 0));

        await engine.DisposeAsync();

        engine.Invoking(e => e.ChunkCount).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void VertexMeshCarriesSplatWeights()
    {
        using var engine = new TerrainChunkEngine(new TerrainChunkEngineOptions
        {
            Surface = new FlatSurface(0.5f),
            MaxHeight = 10f,
            PoolCapacity = 4,
        });
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        var vertices = new TerrainVertex[TerrainChunkLayout.VertexCount];
        var indices = new uint[TerrainChunkLayout.IndexCount];

        engine.TryBuildChunkVertexMesh(coord, vertices, indices).Should().BeTrue();

        TerrainVertex first = vertices[0];
        first.Position.X.Should().BeApproximately(-16f, 1e-5f);
        first.Position.Y.Should().BeApproximately(5f, 1e-5f);
        first.Position.Z.Should().BeApproximately(-16f, 1e-5f);
        first.Normal.Y.Should().BeApproximately(1f, 1e-6f);
        first.TexCoord.X.Should().Be(0f);
        first.TexCoord.Y.Should().Be(0f);
        first.SplatWeights.X.Should().BeApproximately(1f, 1e-6f);
        first.SplatWeights.Y.Should().Be(0f);
    }

    [Fact]
    public void DomainOverloadsAndExtentDelegateToTheEngine()
    {
        using var engine = new TerrainChunkEngine(new TerrainChunkEngineOptions
        {
            Surface = new FlatSurface(0.5f),
            MaxHeight = 10f,
            PoolCapacity = 4,
        });
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);
        var position = new WorldPosition2D(16f, 16f);

        engine.SampleElevation(position).Should().BeApproximately(5f, 1e-5f);
        engine.TryGetElevation(position, out float height).Should().BeTrue();
        height.Should().BeApproximately(5f, 1e-5f);
        engine.TryGetNormal(position, out Vec3 normal).Should().BeTrue();
        normal.Y.Should().BeApproximately(1f, 1e-6f);
        engine.TryGetSplatWeights(position, out SplatWeights weights).Should().BeTrue();
        weights.R.Should().BeApproximately(1f, 1e-6f);

        engine.TryGetChunkInfo(coord, out TerrainChunkInfo info).Should().BeTrue();
        info.Extent.Min.Should().Be(info.MinHeight);
        info.Extent.Max.Should().Be(info.MaxHeight);
    }

    [Fact]
    public void VertexMeshConvenienceMatchesTheSpanBuilder()
    {
        using var engine = new TerrainChunkEngine(new TerrainChunkEngineOptions
        {
            Surface = new FlatSurface(0.5f),
            MaxHeight = 10f,
            PoolCapacity = 4,
        });
        var coord = new TerrainChunkCoord(0, 0);
        engine.CommitChunk(coord);

        TerrainVertexMesh? mesh = engine.BuildChunkVertexMesh(coord);

        mesh.Should().NotBeNull();
        mesh!.VertexCount.Should().Be(TerrainChunkLayout.VertexCount);
        mesh.TriangleCount.Should().Be(TerrainChunkLayout.IndexCount / 3);
        mesh.Vertices[0].Position.Y.Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void RangeCodecRoundTripsWorldHeights()
    {
        float[] heights = [-5f, 0f, 5f, 10f];

        string payload = TerrainHeightCodec.Encode(heights, 0f, 10f);
        var decoded = new float[heights.Length];
        TerrainHeightCodec.Decode(payload, 0f, 10f, decoded);

        decoded[0].Should().BeApproximately(0f, 1e-4f);
        decoded[1].Should().BeApproximately(0f, 1e-4f);
        decoded[2].Should().BeApproximately(5f, 1e-3f);
        decoded[3].Should().BeApproximately(10f, 1e-3f);
    }
}
