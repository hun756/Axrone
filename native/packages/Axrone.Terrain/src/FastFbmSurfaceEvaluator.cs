namespace Axrone.Terrain;

/// <summary>
/// Stateless gradient-noise terrain surface using four octaves of improved Perlin-style
/// value gradients. The evaluator is a policy: <see cref="TerrainEngine{TSurface,TBackoff,TObserver}"/>
/// closes over it at compile time, so row evaluation has no virtual dispatch.
/// </summary>
public readonly struct FastFbmSurfaceEvaluator : ISurfaceEvaluator<FastFbmSurfaceEvaluator>
{
    private const float BaseFrequency = 0.005f;
    private const float Persistence = 0.45f;
    private const float Lacunarity = 2.1f;
    private const float AmplitudeSum =
        1f + Persistence + (Persistence * Persistence) + (Persistence * Persistence * Persistence);

    /// <inheritdoc/>
    public float EvaluateHeight(WorldPosition2D position)
    {
        float amplitude = 1f;
        float frequency = BaseFrequency;
        float total = 0f;

        for (var octave = 0; octave < 4; octave++)
        {
            total += amplitude * GradientNoise(position.X * frequency, position.Z * frequency);
            frequency *= Lacunarity;
            amplitude *= Persistence;
        }

        // Gradient dots span [-2, 2], so the weighted sum maps to [0, 1] under this divisor.
        return Math.Clamp(0.5f + (total / (4f * AmplitudeSum)), 0f, 1f);
    }

    /// <inheritdoc/>
    public void EvaluateRow(WorldPosition2D start, float stepX, Span<float> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = EvaluateHeight(new WorldPosition2D(start.X + (index * stepX), start.Z));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GradientNoise(float x, float z)
    {
        int x0 = (int)MathF.Floor(x);
        int z0 = (int)MathF.Floor(z);
        float fx = x - x0;
        float fz = z - z0;

        float sx = fx * fx * fx * ((fx * ((fx * 6f) - 15f)) + 10f);
        float sz = fz * fz * fz * ((fz * ((fz * 6f) - 15f)) + 10f);

        float d00 = DotHash(x0, z0, fx, fz);
        float d10 = DotHash(x0 + 1, z0, fx - 1f, fz);
        float d01 = DotHash(x0, z0 + 1, fx, fz - 1f);
        float d11 = DotHash(x0 + 1, z0 + 1, fx - 1f, fz - 1f);

        float ix0 = d00 + ((d10 - d00) * sx);
        float ix1 = d01 + ((d11 - d01) * sx);
        return ix0 + ((ix1 - ix0) * sz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DotHash(int ix, int iz, float dx, float dz)
    {
        uint hash = ((uint)ix * 0x8DA6B343u) ^ ((uint)iz * 0xD8163841u);
        hash = (hash ^ (hash >> 15)) * 0x51E127E7u;
        hash ^= hash >> 16;

        return (hash & 3) switch
        {
            0 => dx + dz,
            1 => -dx + dz,
            2 => dx - dz,
            _ => -dx - dz,
        };
    }
}
