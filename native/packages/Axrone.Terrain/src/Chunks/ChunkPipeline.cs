namespace Axrone.Terrain;

/// <summary>Marker for one compile-time stage of the chunk generation pipeline.</summary>
public interface IChunkStage
{
}

/// <summary>Concrete chunk generation stages enforced by <see cref="StagedChunk{TStage}"/>.</summary>
public static class ChunkStages
{
    /// <summary>Slot rented, nothing written.</summary>
    public readonly struct Uninitialized : IChunkStage
    {
    }

    /// <summary>Heights and height extents written.</summary>
    public readonly struct HeightsPopulated : IChunkStage
    {
    }

    /// <summary>Normals derived from the heights.</summary>
    public readonly struct NormalsComputed : IChunkStage
    {
    }

    /// <summary>Splat and foliage buffers initialized.</summary>
    public readonly struct Materialized : IChunkStage
    {
    }
}

/// <summary>
/// Typestate handle over a chunk slot. Extension methods only accept the stage they can
/// advance, so a chunk cannot be published before heights, normals and materials exist.
/// </summary>
/// <typeparam name="TStage">Current pipeline stage.</typeparam>
internal readonly ref struct StagedChunk<TStage>
    where TStage : struct, IChunkStage
{
    internal StagedChunk(TerrainChunkView view) => View = view;

    internal TerrainChunkView View { get; }
}

/// <summary>Typestate transitions for the chunk generation pipeline.</summary>
internal static class StagedChunkExtensions
{
    internal static StagedChunk<ChunkStages.HeightsPopulated> PopulateHeights<TSurface>(
        this StagedChunk<ChunkStages.Uninitialized> staged,
        in TSurface surface,
        TerrainChunkCoord coord,
        float chunkSpan,
        float cellSize,
        uint generation)
        where TSurface : struct, ISurfaceEvaluator<TSurface>
    {
        TerrainChunkView view = staged.View;
        ref TerrainChunkHeader header = ref view.Header;
        header.Coord = coord;
        header.Generation = generation;
        header.State = TerrainChunkState.Building;

        float originX = coord.X * chunkSpan;
        float originZ = coord.Z * chunkSpan;
        Span<float> heights = view.Heights;
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;

        for (var z = 0; z < TerrainChunkLayout.VerticesPerAxis; z++)
        {
            Span<float> row = heights.Slice(
                z * TerrainChunkLayout.VerticesPerAxis,
                TerrainChunkLayout.VerticesPerAxis);
            surface.EvaluateRow(new WorldPosition2D(originX, originZ + (z * cellSize)), cellSize, row);

            for (var x = 0; x < row.Length; x++)
            {
                float height = float.IsFinite(row[x]) ? Math.Clamp(row[x], 0f, 1f) : 0f;
                row[x] = height;
                if (height < min)
                {
                    min = height;
                }

                if (height > max)
                {
                    max = height;
                }
            }
        }

        header.MinHeight = min;
        header.MaxHeight = max;
        return new StagedChunk<ChunkStages.HeightsPopulated>(view);
    }

    internal static StagedChunk<ChunkStages.NormalsComputed> ComputeNormals(
        this StagedChunk<ChunkStages.HeightsPopulated> staged,
        float cellSize)
    {
        TerrainChunkOps.ComputeNormals(staged.View, cellSize);
        return new StagedChunk<ChunkStages.NormalsComputed>(staged.View);
    }

    internal static StagedChunk<ChunkStages.Materialized> InitializeMaterials(
        this StagedChunk<ChunkStages.NormalsComputed> staged)
    {
        TerrainChunkView view = staged.View;
        Span<byte> splat = view.Splat;
        for (var index = 0; index < splat.Length; index += 4)
        {
            splat[index] = TerrainSplatMath.WeightTotal;
            splat[index + 1] = 0;
            splat[index + 2] = 0;
            splat[index + 3] = 0;
        }

        view.Density.Clear();
        return new StagedChunk<ChunkStages.Materialized>(view);
    }

    internal static void Finalize(this StagedChunk<ChunkStages.Materialized> staged) =>
        staged.View.Header.State = TerrainChunkState.Ready;
}

/// <summary>Shared chunk buffer operations used by the pipeline and the editing path.</summary>
internal static class TerrainChunkOps
{
    internal static void ComputeNormals(TerrainChunkView view, float cellSize)
    {
        Span<float> heights = view.Heights;
        const int stride = TerrainChunkLayout.VerticesPerAxis;
        const int last = TerrainChunkLayout.Subdivisions;

        for (var z = 0; z < stride; z++)
        {
            for (var x = 0; x < stride; x++)
            {
                int index = (z * stride) + x;
                float left = x > 0 ? heights[index - 1] : heights[index];
                float right = x < last ? heights[index + 1] : heights[index];
                float near = z > 0 ? heights[index - stride] : heights[index];
                float far = z < last ? heights[index + stride] : heights[index];

                int spanX = (x > 0 ? 1 : 0) + (x < last ? 1 : 0);
                int spanZ = (z > 0 ? 1 : 0) + (z < last ? 1 : 0);
                float dx = (right - left) / (spanX * cellSize);
                float dz = (far - near) / (spanZ * cellSize);

                float inverse = 1f / MathF.Sqrt((dx * dx) + 1f + (dz * dz));
                view.SetNormal(index, new Vec3(-dx * inverse, inverse, -dz * inverse));
            }
        }
    }

    internal static void UpdateExtentsAndNormals(TerrainChunkView view, float cellSize)
    {
        Span<float> heights = view.Heights;
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        foreach (float height in heights)
        {
            if (height < min)
            {
                min = height;
            }

            if (height > max)
            {
                max = height;
            }
        }

        ref TerrainChunkHeader header = ref view.Header;
        header.MinHeight = min;
        header.MaxHeight = max;
        ComputeNormals(view, cellSize);
    }
}
