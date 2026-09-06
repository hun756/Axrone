namespace Axrone.Hash;

public static class WyHash64
{
    private const ulong P0 = 0x2d358dccaa6c78a5UL;
    private const ulong P1 = 0x8bbbc9096f504362UL;
    private const ulong P2 = 0x4b33a62ed433d4a3UL;
    private const ulong P3 = 0x4d5a2da51de1aa47UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mum(ulong a, ulong b)
    {
        ulong hi = Math.BigMul(a, b, out ulong lo);
        return lo ^ hi;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Read4(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt32LittleEndian(p);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Read8(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt64LittleEndian(p);

    public static ulong Compute(ReadOnlySpan<byte> data, ulong seed = 0)
    {
        ulong a = 0, b = 0;
        seed ^= Mum(seed ^ P0, P1);
        int len = data.Length;

        if (len <= 16)
        {
            if (len >= 4)
            {
                int subOffset = (len >> 3) << 2;
                a = (Read4(data) << 32) | Read4(data[subOffset..]);
                b = (Read4(data[(len - 4)..]) << 32) | Read4(data[(len - 4 - subOffset)..]);
            }
            else if (len > 0)
            {
                a = ((ulong)data[0] << 16) | ((ulong)data[len >> 1] << 8) | data[len - 1];
            }
        }
        else
        {
            int l = len;
            ReadOnlySpan<byte> ptr = data;
            if (l > 48)
            {
                ulong see1 = seed, see2 = seed;
                do
                {
                    seed = Mum(Read8(ptr) ^ P1, Read8(ptr[8..]) ^ seed);
                    see1 = Mum(Read8(ptr[16..]) ^ P2, Read8(ptr[24..]) ^ see1);
                    see2 = Mum(Read8(ptr[32..]) ^ P3, Read8(ptr[40..]) ^ see2);
                    ptr = ptr[48..];
                    l -= 48;
                } while (l > 48);
                seed ^= see1 ^ see2;
            }
            while (l > 16)
            {
                seed = Mum(Read8(ptr) ^ P1, Read8(ptr[8..]) ^ seed);
                l -= 16;
                ptr = ptr[16..];
            }
            a = Read8(data[(len - 16)..]);
            b = Read8(data[(len - 8)..]);
        }

        a ^= P1;
        b ^= seed;
        ulong hi = Math.BigMul(a, b, out ulong lo);
        a = lo ^ P0 ^ (ulong)len;
        b = hi ^ P1;
        return Mum(a, b);
    }
}

public struct WyHash64Accumulator : IHashAccumulator<WyHash64Accumulator, Digest64>
{
    private byte[]? _buffer;
    private int _length;
    private readonly ulong _seed;

    public WyHash64Accumulator(ulong seed = 0) { _seed = seed; }

    public void Append(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty) return;
        EnsureCapacity(_length + source.Length);
        source.CopyTo(_buffer.AsSpan(_length));
        _length += source.Length;
    }

    private void EnsureCapacity(int required)
    {
        if (_buffer == null) { _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(required, 256)); return; }
        if (_buffer.Length < required)
        {
            byte[] next = ArrayPool<byte>.Shared.Rent(Math.Max(required, _buffer.Length * 2));
            _buffer.AsSpan(0, _length).CopyTo(next);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = next;
        }
    }

    public Digest64 GetDigest(bool reset = false)
    {
        ReadOnlySpan<byte> span = _buffer == null ? ReadOnlySpan<byte>.Empty : _buffer.AsSpan(0, _length);
        ulong hash = WyHash64.Compute(span, _seed);
        if (reset) Reset();
        return new Digest64(hash);
    }

    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < 8) { bytesWritten = 0; return false; }
        BinaryPrimitives.WriteUInt64BigEndian(destination, GetDigest(reset).Value);
        bytesWritten = 8;
        return true;
    }

    public void Reset() => _length = 0;

    public void Dispose()
    {
        if (_buffer != null) { ArrayPool<byte>.Shared.Return(_buffer); _buffer = null; }
        _length = 0;
    }
}

