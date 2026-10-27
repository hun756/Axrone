namespace Axrone.Hash;

/// <summary>
/// CRC-32 accumulator. The state starts at <c>seed ^ 0xFFFFFFFF</c> and the digest
/// complements the running state, matching the IEEE/zlib convention; the seedable
/// constructor keeps the TS engine's contract for checksum domains.
/// </summary>
public struct Crc32Accumulator : IHashAccumulator<Crc32Accumulator, Digest32>
{
    private readonly uint _seed;
    private uint _hash;

    /// <summary>Creates an accumulator.</summary>
    /// <param name="seed">Initial seed; zero is the standard CRC-32 state.</param>
    public Crc32Accumulator(uint seed = 0)
    {
        _seed = seed;
        _hash = seed ^ 0xFFFFFFFFu;
    }

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> source)
    {
        uint hash = _hash;
        ReadOnlySpan<uint> table = Crc32Table.Value;
        for (int i = 0; i < source.Length; i++)
        {
            hash = (hash >> 8) ^ table[(int)((hash ^ source[i]) & 0xFF)];
        }

        _hash = hash;
    }

    /// <inheritdoc/>
    public Digest32 GetDigest(bool reset = false)
    {
        Digest32 digest = new(_hash ^ 0xFFFFFFFFu);
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

        BinaryPrimitives.WriteUInt32BigEndian(destination, _hash ^ 0xFFFFFFFFu);
        bytesWritten = Digest32.ByteCount;
        if (reset)
        {
            Reset();
        }

        return true;
    }

    /// <inheritdoc/>
    public void Reset() => _hash = _seed ^ 0xFFFFFFFFu;

    /// <inheritdoc/>
    public void Dispose() => Reset();

    private static class Crc32Table
    {
        internal static readonly uint[] Value = Create(0xEDB88320u);

        private static uint[] Create(uint polynomial)
        {
            var table = new uint[256];
            for (var i = 0; i < 256; i++)
            {
                uint c = (uint)i;
                for (var bit = 0; bit < 8; bit++)
                {
                    c = (c & 1) != 0 ? polynomial ^ (c >> 1) : c >> 1;
                }

                table[i] = c;
            }

            return table;
        }
    }
}

/// <summary>CRC-32 (IEEE 802.3) algorithm entry point.</summary>
public sealed class Crc32Algorithm : IIncrementalHashAlgorithm<Crc32Algorithm, Crc32Accumulator, Digest32>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Crc32;

    /// <inheritdoc/>
    public static Crc32Accumulator CreateAccumulator() => new(0u);

    /// <inheritdoc/>
    public static Digest32 Hash(ReadOnlySpan<byte> source)
    {
        var accumulator = new Crc32Accumulator(0u);
        accumulator.Append(source);
        return accumulator.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var accumulator = new Crc32Accumulator(0u);
        accumulator.Append(source);
        accumulator.TryGetDigest(destination, out _);
    }
}

/// <summary>CRC-32C (Castagnoli) accumulator.</summary>
public struct Crc32cAccumulator : IHashAccumulator<Crc32cAccumulator, Digest32>
{
    private readonly uint _seed;
    private uint _hash;

    /// <summary>Creates an accumulator.</summary>
    /// <param name="seed">Initial seed; zero is the standard CRC-32C state.</param>
    public Crc32cAccumulator(uint seed = 0)
    {
        _seed = seed;
        _hash = seed ^ 0xFFFFFFFFu;
    }

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> source)
    {
        uint hash = _hash;
        ReadOnlySpan<uint> table = Crc32cTable.Value;
        for (int i = 0; i < source.Length; i++)
        {
            hash = (hash >> 8) ^ table[(int)((hash ^ source[i]) & 0xFF)];
        }

        _hash = hash;
    }

    /// <inheritdoc/>
    public Digest32 GetDigest(bool reset = false)
    {
        Digest32 digest = new(_hash ^ 0xFFFFFFFFu);
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

        BinaryPrimitives.WriteUInt32BigEndian(destination, _hash ^ 0xFFFFFFFFu);
        bytesWritten = Digest32.ByteCount;
        if (reset)
        {
            Reset();
        }

        return true;
    }

    /// <inheritdoc/>
    public void Reset() => _hash = _seed ^ 0xFFFFFFFFu;

    /// <inheritdoc/>
    public void Dispose() => Reset();

    private static class Crc32cTable
    {
        internal static readonly uint[] Value = Create(0x82F63B78u);

        private static uint[] Create(uint polynomial)
        {
            var table = new uint[256];
            for (var i = 0; i < 256; i++)
            {
                uint c = (uint)i;
                for (var bit = 0; bit < 8; bit++)
                {
                    c = (c & 1) != 0 ? polynomial ^ (c >> 1) : c >> 1;
                }

                table[i] = c;
            }

            return table;
        }
    }
}

/// <summary>CRC-32C (Castagnoli) algorithm entry point.</summary>
public sealed class Crc32cAlgorithm : IIncrementalHashAlgorithm<Crc32cAlgorithm, Crc32cAccumulator, Digest32>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Crc32c;

    /// <inheritdoc/>
    public static Crc32cAccumulator CreateAccumulator() => new(0u);

    /// <inheritdoc/>
    public static Digest32 Hash(ReadOnlySpan<byte> source)
    {
        var accumulator = new Crc32cAccumulator(0u);
        accumulator.Append(source);
        return accumulator.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var accumulator = new Crc32cAccumulator(0u);
        accumulator.Append(source);
        accumulator.TryGetDigest(destination, out _);
    }
}
