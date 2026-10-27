namespace Axrone.Hash;

/// <summary>
/// Incremental Murmur3 32-bit accumulator. Four-byte blocks are mixed as they arrive; the
/// final tail is folded into the length and avalanche at digest time.
/// </summary>
public struct Murmur3_32Accumulator : IHashAccumulator<Murmur3_32Accumulator, Digest32>
{
    private readonly uint _seed;
    private uint _hash;
    private uint _length;
    private uint _tail;
    private int _tailCount;

    /// <summary>Creates an accumulator.</summary>
    /// <param name="seed">Initial seed; zero is the SMHasher default.</param>
    public Murmur3_32Accumulator(uint seed = 0)
    {
        _seed = seed;
        _hash = seed;
        _length = 0;
        _tail = 0;
        _tailCount = 0;
    }

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> source)
    {
        _length += (uint)source.Length;
        int index = 0;

        if (_tailCount > 0)
        {
            while (index < source.Length && _tailCount < 4)
            {
                _tail |= (uint)source[index++] << (_tailCount * 8);
                _tailCount++;
            }

            if (_tailCount == 4)
            {
                Mix(ref _hash, _tail);
                _tail = 0;
                _tailCount = 0;
            }
        }

        while (index + 4 <= source.Length)
        {
            Mix(ref _hash, BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(index, 4)));
            index += 4;
        }

        while (index < source.Length)
        {
            _tail |= (uint)source[index++] << (_tailCount * 8);
            _tailCount++;
        }
    }

    /// <inheritdoc/>
    public Digest32 GetDigest(bool reset = false)
    {
        uint hash = _hash;
        if (_tailCount > 0)
        {
            uint tail = _tail;
            tail *= 0xcc9e2d51u;
            tail = BitOperations.RotateLeft(tail, 15);
            tail *= 0x1b873593u;
            hash ^= tail;
        }

        hash ^= _length;
        hash ^= hash >> 16;
        hash *= 0x85ebca6bu;
        hash ^= hash >> 13;
        hash *= 0xc2b2ae35u;
        hash ^= hash >> 16;

        if (reset)
        {
            Reset();
        }

        return new Digest32(hash);
    }

    /// <inheritdoc/>
    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < Digest32.ByteCount)
        {
            bytesWritten = 0;
            return false;
        }

        BinaryPrimitives.WriteUInt32BigEndian(destination, GetDigest().Value);
        bytesWritten = Digest32.ByteCount;
        if (reset)
        {
            Reset();
        }

        return true;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _hash = _seed;
        _length = 0;
        _tail = 0;
        _tailCount = 0;
    }

    /// <inheritdoc/>
    public void Dispose() => Reset();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Mix(ref uint hash, uint value)
    {
        value *= 0xcc9e2d51u;
        value = BitOperations.RotateLeft(value, 15);
        value *= 0x1b873593u;
        hash ^= value;
        hash = BitOperations.RotateLeft(hash, 13);
        hash = (hash * 5u) + 0xe6546b64u;
    }
}

/// <summary>Murmur3 32-bit algorithm entry point.</summary>
public sealed class Murmur3_32Algorithm : IIncrementalHashAlgorithm<Murmur3_32Algorithm, Murmur3_32Accumulator, Digest32>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Murmur3_32;

    /// <inheritdoc/>
    public static Murmur3_32Accumulator CreateAccumulator() => new(0u);

    /// <inheritdoc/>
    public static Digest32 Hash(ReadOnlySpan<byte> source)
    {
        var accumulator = new Murmur3_32Accumulator(0u);
        accumulator.Append(source);
        return accumulator.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var accumulator = new Murmur3_32Accumulator(0u);
        accumulator.Append(source);
        accumulator.TryGetDigest(destination, out _);
    }
}
