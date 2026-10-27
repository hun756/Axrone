namespace Axrone.Hash;

/// <summary>
/// Incremental xxHash32 accumulator. Input is consumed in 16-byte blocks with a small
/// carry buffer; the tail and total length are folded in with the standard avalanche.
/// </summary>
public struct XxHash32Accumulator : IHashAccumulator<XxHash32Accumulator, Digest32>
{
    private const uint Prime1 = 0x9E3779B1u;
    private const uint Prime2 = 0x85EBCA77u;
    private const uint Prime3 = 0xC2B2AE3Du;
    private const uint Prime4 = 0x27D4EB2Fu;
    private const uint Prime5 = 0x165667B1u;

    private readonly uint _seed;
    private uint _v1;
    private uint _v2;
    private uint _v3;
    private uint _v4;
    private ulong _totalLength;
    private byte[]? _memory;
    private int _memorySize;

    /// <summary>Creates an accumulator.</summary>
    /// <param name="seed">Initial seed; zero is the xxHash default.</param>
    public XxHash32Accumulator(uint seed = 0)
    {
        _seed = seed;
        _v1 = seed + Prime1 + Prime2;
        _v2 = seed + Prime2;
        _v3 = seed;
        _v4 = seed - Prime1;
        _totalLength = 0;
        _memory = new byte[16];
        _memorySize = 0;
    }

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> source)
    {
        _memory ??= new byte[16];
        _totalLength += (ulong)source.Length;

        int index = 0;
        if (_memorySize > 0)
        {
            int toCopy = Math.Min(source.Length, 16 - _memorySize);
            source[..toCopy].CopyTo(_memory.AsSpan(_memorySize));
            _memorySize += toCopy;
            index += toCopy;

            if (_memorySize == 16)
            {
                ProcessBlock(_memory);
                _memorySize = 0;
            }
        }

        while (index + 16 <= source.Length)
        {
            ProcessBlock(source.Slice(index, 16));
            index += 16;
        }

        if (index < source.Length)
        {
            source[index..].CopyTo(_memory);
            _memorySize = source.Length - index;
        }
    }

    /// <inheritdoc/>
    public Digest32 GetDigest(bool reset = false)
    {
        uint hash = _totalLength >= 16
            ? BitOperations.RotateLeft(_v1, 1) +
              BitOperations.RotateLeft(_v2, 7) +
              BitOperations.RotateLeft(_v3, 12) +
              BitOperations.RotateLeft(_v4, 18)
            : _seed + Prime5;

        hash += (uint)_totalLength;

        int index = 0;
        ReadOnlySpan<byte> memory = _memory is null ? default : _memory.AsSpan(0, _memorySize);
        while (index + 4 <= memory.Length)
        {
            hash += BinaryPrimitives.ReadUInt32LittleEndian(memory.Slice(index, 4)) * Prime3;
            hash = BitOperations.RotateLeft(hash, 17) * Prime4;
            index += 4;
        }

        while (index < memory.Length)
        {
            hash += memory[index] * Prime5;
            hash = BitOperations.RotateLeft(hash, 11) * Prime1;
            index++;
        }

        hash ^= hash >> 15;
        hash *= Prime2;
        hash ^= hash >> 13;
        hash *= Prime3;
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
        _v1 = _seed + Prime1 + Prime2;
        _v2 = _seed + Prime2;
        _v3 = _seed;
        _v4 = _seed - Prime1;
        _totalLength = 0;
        _memorySize = 0;
        if (_memory is not null)
        {
            Array.Clear(_memory);
        }
    }

    /// <inheritdoc/>
    public void Dispose() => Reset();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBlock(ReadOnlySpan<byte> block)
    {
        _v1 = Round(_v1, BinaryPrimitives.ReadUInt32LittleEndian(block));
        _v2 = Round(_v2, BinaryPrimitives.ReadUInt32LittleEndian(block[4..]));
        _v3 = Round(_v3, BinaryPrimitives.ReadUInt32LittleEndian(block[8..]));
        _v4 = Round(_v4, BinaryPrimitives.ReadUInt32LittleEndian(block[12..]));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Round(uint accumulator, uint input)
    {
        accumulator += input * Prime2;
        accumulator = BitOperations.RotateLeft(accumulator, 13);
        accumulator *= Prime1;
        return accumulator;
    }
}

/// <summary>xxHash32 algorithm entry point.</summary>
public sealed class XxHash32Algorithm : IIncrementalHashAlgorithm<XxHash32Algorithm, XxHash32Accumulator, Digest32>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.XxHash32;

    /// <inheritdoc/>
    public static XxHash32Accumulator CreateAccumulator() => new(0u);

    /// <inheritdoc/>
    public static Digest32 Hash(ReadOnlySpan<byte> source)
    {
        var accumulator = new XxHash32Accumulator(0u);
        accumulator.Append(source);
        return accumulator.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var accumulator = new XxHash32Accumulator(0u);
        accumulator.Append(source);
        accumulator.TryGetDigest(destination, out _);
    }
}