public sealed class WyHash64Algorithm : IIncrementalHashAlgorithm<WyHash64Algorithm, WyHash64Accumulator, Digest64>
{
    public static HashAlgorithmId Id => HashAlgorithmId.WyHash64;
    public static WyHash64Accumulator CreateAccumulator() => new(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Digest64 Hash(ReadOnlySpan<byte> source) => new(WyHash64.Compute(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination) =>
        BinaryPrimitives.WriteUInt64BigEndian(destination, WyHash64.Compute(source));
}

public struct XxHash64Accumulator : IHashAccumulator<XxHash64Accumulator, Digest64>
{
    private const ulong Prime1 = 0x9E3779B185EBCA87UL;
    private const ulong Prime2 = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Prime3 = 0x165667B19E3779F9UL;
    private const ulong Prime4 = 0x85EBCA77C2B2AE63UL;
    private const ulong Prime5 = 0x27D4EB2F165667C5UL;

    private readonly ulong _seed;
    private ulong _v1, _v2, _v3, _v4;
    private ulong _totalLength;

    [InlineArray(32)]
    private struct Buffer32 { private byte _b; }
    private Buffer32 _buffer;
    private int _bufferSize;

    public XxHash64Accumulator(ulong seed = 0) { _seed = seed; _buffer = default; Reset(); }

    public void Reset()
    {
        _v1 = _seed + Prime1 + Prime2;
        _v2 = _seed + Prime2;
        _v3 = _seed;
        _v4 = _seed - Prime1;
        _totalLength = 0;
        _bufferSize = 0;
    }

    public void Append(ReadOnlySpan<byte> source)
    {
        _totalLength += (ulong)source.Length;

        if (_bufferSize > 0)
        {
            int toCopy = Math.Min(32 - _bufferSize, source.Length);
            Span<byte> dst = MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _buffer[0]), 32).Slice(_bufferSize);
            source[..toCopy].CopyTo(dst);
            _bufferSize += toCopy;
            source = source[toCopy..];

            if (_bufferSize == 32)
            {
                ReadOnlySpan<byte> blk = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _buffer[0]), 32);
                ProcessStripe(blk);
                _bufferSize = 0;
            }
            else return;
        }

        while (source.Length >= 32)
        {
            ProcessStripe(source[..32]);
            source = source[32..];
        }

        if (source.Length > 0)
        {
            source.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _buffer[0]), 32));
            _bufferSize = source.Length;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessStripe(ReadOnlySpan<byte> stripe)
    {
        _v1 = Round(_v1, BinaryPrimitives.ReadUInt64LittleEndian(stripe));
        _v2 = Round(_v2, BinaryPrimitives.ReadUInt64LittleEndian(stripe[8..]));
        _v3 = Round(_v3, BinaryPrimitives.ReadUInt64LittleEndian(stripe[16..]));
        _v4 = Round(_v4, BinaryPrimitives.ReadUInt64LittleEndian(stripe[24..]));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Round(ulong acc, ulong input) =>
        BitOperations.RotateLeft(acc + (input * Prime2), 31) * Prime1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MergeRound(ulong acc, ulong val) =>
        (acc ^ Round(0, val)) * Prime1 + Prime4;

    public Digest64 GetDigest(bool reset = false)
    {
        ulong h64 = FinalizeHash();
        if (reset) Reset();
        return new Digest64(h64);
    }

    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < 8) { bytesWritten = 0; return false; }
        BinaryPrimitives.WriteUInt64BigEndian(destination, FinalizeHash());
        bytesWritten = 8;
        if (reset) Reset();
        return true;
    }

    private ulong FinalizeHash()
    {
        ulong h64;
        if (_totalLength >= 32)
        {
            h64 = BitOperations.RotateLeft(_v1, 1) + BitOperations.RotateLeft(_v2, 7) +
                  BitOperations.RotateLeft(_v3, 12) + BitOperations.RotateLeft(_v4, 18);
            h64 = MergeRound(h64, _v1);
            h64 = MergeRound(h64, _v2);
            h64 = MergeRound(h64, _v3);
            h64 = MergeRound(h64, _v4);
        }
        else h64 = _seed + Prime5;

        h64 += _totalLength;

        ReadOnlySpan<byte> remaining = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _buffer[0]), _bufferSize);
        while (remaining.Length >= 8)
        {
            ulong k1 = Round(0, BinaryPrimitives.ReadUInt64LittleEndian(remaining));
            h64 ^= k1;
            h64 = BitOperations.RotateLeft(h64, 27) * Prime1 + Prime4;
            remaining = remaining[8..];
        }

        if (remaining.Length >= 4)
        {
            h64 ^= BinaryPrimitives.ReadUInt32LittleEndian(remaining) * Prime1;
            h64 = BitOperations.RotateLeft(h64, 23) * Prime2 + Prime3;
            remaining = remaining[4..];
        }

        while (remaining.Length > 0)
        {
            h64 ^= remaining[0] * Prime5;
            h64 = BitOperations.RotateLeft(h64, 11) * Prime1;
            remaining = remaining[1..];
        }

        h64 ^= h64 >> 33;
        h64 *= Prime2;
        h64 ^= h64 >> 29;
        h64 *= Prime3;
        h64 ^= h64 >> 32;

        return h64;
    }

    public void Dispose() => Reset();
}

