namespace Axrone.Numeric;

/// <summary>
/// Stateless, seedable gradient noise. The lattice permutation is derived from the seed
/// with a 64-bit avalanche hash instead of a 512-byte table, so the generator is
/// register-friendly, allocation-free and safe to share across threads.
/// </summary>
public readonly struct PerlinNoise : IEquatable<PerlinNoise>
{
    private const ulong XPrime = 0x9E3779B97F4A7C15UL;
    private const ulong YPrime = 0xC2B2AE3D27D4EB4FUL;
    private const ulong ZPrime = 0x165667B19E3779F9UL;

    /// <summary>Gets the avalanche seed.</summary>
    public ulong Seed { get; }

    /// <summary>Gets the fractal octave count.</summary>
    public int Octaves { get; }

    /// <summary>Gets the base frequency.</summary>
    public float Frequency { get; }

    /// <summary>Gets the amplitude persistence between octaves.</summary>
    public float Persistence { get; }

    /// <summary>Gets the frequency lacunarity between octaves.</summary>
    public float Lacunarity { get; }

    /// <summary>Gets the base amplitude.</summary>
    public float Amplitude { get; }

    /// <summary>Creates the default one-octave generator.</summary>
    public PerlinNoise()
        : this(0, 1, 1f, 0.5f, 2f, 1f)
    {
    }

    /// <summary>Creates a noise generator.</summary>
    /// <param name="seed">Lattice seed.</param>
    /// <param name="octaves">Fractal octaves in <c>[1, 8]</c>.</param>
    /// <param name="frequency">Base frequency, greater than zero.</param>
    /// <param name="persistence">Amplitude persistence in <c>(0, 1]</c>.</param>
    /// <param name="lacunarity">Frequency lacunarity, at least one.</param>
    /// <param name="amplitude">Base amplitude.</param>
    public PerlinNoise(
        ulong seed = 0,
        int octaves = 1,
        float frequency = 1f,
        float persistence = 0.5f,
        float lacunarity = 2f,
        float amplitude = 1f)
    {
        if (octaves is < 1 or > 8)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(octaves));
        }

        if (!float.IsFinite(frequency) || frequency <= 0f)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(frequency));
        }

        if (!float.IsFinite(persistence) || persistence <= 0f || persistence > 1f)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(persistence));
        }

        if (!float.IsFinite(lacunarity) || lacunarity < 1f)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(lacunarity));
        }

        if (!float.IsFinite(amplitude))
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(amplitude));
        }

        Seed = seed;
        Octaves = octaves;
        Frequency = frequency;
        Persistence = persistence;
        Lacunarity = lacunarity;
        Amplitude = amplitude;
    }

    /// <summary>Samples one-dimensional gradient noise in <c>[-1, 1]</c>.</summary>
    /// <param name="x">Sample coordinate.</param>
    /// <returns>The noise value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Sample1D(float x) => Noise1D(Seed, x);

    /// <summary>Samples two-dimensional gradient noise in <c>[-1, 1]</c>.</summary>
    /// <param name="x">Sample X.</param>
    /// <param name="y">Sample Y.</param>
    /// <returns>The noise value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Sample2D(float x, float y) => Noise2D(Seed, x, y);

    /// <summary>Samples three-dimensional gradient noise in <c>[-1, 1]</c>.</summary>
    /// <param name="x">Sample X.</param>
    /// <param name="y">Sample Y.</param>
    /// <param name="z">Sample Z.</param>
    /// <returns>The noise value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Sample3D(float x, float y, float z) => Noise3D(Seed, x, y, z);

    /// <summary>Samples fractal one-dimensional noise.</summary>
    /// <param name="x">Sample coordinate.</param>
    /// <returns>The summed octave value.</returns>
    public float Fbm1D(float x)
    {
        float value = 0f;
        float amplitude = Amplitude;
        float frequency = Frequency;
        for (var octave = 0; octave < Octaves; octave++)
        {
            value += Noise1D(Seed, x * frequency) * amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        return value;
    }

    /// <summary>Samples fractal two-dimensional noise.</summary>
    /// <param name="x">Sample X.</param>
    /// <param name="y">Sample Y.</param>
    /// <returns>The summed octave value.</returns>
    public float Fbm2D(float x, float y)
    {
        float value = 0f;
        float amplitude = Amplitude;
        float frequency = Frequency;
        for (var octave = 0; octave < Octaves; octave++)
        {
            value += Noise2D(Seed, x * frequency, y * frequency) * amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        return value;
    }

    /// <summary>Samples fractal three-dimensional noise.</summary>
    /// <param name="x">Sample X.</param>
    /// <param name="y">Sample Y.</param>
    /// <param name="z">Sample Z.</param>
    /// <returns>The summed octave value.</returns>
    public float Fbm3D(float x, float y, float z)
    {
        float value = 0f;
        float amplitude = Amplitude;
        float frequency = Frequency;
        for (var octave = 0; octave < Octaves; octave++)
        {
            value += Noise3D(Seed, x * frequency, y * frequency, z * frequency) * amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        return value;
    }

    /// <inheritdoc/>
    public bool Equals(PerlinNoise other) =>
        Seed == other.Seed &&
        Octaves == other.Octaves &&
        Frequency.Equals(other.Frequency) &&
        Persistence.Equals(other.Persistence) &&
        Lacunarity.Equals(other.Lacunarity) &&
        Amplitude.Equals(other.Amplitude);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PerlinNoise other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Seed, Octaves, Frequency, Persistence, Lacunarity, Amplitude);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(PerlinNoise left, PerlinNoise right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(PerlinNoise left, PerlinNoise right) => !left.Equals(right);

    private static float Noise1D(ulong seed, float x)
    {
        int lattice = FastFloor(x);
        float fraction = x - lattice;
        uint left = Hash(seed, lattice, 0, 0);
        uint right = Hash(seed, lattice + 1, 0, 0);
        float u = Fade(fraction);
        return Lerp(Gradient1D(left, fraction), Gradient1D(right, fraction - 1f), u);
    }

    private static float Noise2D(ulong seed, float x, float y)
    {
        int x0 = FastFloor(x);
        int y0 = FastFloor(y);
        float fx = x - x0;
        float fy = y - y0;
        float u = Fade(fx);
        float v = Fade(fy);

        float topLeft = Gradient2D(Hash(seed, x0, y0, 0), fx, fy);
        float topRight = Gradient2D(Hash(seed, x0 + 1, y0, 0), fx - 1f, fy);
        float bottomLeft = Gradient2D(Hash(seed, x0, y0 + 1, 0), fx, fy - 1f);
        float bottomRight = Gradient2D(Hash(seed, x0 + 1, y0 + 1, 0), fx - 1f, fy - 1f);

        return Lerp(Lerp(topLeft, topRight, u), Lerp(bottomLeft, bottomRight, u), v);
    }

    private static float Noise3D(ulong seed, float x, float y, float z)
    {
        int x0 = FastFloor(x);
        int y0 = FastFloor(y);
        int z0 = FastFloor(z);
        float fx = x - x0;
        float fy = y - y0;
        float fz = z - z0;
        float u = Fade(fx);
        float v = Fade(fy);
        float w = Fade(fz);

        float c000 = Gradient3D(Hash(seed, x0, y0, z0), fx, fy, fz);
        float c100 = Gradient3D(Hash(seed, x0 + 1, y0, z0), fx - 1f, fy, fz);
        float c010 = Gradient3D(Hash(seed, x0, y0 + 1, z0), fx, fy - 1f, fz);
        float c110 = Gradient3D(Hash(seed, x0 + 1, y0 + 1, z0), fx - 1f, fy - 1f, fz);
        float c001 = Gradient3D(Hash(seed, x0, y0, z0 + 1), fx, fy, fz - 1f);
        float c101 = Gradient3D(Hash(seed, x0 + 1, y0, z0 + 1), fx - 1f, fy, fz - 1f);
        float c011 = Gradient3D(Hash(seed, x0, y0 + 1, z0 + 1), fx, fy - 1f, fz - 1f);
        float c111 = Gradient3D(Hash(seed, x0 + 1, y0 + 1, z0 + 1), fx - 1f, fy - 1f, fz - 1f);

        float x00 = Lerp(c000, c100, u);
        float x10 = Lerp(c010, c110, u);
        float x01 = Lerp(c001, c101, u);
        float x11 = Lerp(c011, c111, u);
        return Lerp(Lerp(x00, x10, v), Lerp(x01, x11, v), w);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Hash(ulong seed, int x, int y, int z)
    {
        ulong hash = seed ^ ((ulong)(uint)x * XPrime) ^ ((ulong)(uint)y * YPrime) ^ ((ulong)(uint)z * ZPrime);
        hash ^= hash >> 33;
        hash *= 0xFF51AFD7ED558CCDUL;
        hash ^= hash >> 33;
        hash *= 0xC4CEB9FE1A85EC53UL;
        hash ^= hash >> 33;
        return (uint)hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Gradient1D(uint hash, float x) => (hash & 1u) == 0 ? x : -x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Gradient2D(uint hash, float x, float y) => (hash & 7u) switch
    {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        3 => -x - y,
        4 => x,
        5 => -x,
        6 => y,
        _ => -y,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Gradient3D(uint hash, float x, float y, float z) => (hash % 12u) switch
    {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        3 => -x - y,
        4 => x + z,
        5 => -x + z,
        6 => x - z,
        7 => -x - z,
        8 => y + z,
        9 => -y + z,
        10 => y - z,
        _ => -y - z,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Fade(float t) => t * t * t * ((t * ((t * 6f) - 15f)) + 10f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FastFloor(float value) => (int)MathF.Floor(value);
}
