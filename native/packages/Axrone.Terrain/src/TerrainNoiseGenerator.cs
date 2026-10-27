namespace Axrone.Terrain;

/// <summary>
/// Deterministic fractional-Brownian-motion terrain generator built on
/// <see cref="TerrainNoiseField"/>. Identical descriptors and options always produce
/// identical heightmaps.
/// </summary>
public static class TerrainNoiseGenerator
{
    /// <summary>Generates a heightmap using the documented default noise options.</summary>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <returns>A normalized heightmap.</returns>
    /// <exception cref="TerrainException">The descriptor is invalid.</exception>
    public static TerrainHeightmap Generate(in TerrainDescriptor descriptor) =>
        Generate(descriptor, new TerrainNoiseOptions());

    /// <summary>Generates a normalized fractional-Brownian-motion heightmap.</summary>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <param name="options">Noise parameters; a default value selects the documented defaults.</param>
    /// <returns>A normalized heightmap in <c>[0, 1]</c>.</returns>
    /// <exception cref="TerrainException">The descriptor or options are invalid.</exception>
    [SkipLocalsInit]
    public static TerrainHeightmap Generate(in TerrainDescriptor descriptor, in TerrainNoiseOptions options)
    {
        descriptor.Validate();
        options.Validate();

        var field = new TerrainNoiseField(options);
        int resolution = descriptor.Resolution;
        var heights = new float[resolution * resolution];
        float lastGrid = resolution - 1;
        Span<float> row = stackalloc float[resolution];

        for (var gridZ = 0; gridZ < resolution; gridZ++)
        {
            float worldZ = (gridZ / lastGrid) * descriptor.Length;
            field.SampleRow(0f, worldZ, descriptor.Width / lastGrid, row);
            row.CopyTo(heights.AsSpan(gridZ * resolution, resolution));
        }

        return TerrainHeightmap.FromRawHeights(resolution, heights);
    }
}
