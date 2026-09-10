namespace Axrone.Hash;

public struct Sha3_256Accumulator : IHashAccumulator<Sha3_256Accumulator, Digest256>
{
    private IncrementalHash _hash;

    public Sha3_256Accumulator()
    {
        if (!SHA3_256.IsSupported) throw new PlatformNotSupportedException("SHA3-256 is not supported on this platform.");
        _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA3_256);
    }

    public void Append(ReadOnlySpan<byte> source) => _hash.AppendData(source);

    public Digest256 GetDigest(bool reset = false)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (reset) _hash.GetHashAndReset(buffer); else _hash.GetCurrentHash(buffer);
        return new Digest256(buffer);
    }

    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < 32) { bytesWritten = 0; return false; }
        if (reset) return _hash.TryGetHashAndReset(destination, out bytesWritten);
        return _hash.TryGetCurrentHash(destination, out bytesWritten);
    }

    public void Reset() { Span<byte> dump = stackalloc byte[32]; _hash.TryGetHashAndReset(dump, out _); }
    public void Dispose() => _hash?.Dispose();
}

public sealed class Sha3_256Algorithm : IIncrementalHashAlgorithm<Sha3_256Algorithm, Sha3_256Accumulator, Digest256>
{
    public static HashAlgorithmId Id => HashAlgorithmId.Sha3_256;
    public static Sha3_256Accumulator CreateAccumulator() => new();

    public static Digest256 Hash(ReadOnlySpan<byte> source)
    {
        if (!SHA3_256.IsSupported) throw new PlatformNotSupportedException("SHA3-256 is not supported on this platform.");
        Span<byte> buffer = stackalloc byte[32];
        SHA3_256.HashData(source, buffer);
        return new Digest256(buffer);
    }

    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (!SHA3_256.IsSupported) throw new PlatformNotSupportedException("SHA3-256 is not supported on this platform.");
        SHA3_256.HashData(source, destination);
    }
}

public struct Sha3_512Accumulator : IHashAccumulator<Sha3_512Accumulator, Digest512>
{
    private IncrementalHash _hash;

    public Sha3_512Accumulator()
    {
        if (!SHA3_512.IsSupported) throw new PlatformNotSupportedException("SHA3-512 is not supported on this platform.");
        _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA3_512);
    }

    public void Append(ReadOnlySpan<byte> source) => _hash.AppendData(source);

    public Digest512 GetDigest(bool reset = false)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (reset) _hash.GetHashAndReset(buffer); else _hash.GetCurrentHash(buffer);
        return new Digest512(buffer);
    }

    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < 64) { bytesWritten = 0; return false; }
        if (reset) return _hash.TryGetHashAndReset(destination, out bytesWritten);
        return _hash.TryGetCurrentHash(destination, out bytesWritten);
    }

    public void Reset() { Span<byte> dump = stackalloc byte[64]; _hash.TryGetHashAndReset(dump, out _); }
    public void Dispose() => _hash?.Dispose();
}

public sealed class Sha3_512Algorithm : IIncrementalHashAlgorithm<Sha3_512Algorithm, Sha3_512Accumulator, Digest512>
{
    public static HashAlgorithmId Id => HashAlgorithmId.Sha3_512;
    public static Sha3_512Accumulator CreateAccumulator() => new();

    public static Digest512 Hash(ReadOnlySpan<byte> source)
    {
        if (!SHA3_512.IsSupported) throw new PlatformNotSupportedException("SHA3-512 is not supported on this platform.");
        Span<byte> buffer = stackalloc byte[64];
        SHA3_512.HashData(source, buffer);
        return new Digest512(buffer);
    }

    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (!SHA3_512.IsSupported) throw new PlatformNotSupportedException("SHA3-512 is not supported on this platform.");
        SHA3_512.HashData(source, destination);
    }
}

public sealed class HmacSha256Algorithm : IKeyedHashAlgorithm<HmacSha256Algorithm, Digest256>
{
    public static HashAlgorithmId Id => HashAlgorithmId.HmacSha256;

    public static Digest256 Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source)
    {
        Span<byte> buffer = stackalloc byte[32];
        HMACSHA256.HashData(key, source, buffer);
        return new Digest256(buffer);
    }

    public static void Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination) =>
        HMACSHA256.HashData(key, source, destination);
}

public sealed class HmacSha512Algorithm : IKeyedHashAlgorithm<HmacSha512Algorithm, Digest512>
{
    public static HashAlgorithmId Id => HashAlgorithmId.HmacSha512;

    public static Digest512 Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source)
    {
        Span<byte> buffer = stackalloc byte[64];
        HMACSHA512.HashData(key, source, buffer);
        return new Digest512(buffer);
    }

    public static void Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination) =>
        HMACSHA512.HashData(key, source, destination);
}
