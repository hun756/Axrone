using Axrone.Terrain;
using Axrone.Terrain.Chunks;

namespace Axrone.Terrain.Chunks.Tests;

internal sealed class FlatSurface : ITerrainSurface
{
    private readonly float _height;

    public FlatSurface(float height) => _height = height;

    public float SampleHeight(float worldX, float worldZ) => _height;

    public void SampleRow(float startX, float worldZ, float stepX, Span<float> destination) =>
        destination.Fill(_height);
}

internal sealed class RampSurface : ITerrainSurface
{
    private readonly float _slope;

    public RampSurface(float slope) => _slope = slope;

    public float SampleHeight(float worldX, float worldZ) => Math.Clamp(_slope * worldX, 0f, 1f);

    public void SampleRow(float startX, float worldZ, float stepX, Span<float> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = SampleHeight(startX + (index * stepX), worldZ);
        }
    }
}

internal static class TestEngine
{
    public static TerrainChunkEngine Create(
        ITerrainSurface? surface = null,
        int poolCapacity = 8,
        float cellSize = 1f,
        float maxHeight = 10f) =>
        new(new TerrainChunkEngineOptions
        {
            Surface = surface ?? new FlatSurface(0.5f),
            CellSize = cellSize,
            MaxHeight = maxHeight,
            PoolCapacity = poolCapacity,
        });

    public static TerrainFoliageLayer FoliageLayer(
        float density = 1f,
        float minScale = 0.8f,
        float maxScale = 1.4f,
        float maxSlopeDeg = 90f) => new()
        {
            Id = "grass",
            Name = "Grass",
            MeshAsset = string.Empty,
            Density = density,
            MinScale = minScale,
            MaxScale = maxScale,
            AlignToNormal = false,
            MaxSlopeDeg = maxSlopeDeg,
        };
}
