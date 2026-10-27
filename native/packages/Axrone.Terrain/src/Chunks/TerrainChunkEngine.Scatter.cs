namespace Axrone.Terrain;

/// <summary>
/// Deterministic foliage scatter. Candidate draws are fixed per candidate (position,
/// acceptance, rotation and scale), so painting density never shifts later instances, the
/// candidate budget depends only on the chunk, and the output is independent of the caller's
/// destination length.
/// </summary>
public partial class TerrainEngine<TSurface, TBackoff, TObserver>
{
    private static readonly int FoliageCandidatesPerChunk =
        (int)MathF.Ceiling(TerrainChunkLayout.VertexCount * TerrainLimits.FoliageCandidatesPerTexel);

    private const ulong ScatterSalt = 0x9E3779B97F4A7C15UL;

    /// <summary>Scatters foliage for one layer inside a chunk.</summary>
    /// <param name="coord">Chunk coordinate.</param>
    /// <param name="layerIndex">Density layer in <c>[0, 3]</c>.</param>
    /// <param name="layer">Layer definition.</param>
    /// <param name="seed">World seed.</param>
    /// <param name="destination">Destination instance span.</param>
    /// <returns>The number of instances written; zero when the chunk is missing or empty.</returns>
    /// <exception cref="TerrainException">The layer or layer index is invalid.</exception>
    /// <exception cref="ObjectDisposedException">The engine is disposed.</exception>
    public int ScatterChunkFoliage(
        TerrainChunkCoord coord,
        int layerIndex,
        in TerrainFoliageLayer layer,
        ulong seed,
        Span<TerrainFoliageInstance> destination)
    {
        ReadOnlySpan<TerrainFoliageLayer> layers = [layer];
        TerrainValidation.ValidateFoliageLayers(layers);
        if (layerIndex < 0 || layerIndex >= TerrainChunkLayout.FoliageLayers)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid foliage layer index: {layerIndex}."),
                TerrainErrorCode.ValidationFailed);
        }

        if (destination.IsEmpty)
        {
            return 0;
        }

        EnterRead();
        try
        {
            ThrowIfDisposed();
            if (!_chunks.TryGetValue(coord, out ChunkEntry? entry))
            {
                return 0;
            }

            TerrainChunkView view = entry.CreateView();
            Span<byte> densityMap = view.FoliageDensity(layerIndex);
            float originX = coord.X * _chunkSpan;
            float originZ = coord.Z * _chunkSpan;

            Span<ulong> seedWords = stackalloc ulong[5];
            seedWords[0] = seed;
            seedWords[1] = (ulong)(uint)coord.X;
            seedWords[2] = (ulong)(uint)coord.Z;
            seedWords[3] = (ulong)(uint)layerIndex;
            seedWords[4] = ScatterSalt;
            (ulong S0, ulong _, ulong _, ulong _) = SeedHashing.HashUInt64sToWords(seedWords);
            var random = new RandomEngine<Xoroshiro128PlusPlus>(S0);

            var written = 0;
            for (var candidate = 0; candidate < FoliageCandidatesPerChunk; candidate++)
            {
                float u = random.NextSingle();
                float v = random.NextSingle();
                float acceptance = random.NextSingle();
                float rotation = random.NextSingle() * MathF.PI * 2f;
                float scaleT = random.NextSingle();

                float density = SampleFoliageDensity(densityMap, u, v) * layer.Density;
                if (density <= 0f || acceptance > density)
                {
                    continue;
                }

                float worldX = originX + (u * _chunkSpan);
                float worldZ = originZ + (v * _chunkSpan);
                Vec3 normal = SampleBilinearNormal(entry, coord, worldX, worldZ);
                if (layer.MaxSlopeDeg < 90f)
                {
                    float slopeDegrees = MathF.Acos(Math.Clamp(normal.Y, -1f, 1f)) * (180f / MathF.PI);
                    if (slopeDegrees > layer.MaxSlopeDeg)
                    {
                        continue;
                    }
                }

                float worldY = SampleBilinearHeight(entry, coord, worldX, worldZ) * _options.MaxHeight;
                destination[written++] = new TerrainFoliageInstance(
                    worldX,
                    worldY,
                    worldZ,
                    rotation,
                    layer.MinScale + ((layer.MaxScale - layer.MinScale) * scaleT),
                    layer.AlignToNormal ? normal : Vec3.UnitY);

                if (written == destination.Length)
                {
                    break;
                }
            }

            return written;
        }
        finally
        {
            ExitRead();
        }
    }

    private static float SampleFoliageDensity(ReadOnlySpan<byte> density, float u, float v)
    {
        float clampedU = Math.Clamp(u, 0f, 1f);
        float clampedV = Math.Clamp(v, 0f, 1f);
        float gridX = clampedU * TerrainChunkLayout.Subdivisions;
        float gridZ = clampedV * TerrainChunkLayout.Subdivisions;
        int x0 = (int)MathF.Floor(gridX);
        int z0 = (int)MathF.Floor(gridZ);
        int x1 = Math.Min(x0 + 1, TerrainChunkLayout.Subdivisions);
        int z1 = Math.Min(z0 + 1, TerrainChunkLayout.Subdivisions);
        float tx = gridX - x0;
        float tz = gridZ - z0;

        const int stride = TerrainChunkLayout.VerticesPerAxis;
        float topLeft = density[(z0 * stride) + x0];
        float topRight = density[(z0 * stride) + x1];
        float bottomLeft = density[(z1 * stride) + x0];
        float bottomRight = density[(z1 * stride) + x1];
        float top = topLeft + ((topRight - topLeft) * tx);
        float bottom = bottomLeft + ((bottomRight - bottomLeft) * tx);
        return (top + ((bottom - top) * tz)) / 255f;
    }
}
