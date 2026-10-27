namespace Axrone.Terrain;

/// <summary>
/// Deterministic fractional-Brownian-motion terrain generator. Identical
/// descriptors and options always produce identical heightmaps because the value
/// lattice is built from the seeded engine PRNG.
/// </summary>
public static class TerrainNoiseGenerator
{
    private const int LatticeSize = 256;
    private const int LatticeMask = LatticeSize - 1;

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
    public static TerrainHeightmap Generate(in TerrainDescriptor descriptor, in TerrainNoiseOptions options)
    {
        descriptor.Validate();
        options.Validate();

        ValueNoiseLattice lattice = CreateLattice(options.Seed);
        int resolution = descriptor.Resolution;
        var heights = new float[resolution * resolution];

        float amplitudeSum = 0f;
        float amplitude = 1f;
        for (var octave = 0; octave < options.Octaves; octave++)
        {
            amplitudeSum += amplitude;
            amplitude *= options.Persistence;
        }

        float lastGrid = resolution - 1;
        for (var gridZ = 0; gridZ < resolution; gridZ++)
        {
            float worldZ = ((gridZ / lastGrid) * descriptor.Length) + options.OffsetZ;

            for (var gridX = 0; gridX < resolution; gridX++)
            {
                float worldX = ((gridX / lastGrid) * descriptor.Width) + options.OffsetX;

                float total = 0f;
                float octaveAmplitude = 1f;
                float octaveFrequency = options.Frequency;

                for (var octave = 0; octave < options.Octaves; octave++)
                {
                    total += SampleValueNoise(lattice, worldX * octaveFrequency, worldZ * octaveFrequency) *
                             octaveAmplitude;
                    octaveAmplitude *= options.Persistence;
                    octaveFrequency *= options.Lacunarity;
                }

                heights[(gridZ * resolution) + gridX] = total / amplitudeSum;
            }
        }

        return TerrainHeightmap.FromRawHeights(resolution, heights);
    }

    private static ValueNoiseLattice CreateLattice(ulong seed)
    {
        var random = new RandomEngine<Xoroshiro128PlusPlus>(seed);
        var permutation = new byte[LatticeSize * 2];
        var values = new float[LatticeSize];
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
            permutation[index] = identity[index];
            permutation[index + LatticeSize] = identity[index];
            values[index] = random.NextSingle();
        }

        return new ValueNoiseLattice(permutation, values);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LatticeValueAt(in ValueNoiseLattice lattice, int x, int z)
    {
        int hashed = lattice.Permutation[lattice.Permutation[x & LatticeMask] + (z & LatticeMask)];
        return lattice.Values[hashed];
    }

    private static float SampleValueNoise(in ValueNoiseLattice lattice, float x, float z)
    {
        int x0 = (int)MathF.Floor(x);
        int z0 = (int)MathF.Floor(z);
        float tx = TerrainMath.Smoothstep(x - x0);
        float tz = TerrainMath.Smoothstep(z - z0);

        float topLeft = LatticeValueAt(lattice, x0, z0);
        float topRight = LatticeValueAt(lattice, x0 + 1, z0);
        float bottomLeft = LatticeValueAt(lattice, x0, z0 + 1);
        float bottomRight = LatticeValueAt(lattice, x0 + 1, z0 + 1);

        float top = topLeft + ((topRight - topLeft) * tx);
        float bottom = bottomLeft + ((bottomRight - bottomLeft) * tx);
        return top + ((bottom - top) * tz);
    }

    private readonly struct ValueNoiseLattice
    {
        public ValueNoiseLattice(byte[] permutation, float[] values)
        {
            Permutation = permutation;
            Values = values;
        }

        public byte[] Permutation { get; }

        public float[] Values { get; }
    }
}
