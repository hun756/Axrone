namespace Axrone.Terrain;

/// <summary>Normalized RGBA splat weights sampled from a weight buffer.</summary>
/// <param name="R">Layer 0 weight in <c>[0, 1]</c>.</param>
/// <param name="G">Layer 1 weight in <c>[0, 1]</c>.</param>
/// <param name="B">Layer 2 weight in <c>[0, 1]</c>.</param>
/// <param name="A">Layer 3 weight in <c>[0, 1]</c>.</param>
public readonly record struct SplatWeights(float R, float G, float B, float A)
{
    /// <summary>Gets the sum of all channel weights.</summary>
    public float Sum => R + G + B + A;
}

/// <summary>
/// Splat weight buffers: RGBA texels where each channel carries one layer's
/// blend weight. Channel weights sum to 255 on every texel so the shader can
/// renormalize cheaply and painting stays lossless under repeated stamps.
/// </summary>
public static class TerrainSplatPainter
{

    /// <summary>Creates an RGBA weight buffer fully assigned to the first layer.</summary>
    /// <param name="resolution">Splat resolution; one of 64, 128 or 256.</param>
    /// <returns>A buffer of <c>resolution * resolution * 4</c> bytes.</returns>
    /// <exception cref="TerrainException">The resolution is unsupported.</exception>
    public static byte[] CreateBuffer(int resolution)
    {
        TerrainLimits.AssertSplatResolution(resolution);

        var splat = new byte[resolution * resolution * 4];
        for (var index = 0; index < splat.Length; index += 4)
        {
            splat[index] = TerrainSplatMath.WeightTotal;
        }

        return splat;
    }

    /// <summary>
    /// Paints one brush stamp of the target layer into the weight buffer in
    /// place, renormalizing the remaining channels so every texel keeps the
    /// 255-total invariant.
    /// </summary>
    /// <param name="splat">RGBA weight buffer.</param>
    /// <param name="resolution">Splat resolution; one of 64, 128 or 256.</param>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <param name="brush">Resolved brush parameters.</param>
    /// <param name="layerIndex">Target layer channel in <c>[0, 3]</c>.</param>
    /// <param name="localX">Stamp center X in terrain-local space.</param>
    /// <param name="localZ">Stamp center Z in terrain-local space.</param>
    /// <returns><see langword="true"/> when at least one texel changed.</returns>
    /// <exception cref="TerrainException">A parameter or buffer is invalid.</exception>
    public static bool ApplyStamp(
        Span<byte> splat,
        int resolution,
        in TerrainDescriptor descriptor,
        in TerrainBrushOptions brush,
        int layerIndex,
        float localX,
        float localZ)
    {
        descriptor.Validate();
        brush.Validate();
        TerrainLimits.AssertSplatResolution(resolution);

        int expectedLength = resolution * resolution * 4;
        if (splat.Length != expectedLength)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Splat buffer length {splat.Length} does not match resolution {resolution} (expected {expectedLength})."),
                TerrainErrorCode.SplatSizeMismatch);
        }

        if (layerIndex < 0 || layerIndex >= TerrainLimits.MaxLayers)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Invalid splat layer index: {layerIndex}. Expected an integer in [0, {TerrainLimits.MaxLayers - 1}]."),
                TerrainErrorCode.ValidationFailed);
        }

        int lastTexel = resolution - 1;
        float texelSizeX = descriptor.Width / lastTexel;
        float texelSizeZ = descriptor.Length / lastTexel;

        float texelCenterX = (localX + (descriptor.Width * 0.5f)) / texelSizeX;
        float texelCenterZ = (localZ + (descriptor.Length * 0.5f)) / texelSizeZ;
        float texelRadiusX = brush.Radius / texelSizeX;
        float texelRadiusZ = brush.Radius / texelSizeZ;

        int minX = Math.Max(0, (int)MathF.Floor(texelCenterX - texelRadiusX));
        int maxX = Math.Min(lastTexel, (int)MathF.Ceiling(texelCenterX + texelRadiusX));
        int minZ = Math.Max(0, (int)MathF.Floor(texelCenterZ - texelRadiusZ));
        int maxZ = Math.Min(lastTexel, (int)MathF.Ceiling(texelCenterZ + texelRadiusZ));
        if (minX > maxX || minZ > maxZ)
        {
            return false;
        }

        var changed = false;

        for (var texelZ = minZ; texelZ <= maxZ; texelZ++)
        {
            for (var texelX = minX; texelX <= maxX; texelX++)
            {
                float worldDx = (texelX - texelCenterX) * texelSizeX;
                float worldDz = (texelZ - texelCenterZ) * texelSizeZ;
                float distance = MathF.Sqrt((worldDx * worldDx) + (worldDz * worldDz));
                if (distance > brush.Radius)
                {
                    continue;
                }

                float weight = MathF.Pow(TerrainMath.Smoothstep(1f - (distance / brush.Radius)), brush.Falloff);
                if (weight <= 0f)
                {
                    continue;
                }

                int offset = ((texelZ * resolution) + texelX) * 4;
                if (TerrainSplatMath.Paint(
                        splat.Slice(offset, 4),
                        layerIndex,
                        brush.Strength * weight * TerrainLimits.SplatStampScale))
                {
                    changed = true;
                }
            }
        }

        return changed;
    }

    /// <summary>Bilinear weight sample at normalized <c>[0, 1]</c> coordinates.</summary>
    /// <param name="splat">RGBA weight buffer.</param>
    /// <param name="resolution">Splat resolution; one of 64, 128 or 256.</param>
    /// <param name="u">Horizontal coordinate; clamped into <c>[0, 1]</c>.</param>
    /// <param name="v">Vertical coordinate; clamped into <c>[0, 1]</c>.</param>
    /// <returns>Normalized per-channel weights.</returns>
    /// <exception cref="TerrainException">The resolution or buffer is invalid.</exception>
    public static SplatWeights Sample(ReadOnlySpan<byte> splat, int resolution, float u, float v)
    {
        TerrainLimits.AssertSplatResolution(resolution);
        AssertBuffer(splat, resolution);
        return TerrainMath.BilinearRgba(splat, resolution, u, v);
    }

    internal static void AssertBuffer(ReadOnlySpan<byte> splat, int resolution)
    {
        int expectedLength = resolution * resolution * 4;
        if (splat.Length != expectedLength)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Splat buffer length {splat.Length} does not match resolution {resolution} (expected {expectedLength})."),
                TerrainErrorCode.SplatSizeMismatch);
        }
    }
}
