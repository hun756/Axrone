namespace Axrone.Terrain;

/// <summary>Shared scalar helpers for the terrain pipeline.</summary>
internal static class TerrainMath
{
    /// <summary>Lower bound used when normalizing zero-length gradient vectors.</summary>
    internal const float Epsilon = 1e-6f;

    /// <summary>Hermite interpolation between 0 and 1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Smoothstep(float t) => t * t * (3f - (2f * t));

    /// <summary>Clamps a value into <c>[0, 1]</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

    /// <summary>Rounds half away from zero, matching JavaScript's <c>Math.round</c> for non-negative inputs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RoundHalfUp(float value) => (int)MathF.Floor(value + 0.5f);

    /// <summary>Bilinear sample from a row-major single-channel grid at normalized coordinates.</summary>
    internal static float Bilinear(ReadOnlySpan<float> data, int resolution, float u, float v)
    {
        float clampedU = Clamp01(u);
        float clampedV = Clamp01(v);
        float gridX = clampedU * (resolution - 1);
        float gridZ = clampedV * (resolution - 1);
        int x0 = (int)MathF.Floor(gridX);
        int z0 = (int)MathF.Floor(gridZ);
        int x1 = Math.Min(x0 + 1, resolution - 1);
        int z1 = Math.Min(z0 + 1, resolution - 1);
        float tx = gridX - x0;
        float tz = gridZ - z0;

        float topLeft = data[(z0 * resolution) + x0];
        float topRight = data[(z0 * resolution) + x1];
        float bottomLeft = data[(z1 * resolution) + x0];
        float bottomRight = data[(z1 * resolution) + x1];

        float top = topLeft + ((topRight - topLeft) * tx);
        float bottom = bottomLeft + ((bottomRight - bottomLeft) * tx);
        return top + ((bottom - top) * tz);
    }

    /// <summary>Bilinear sample from a row-major RGBA byte grid, normalized to <c>[0, 1]</c>.</summary>
    internal static SplatWeights BilinearRgba(ReadOnlySpan<byte> data, int resolution, float u, float v)
    {
        float clampedU = Clamp01(u);
        float clampedV = Clamp01(v);
        float texelX = clampedU * (resolution - 1);
        float texelZ = clampedV * (resolution - 1);
        int x0 = (int)MathF.Floor(texelX);
        int z0 = (int)MathF.Floor(texelZ);
        int x1 = Math.Min(x0 + 1, resolution - 1);
        int z1 = Math.Min(z0 + 1, resolution - 1);
        float tx = texelX - x0;
        float tz = texelZ - z0;

        Span<float> channels = stackalloc float[4];
        for (var channel = 0; channel < 4; channel++)
        {
            float topLeft = data[(((z0 * resolution) + x0) * 4) + channel];
            float topRight = data[(((z0 * resolution) + x1) * 4) + channel];
            float bottomLeft = data[(((z1 * resolution) + x0) * 4) + channel];
            float bottomRight = data[(((z1 * resolution) + x1) * 4) + channel];
            float top = topLeft + ((topRight - topLeft) * tx);
            float bottom = bottomLeft + ((bottomRight - bottomLeft) * tx);
            channels[channel] = (top + ((bottom - top) * tz)) / 255f;
        }

        return new SplatWeights(channels[0], channels[1], channels[2], channels[3]);
    }

    /// <summary>Bilinear sample from a row-major single-byte grid, normalized to <c>[0, 1]</c>.</summary>
    internal static float BilinearByte(ReadOnlySpan<byte> data, int resolution, float u, float v)
    {
        float clampedU = Clamp01(u);
        float clampedV = Clamp01(v);
        float texelX = clampedU * (resolution - 1);
        float texelZ = clampedV * (resolution - 1);
        int x0 = (int)MathF.Floor(texelX);
        int z0 = (int)MathF.Floor(texelZ);
        int x1 = Math.Min(x0 + 1, resolution - 1);
        int z1 = Math.Min(z0 + 1, resolution - 1);
        float tx = texelX - x0;
        float tz = texelZ - z0;

        float topLeft = data[(z0 * resolution) + x0];
        float topRight = data[(z0 * resolution) + x1];
        float bottomLeft = data[(z1 * resolution) + x0];
        float bottomRight = data[(z1 * resolution) + x1];
        float top = topLeft + ((topRight - topLeft) * tx);
        float bottom = bottomLeft + ((bottomRight - bottomLeft) * tx);
        return (top + ((bottom - top) * tz)) / 255f;
    }
}
