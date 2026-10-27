namespace Axrone.Terrain;

/// <summary>
/// Continuous height field sampled in world space. Implementations are read-only after
/// construction, so a single instance can back concurrent chunk generation.
/// </summary>
public interface ITerrainSurface
{
    /// <summary>Samples the normalized height at a world-space XZ position.</summary>
    /// <param name="worldX">World X.</param>
    /// <param name="worldZ">World Z.</param>
    /// <returns>A normalized height in <c>[0, 1]</c>.</returns>
    float SampleHeight(float worldX, float worldZ);

    /// <summary>Samples a straight row of normalized heights.</summary>
    /// <param name="startX">World X of the first sample.</param>
    /// <param name="worldZ">World Z shared by every sample.</param>
    /// <param name="stepX">World-space distance between samples.</param>
    /// <param name="destination">Destination row.</param>
    void SampleRow(float startX, float worldZ, float stepX, Span<float> destination);
}

/// <summary>
/// Deterministic value-noise field using a seeded 256-cell lattice and fractional
/// Brownian motion. The same options always produce the same field, and the field can be
/// sampled at arbitrary world positions, which is what chunk streaming needs.
/// </summary>
public sealed class TerrainNoiseField : ITerrainSurface
{
    private const int LatticeSize = 256;
    private const int LatticeMask = LatticeSize - 1;

    private readonly byte[] _permutation;
    private readonly float[] _values;
    private readonly TerrainNoiseOptions _options;
    private readonly float _amplitudeSum;

    /// <summary>Builds a field from the supplied noise options.</summary>
    /// <param name="options">Noise parameters; a default value selects the documented defaults.</param>
    /// <exception cref="TerrainException">A parameter is outside its documented domain.</exception>
    public TerrainNoiseField(in TerrainNoiseOptions options)
    {
        options.Validate();
        _options = options;

        var random = new RandomEngine<Xoroshiro128PlusPlus>(options.Seed);
        _permutation = new byte[LatticeSize * 2];
        _values = new float[LatticeSize];
        var identity = new byte[LatticeSize];

        for (var index = 0; index < LatticeSize; index++)
        {
            identity[index] = (byte)index;
        }

        for (var index = LatticeSize - 1; index > 0; index--)
        {
            int swapIndex = random.NextInt32(0, index + 1);
            (identity[index], identity[swapIndex]) = (identity[swapIndex], identity[index]);
        }

        for (var index = 0; index < LatticeSize; index++)
        {
            _permutation[index] = identity[index];
            _permutation[index + LatticeSize] = identity[index];
            _values[index] = random.NextSingle();
        }

        float amplitudeSum = 0f;
        float amplitude = 1f;
        for (var octave = 0; octave < options.Octaves; octave++)
        {
            amplitudeSum += amplitude;
            amplitude *= options.Persistence;
        }

        _amplitudeSum = amplitudeSum;
    }

    /// <summary>Gets the options this field was built with.</summary>
    public TerrainNoiseOptions Options => _options;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float SampleHeight(float worldX, float worldZ)
    {
        float x = worldX + _options.OffsetX;
        float z = worldZ + _options.OffsetZ;

        float total = 0f;
        float octaveAmplitude = 1f;
        float octaveFrequency = _options.Frequency;

        for (var octave = 0; octave < _options.Octaves; octave++)
        {
            total += SampleValueNoise(x * octaveFrequency, z * octaveFrequency) * octaveAmplitude;
            octaveAmplitude *= _options.Persistence;
            octaveFrequency *= _options.Lacunarity;
        }

        return total / _amplitudeSum;
    }

    /// <inheritdoc/>
    public void SampleRow(float startX, float worldZ, float stepX, Span<float> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = SampleHeight(startX + (index * stepX), worldZ);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float LatticeValueAt(int x, int z)
    {
        int hashed = _permutation[_permutation[x & LatticeMask] + (z & LatticeMask)];
        return _values[hashed];
    }

    private float SampleValueNoise(float x, float z)
    {
        int x0 = (int)MathF.Floor(x);
        int z0 = (int)MathF.Floor(z);
        float tx = TerrainMath.Smoothstep(x - x0);
        float tz = TerrainMath.Smoothstep(z - z0);

        float topLeft = LatticeValueAt(x0, z0);
        float topRight = LatticeValueAt(x0 + 1, z0);
        float bottomLeft = LatticeValueAt(x0, z0 + 1);
        float bottomRight = LatticeValueAt(x0 + 1, z0 + 1);

        float top = topLeft + ((topRight - topLeft) * tx);
        float bottom = bottomLeft + ((bottomRight - bottomLeft) * tx);
        return top + ((bottom - top) * tz);
    }
}