public sealed class XxHash64Algorithm : IIncrementalHashAlgorithm<XxHash64Algorithm, XxHash64Accumulator, Digest64>
{
    public static HashAlgorithmId Id => HashAlgorithmId.XxHash64;
    public static XxHash64Accumulator CreateAccumulator() => new(0);

    public static Digest64 Hash(ReadOnlySpan<byte> source)
    {
        XxHash64Accumulator acc = new(0);
        acc.Append(source);
        return acc.GetDigest();
    }

    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        XxHash64Accumulator acc = new(0);
        acc.Append(source);
        acc.TryGetDigest(destination, out _);
    }
}

public struct Murmur3_128Accumulator : IHashAccumulator<Murmur3_128Accumulator, Digest128>
{
    private const ulong C1 = 0x87c37b91114253d5UL;
    private const ulong C2 = 0x4cf5ad432745937fUL;

    private readonly ulong _seed;
    private ulong _h1, _h2;
    private ulong _totalLength;

    [InlineArray(16)]
    private struct Buffer16 { private byte _b; }
    private Buffer16 _buffer;
    private int _bufferSize;

    public Murmur3_128Accumulator(ulong seed = 0) { _seed = seed; _buffer = default; Reset(); }

    public void Reset() { _h1 = _seed; _h2 = _seed; _totalLength = 0; _bufferSize = 0; }

    public void Append(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty) return;
        _totalLength += (ulong)source.Length;

