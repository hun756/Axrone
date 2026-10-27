namespace Axrone.Terrain;

/// <summary>
/// Plain heightfield collider source matching the terrain mesh layout. Heights
/// stay normalized while <see cref="ScaleY"/> carries the world height and
/// <see cref="ScaleX"/>/<see cref="ScaleZ"/> describe the world-space cell size,
/// mirroring the physics heightfield local-vertex convention (origin-centered
/// grid).
/// </summary>
public sealed class TerrainHeightfieldSource
{
    /// <summary>Initializes a heightfield source.</summary>
    /// <param name="heights">Owned normalized height buffer.</param>
    /// <param name="width">Sample count along X.</param>
    /// <param name="depth">Sample count along Z.</param>
    /// <param name="scaleX">World-space cell size along X.</param>
    /// <param name="scaleY">World-space height multiplier.</param>
    /// <param name="scaleZ">World-space cell size along Z.</param>
    public TerrainHeightfieldSource(
        float[] heights,
        int width,
        int depth,
        float scaleX,
        float scaleY,
        float scaleZ)
    {
        ArgumentNullException.ThrowIfNull(heights);
        Heights = heights;
        Width = width;
        Depth = depth;
        ScaleX = scaleX;
        ScaleY = scaleY;
        ScaleZ = scaleZ;
    }

    /// <summary>Gets the owned normalized height buffer (row-major, <c>z * width + x</c>).</summary>
    public float[] Heights { get; }

    /// <summary>Gets the sample count along X.</summary>
    public int Width { get; }

    /// <summary>Gets the sample count along Z.</summary>
    public int Depth { get; }

    /// <summary>Gets the world-space cell size along X.</summary>
    public float ScaleX { get; }

    /// <summary>Gets the world-space height multiplier.</summary>
    public float ScaleY { get; }

    /// <summary>Gets the world-space cell size along Z.</summary>
    public float ScaleZ { get; }
}

/// <summary>Creates physics-ready heightfield collider sources from a heightmap.</summary>
public static class TerrainHeightfield
{
    /// <summary>Creates a heightfield source matching the terrain mesh layout.</summary>
    /// <param name="heightmap">Heightmap whose resolution matches the descriptor.</param>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <returns>A heightfield source owning a copy of the heights.</returns>
    /// <exception cref="TerrainException">
    /// The descriptor is invalid, or the heightmap resolution differs from it.
    /// </exception>
    public static TerrainHeightfieldSource Create(
        TerrainHeightmap heightmap,
        in TerrainDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(heightmap);
        descriptor.Validate();

        if (heightmap.Resolution != descriptor.Resolution)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Heightmap resolution {heightmap.Resolution} does not match descriptor resolution {descriptor.Resolution}."),
                TerrainErrorCode.HeightmapSizeMismatch);
        }

        int resolution = descriptor.Resolution;
        return new TerrainHeightfieldSource(
            heightmap.ToArray(),
            resolution,
            resolution,
            descriptor.Width / (resolution - 1),
            descriptor.MaxHeight,
            descriptor.Length / (resolution - 1));
    }
}
