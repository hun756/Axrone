namespace Axrone.Terrain;

/// <summary>
/// Deterministic foliage scatter: candidate points are drawn cell-by-cell from
/// the seeded engine PRNG and accepted proportionally to the painted density
/// map, so identical inputs always reproduce the identical forest. Heights are
/// sampled from the live heightmap, so sculpting the terrain reseats every
/// instance on the next scatter pass.
/// </summary>
public static class TerrainFoliageScatter
{
    /// <summary>Produces the deterministic instance list for one foliage layer.</summary>
    /// <param name="heightmap">Heightmap providing surface heights.</param>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <param name="density">Single-channel density buffer.</param>
    /// <param name="densityResolution">Density buffer resolution.</param>
    /// <param name="layer">Foliage layer definition.</param>
    /// <param name="seed">Scatter seed.</param>
    /// <returns>Scattered instances, capped at <see cref="TerrainLimits.MaxFoliageInstances"/>.</returns>
    /// <exception cref="TerrainException">The descriptor, layer or buffer is invalid.</exception>
    public static TerrainFoliageInstance[] Scatter(
        TerrainHeightmap heightmap,
        in TerrainDescriptor descriptor,
        ReadOnlySpan<byte> density,
        int densityResolution,
        in TerrainFoliageLayer layer,
        ulong seed)
    {
        ArgumentNullException.ThrowIfNull(heightmap);
        descriptor.Validate();
        ReadOnlySpan<TerrainFoliageLayer> single = [layer];
        TerrainValidation.ValidateFoliageLayers(single);

        if (layer.Density <= 0f)
        {
            return [];
        }

        var random = new RandomEngine<Xoroshiro128PlusPlus>(seed);
        int texelCount = densityResolution * densityResolution;
        int candidateCount = Math.Min(
            (int)MathF.Ceiling(texelCount * TerrainLimits.FoliageCandidatesPerTexel),
            TerrainLimits.MaxFoliageInstances * 4);

        var instances = new List<TerrainFoliageInstance>();
        for (var candidate = 0; candidate < candidateCount; candidate++)
        {
            float u = random.NextSingle();
            float v = random.NextSingle();
            float acceptance = random.NextSingle();
            float rotationY = random.NextSingle() * MathF.PI * 2f;
            float scaleT = random.NextSingle();

            float localDensity =
                TerrainFoliageDensity.Sample(density, densityResolution, u, v) * layer.Density;
            if (localDensity <= 0f || acceptance > localDensity)
            {
                continue;
            }

            if (layer.MaxSlopeDeg < 90f)
            {
                float slopeDeg = SampleSlopeDegrees(heightmap, descriptor, u, v);
                if (slopeDeg > layer.MaxSlopeDeg)
                {
                    continue;
                }
            }

            instances.Add(new TerrainFoliageInstance(
                (u - 0.5f) * descriptor.Width,
                heightmap.SampleHeight(u, v) * descriptor.MaxHeight,
                (v - 0.5f) * descriptor.Length,
                rotationY,
                layer.MinScale + ((layer.MaxScale - layer.MinScale) * scaleT)));

            if (instances.Count >= TerrainLimits.MaxFoliageInstances)
            {
                break;
            }
        }

        return [.. instances];
    }

    private static float SampleSlopeDegrees(
        TerrainHeightmap heightmap,
        in TerrainDescriptor descriptor,
        float u,
        float v)
    {
        float step = 1f / (heightmap.Resolution - 1);
        float left = heightmap.SampleHeight(u - step, v);
        float right = heightmap.SampleHeight(u + step, v);
        float near = heightmap.SampleHeight(u, v - step);
        float far = heightmap.SampleHeight(u, v + step);

        float worldStepX = 2f * step * descriptor.Width;
        float worldStepZ = 2f * step * descriptor.Length;
        float slopeX = ((right - left) * descriptor.MaxHeight) / worldStepX;
        float slopeZ = ((far - near) * descriptor.MaxHeight) / worldStepZ;
        return MathF.Atan(MathF.Sqrt((slopeX * slopeX) + (slopeZ * slopeZ))) * (180f / MathF.PI);
    }
}
