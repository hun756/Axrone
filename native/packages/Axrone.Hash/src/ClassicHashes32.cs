namespace Axrone.Hash;

/// <summary>DJB2 accumulator (<c>hash * 33 + byte</c>), initialised to 5381.</summary>
public struct Djb2Accumulator : IHashAccumulator<Djb2Accumulator, Digest32>
{
    private uint _hash;

    /// <summary>Creates an accumulator.</summary>
    public Djb2Accumulator() => _hash = 5381u;

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> source)
    {
        uint hash = _hash;
        for (int i = 0; i < source.Length; i++)
        {
            hash = (hash * 33u) + source[i];
        }

        _hash = hash;
    }

    /// <inheritdoc/>
    public Digest32 GetDigest(bool reset = false)
    {
        Digest32 digest = new(_hash);
        if (reset)
        {
            Reset();
        }

        return digest;
    }

    /// <inheritdoc/>
    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < Digest32.ByteCount)
        {
            bytesWritten = 0;
            return false;
        }

        BinaryPrimitives.WriteUInt32BigEndian(destination, _hash);
        bytesWritten = Digest32.ByteCount;
        if (reset)
        {
            Reset();
        }

        return true;
    }

    /// <inheritdoc/>
    public void Reset() => _hash = 5381u;

    /// <inheritdoc/>
    public void Dispose() => Reset();
}

/// <summary>DJB2 algorithm entry point.</summary>
public sealed class Djb2Algorithm : IIncrementalHashAlgorithm<Djb2Algorithm, Djb2Accumulator, Digest32>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Djb2;

    /// <inheritdoc/>
    public static Djb2Accumulator CreateAccumulator() => new();

    /// <inheritdoc/>
    public static Digest32 Hash(ReadOnlySpan<byte> source)
    {
        var accumulator = new Djb2Accumulator();
        accumulator.Append(source);
        return accumulator.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var accumulator = new Djb2Accumulator();
        accumulator.Append(source);
        accumulator.TryGetDigest(destination, out _);
    }
}

/// <summary>DJB2a accumulator (<c>hash * 33 ^ byte</c>), initialised to 5381.</summary>
public struct Djb2aAccumulator : IHashAccumulator<Djb2aAccumulator, Digest32>
{
    private uint _hash;

    /// <summary>Creates an accumulator.</summary>
    public Djb2aAccumulator() => _hash = 5381u;

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> source)
    {
        uint hash = _hash;
        for (int i = 0; i < source.Length; i++)
        {
            hash = (hash ^ source[i]) * 33u;
        }

        _hash = hash;
    }

    /// <inheritdoc/>
    public Digest32 GetDigest(bool reset = false)
    {
        Digest32 digest = new(_hash);
        if (reset)
        {
            Reset();
        }

        return digest;
    }

    /// <inheritdoc/>
    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < Digest32.ByteCount)
        {
            bytesWritten = 0;
            return false;
        }

        BinaryPrimitives.WriteUInt32BigEndian(destination, _hash);
        bytesWritten = Digest32.ByteCount;
        if (reset)
        {
            Reset();
        }

        return true;
    }

    /// <inheritdoc/>
    public void Reset() => _hash = 5381u;

    /// <inheritdoc/>
    public void Dispose() => Reset();
}

/// <summary>DJB2a algorithm entry point.</summary>
public sealed class Djb2aAlgorithm : IIncrementalHashAlgorithm<Djb2aAlgorithm, Djb2aAccumulator, Digest32>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Djb2a;

    /// <inheritdoc/>
    public static Djb2aAccumulator CreateAccumulator() => new();

    /// <inheritdoc/>
    public static Digest32 Hash(ReadOnlySpan<byte> source)
    {
        var accumulator = new Djb2aAccumulator();
        accumulator.Append(source);
        return accumulator.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var accumulator = new Djb2aAccumulator();
        accumulator.Append(source);
        accumulator.TryGetDigest(destination, out _);
    }
}

/// <summary>SDBM accumulator (<c>byte + (hash &lt;&lt; 6) + (hash &lt;&lt; 16) - hash</c>).</summary>
public struct SdbmAccumulator : IHashAccumulator<SdbmAccumulator, Digest32>
{
    private uint _hash;