        if (_bufferSize > 0)
        {
            int toCopy = Math.Min(16 - _bufferSize, source.Length);
            Span<byte> dst = MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _buffer[0]), 16).Slice(_bufferSize);
            source[..toCopy].CopyTo(dst);
            _bufferSize += toCopy;
            source = source[toCopy..];

            if (_bufferSize == 16)
            {
                ReadOnlySpan<byte> blk = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _buffer[0]), 16);
                ProcessBlock(BinaryPrimitives.ReadUInt64LittleEndian(blk), BinaryPrimitives.ReadUInt64LittleEndian(blk[8..]));
                _bufferSize = 0;
            }
            else return;
        }

        while (source.Length >= 16)
        {
            ProcessBlock(BinaryPrimitives.ReadUInt64LittleEndian(source), BinaryPrimitives.ReadUInt64LittleEndian(source[8..]));
            source = source[16..];
        }

        if (source.Length > 0)
        {
            source.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _buffer[0]), 16));
            _bufferSize = source.Length;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBlock(ulong k1, ulong k2)
    {
        ulong k1r = k1 * C1; k1r = BitOperations.RotateLeft(k1r, 31); k1r *= C2; _h1 ^= k1r;
        _h1 = BitOperations.RotateLeft(_h1, 27); _h1 += _h2; _h1 = (_h1 * 5) + 0x52dce729UL;

        ulong k2r = k2 * C2; k2r = BitOperations.RotateLeft(k2r, 33); k2r *= C1; _h2 ^= k2r;
        _h2 = BitOperations.RotateLeft(_h2, 31); _h2 += _h1; _h2 = (_h2 * 5) + 0x38495ab5UL;
    }

    public Digest128 GetDigest(bool reset = false)
    {
        FinalizeState(out ulong r1, out ulong r2);
        if (reset) Reset();
        return new Digest128(r1, r2);
    }

    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < 16) { bytesWritten = 0; return false; }
        FinalizeState(out ulong r1, out ulong r2);
        BinaryPrimitives.WriteUInt64BigEndian(destination, r1);
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..], r2);
        bytesWritten = 16;
        if (reset) Reset();
        return true;
    }

    private void FinalizeState(out ulong res1, out ulong res2)
    {
        ulong k1 = 0, k2 = 0;
        ReadOnlySpan<byte> tail = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _buffer[0]), _bufferSize);

        switch (tail.Length)
        {
            case 15: k2 ^= (ulong)tail[14] << 48; goto case 14;
            case 14: k2 ^= (ulong)tail[13] << 40; goto case 13;
            case 13: k2 ^= (ulong)tail[12] << 32; goto case 12;
            case 12: k2 ^= (ulong)tail[11] << 24; goto case 11;
            case 11: k2 ^= (ulong)tail[10] << 16; goto case 10;
            case 10: k2 ^= (ulong)tail[9] << 8; goto case 9;
            case 9: k2 ^= tail[8]; k2 *= C2; k2 = BitOperations.RotateLeft(k2, 33); k2 *= C1; _h2 ^= k2; goto case 8;
            case 8: k1 ^= (ulong)tail[7] << 56; goto case 7;
            case 7: k1 ^= (ulong)tail[6] << 48; goto case 6;
            case 6: k1 ^= (ulong)tail[5] << 40; goto case 5;
            case 5: k1 ^= (ulong)tail[4] << 32; goto case 4;
            case 4: k1 ^= (ulong)tail[3] << 24; goto case 3;
            case 3: k1 ^= (ulong)tail[2] << 16; goto case 2;
            case 2: k1 ^= (ulong)tail[1] << 8; goto case 1;
            case 1: k1 ^= tail[0]; k1 *= C1; k1 = BitOperations.RotateLeft(k1, 31); k1 *= C2; _h1 ^= k1; break;
        }

        ulong h1 = _h1 ^ _totalLength;
        ulong h2 = _h2 ^ _totalLength;
        h1 += h2; h2 += h1;
        h1 = Fmix64(h1); h2 = Fmix64(h2);
        h1 += h2; h2 += h1;
        res1 = h1; res2 = h2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Fmix64(ulong k)
    {
        k ^= k >> 33; k *= 0xff51afd7ed558ccdUL;
        k ^= k >> 33; k *= 0xc4ceb9fe1a85ec53UL;
        k ^= k >> 33; return k;
    }

    public void Dispose() => Reset();
}

public sealed class Murmur3_128Algorithm : IIncrementalHashAlgorithm<Murmur3_128Algorithm, Murmur3_128Accumulator, Digest128>
{
    public static HashAlgorithmId Id => HashAlgorithmId.Murmur3_128;
    public static Murmur3_128Accumulator CreateAccumulator() => new(0);

    public static Digest128 Hash(ReadOnlySpan<byte> source)
    {
        Murmur3_128Accumulator acc = new(0);
        acc.Append(source);
        return acc.GetDigest();
    }

    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Murmur3_128Accumulator acc = new(0);
        acc.Append(source);
        acc.TryGetDigest(destination, out _);
    }
}
