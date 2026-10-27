namespace Axrone.Terrain;

/// <summary>
/// Decodes RGBA pixel data (the structural equivalent of DOM <c>ImageData</c>)
/// into a normalized heightmap by bilinear-resampling source luminance onto the
/// requested grid resolution.
/// </summary>
public static class TerrainHeightmapDecoder
{
    private const float RedLuminance = 0.2126f;
    private const float GreenLuminance = 0.7152f;
    private const float BlueLuminance = 0.0722f;

    /// <summary>Decodes an RGBA8 pixel buffer into a heightmap.</summary>
    /// <param name="rgba">Row-major RGBA pixels; length must be <c>width * height * 4</c>.</param>
    /// <param name="width">Source image width in pixels; at least 2.</param>
    /// <param name="height">Source image height in pixels; at least 2.</param>
    /// <param name="resolution">Target height-grid resolution.</param>
    /// <returns>A heightmap sampled from the image luminance.</returns>
    /// <exception cref="TerrainException">
    /// The image is smaller than 2x2, the pixel buffer length is wrong, or the
    /// resolution is unsupported.
    /// </exception>
    public static TerrainHeightmap DecodeFromRgba(
        ReadOnlySpan<byte> rgba,
        int width,
        int height,
        int resolution)
    {
        TerrainLimits.AssertResolution(resolution);

        if (width < 2 || height < 2)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Heightmap image must be at least 2x2 pixels, received {width}x{height}."),
                TerrainErrorCode.SourceDecodeFailed);
        }

        if (rgba.Length != width * height * 4)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Heightmap pixel buffer length {rgba.Length} does not match {width}x{height} RGBA layout."),
                TerrainErrorCode.SourceDecodeFailed);
        }

        var heights = new float[resolution * resolution];
        float lastGrid = resolution - 1;

        for (var gridZ = 0; gridZ < resolution; gridZ++)
        {
            float sourceZ = (gridZ / lastGrid) * (height - 1);
            int z0 = (int)MathF.Floor(sourceZ);
            int z1 = Math.Min(z0 + 1, height - 1);
            float tz = sourceZ - z0;

            for (var gridX = 0; gridX < resolution; gridX++)
            {
                float sourceX = (gridX / lastGrid) * (width - 1);
                int x0 = (int)MathF.Floor(sourceX);
                int x1 = Math.Min(x0 + 1, width - 1);
                float tx = sourceX - x0;

                float topLeft = LuminanceAt(rgba, width, x0, z0);
                float topRight = LuminanceAt(rgba, width, x1, z0);
                float bottomLeft = LuminanceAt(rgba, width, x0, z1);
                float bottomRight = LuminanceAt(rgba, width, x1, z1);

                float top = topLeft + ((topRight - topLeft) * tx);
                float bottom = bottomLeft + ((bottomRight - bottomLeft) * tx);
                heights[(gridZ * resolution) + gridX] = top + ((bottom - top) * tz);
            }
        }

        return TerrainHeightmap.FromRawHeights(resolution, heights);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LuminanceAt(ReadOnlySpan<byte> rgba, int width, int x, int z)
    {
        int pixelIndex = ((z * width) + x) * 4;
        float red = rgba[pixelIndex];
        float green = rgba[pixelIndex + 1];
        float blue = rgba[pixelIndex + 2];
        return ((RedLuminance * red) + (GreenLuminance * green) + (BlueLuminance * blue)) / 255f;
    }
}