    /// <summary>Creates an accumulator.</summary>
    public SdbmAccumulator() => _hash = 0u;

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> source)
    {
        uint hash = _hash;
        for (int i = 0; i < source.Length; i++)
        {
            hash = source[i] + (hash << 6) + (hash << 16) - hash;
        }

        _hash = hash;
    }

    /// <inheritdoc/>
    public Digest32 GetDigest(bool reset = false)
    {
        Digest32 digest = new(_hash);
        if (reset)
        {
            Reset();
        }

        return digest;
    }

    /// <inheritdoc/>
    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < Digest32.ByteCount)
        {
            bytesWritten = 0;
            return false;
        }

        BinaryPrimitives.WriteUInt32BigEndian(destination, _hash);
        bytesWritten = Digest32.ByteCount;
        if (reset)
        {
            Reset();
        }

        return true;
    }

    /// <inheritdoc/>
    public void Reset() => _hash = 0u;

    /// <inheritdoc/>
    public void Dispose() => Reset();
}

/// <summary>SDBM algorithm entry point.</summary>
public sealed class SdbmAlgorithm : IIncrementalHashAlgorithm<SdbmAlgorithm, SdbmAccumulator, Digest32>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Sdbm;

    /// <inheritdoc/>
    public static SdbmAccumulator CreateAccumulator() => new();

    /// <inheritdoc/>
    public static Digest32 Hash(ReadOnlySpan<byte> source)
    {
        var accumulator = new SdbmAccumulator();
        accumulator.Append(source);
        return accumulator.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var accumulator = new SdbmAccumulator();
        accumulator.Append(source);
        accumulator.TryGetDigest(destination, out _);
    }
}

/// <summary>FNV-1 32 accumulator: multiply by the prime, then XOR the input octet.</summary>
public struct Fnv1_32Accumulator : IHashAccumulator<Fnv1_32Accumulator, Digest32>
{
    /// <summary>32-bit FNV offset basis.</summary>
    public const uint OffsetBasis = 2166136261u;

    /// <summary>32-bit FNV prime.</summary>
    public const uint Prime = 16777619u;

    private readonly uint _seed;
    private uint _hash;

    /// <summary>Creates an accumulator.</summary>
    /// <param name="seed">Initial state; defaults to the FNV offset basis.</param>
    public Fnv1_32Accumulator(uint seed = OffsetBasis)
    {
        _seed = seed;
        _hash = seed;
    }

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> source)
    {
        uint hash = _hash;
        for (int i = 0; i < source.Length; i++)
        {
            hash = (hash * Prime) ^ source[i];
        }

        _hash = hash;
    }

    /// <inheritdoc/>
    public Digest32 GetDigest(bool reset = false)
    {
        Digest32 digest = new(_hash);
        if (reset)
        {
            Reset();
        }

        return digest;
    }

    /// <inheritdoc/>
    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < Digest32.ByteCount)
        {
            bytesWritten = 0;
            return false;
        }

        BinaryPrimitives.WriteUInt32BigEndian(destination, _hash);
        bytesWritten = Digest32.ByteCount;
        if (reset)
        {
            Reset();
        }

        return true;
    }

    /// <inheritdoc/>
    public void Reset() => _hash = _seed;

    /// <inheritdoc/>
    public void Dispose() => Reset();
}

/// <summary>FNV-1 32 algorithm entry point.</summary>
public sealed class Fnv1_32Algorithm : IIncrementalHashAlgorithm<Fnv1_32Algorithm, Fnv1_32Accumulator, Digest32>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Fnv1_32;

    /// <inheritdoc/>
    public static Fnv1_32Accumulator CreateAccumulator() => new(Fnv1_32Accumulator.OffsetBasis);

    /// <inheritdoc/>
    public static Digest32 Hash(ReadOnlySpan<byte> source)
    {
        var accumulator = new Fnv1_32Accumulator(Fnv1_32Accumulator.OffsetBasis);
        accumulator.Append(source);
        return accumulator.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var accumulator = new Fnv1_32Accumulator(Fnv1_32Accumulator.OffsetBasis);
        accumulator.Append(source);
        accumulator.TryGetDigest(destination, out _);
    }
}
