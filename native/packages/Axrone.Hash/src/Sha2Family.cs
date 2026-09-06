namespace Axrone.Hash;

public struct Sha256Accumulator : IHashAccumulator<Sha256Accumulator, Digest256>
{
    private IncrementalHash _hash;

    public Sha256Accumulator() => _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

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

public sealed class Sha256Algorithm : IIncrementalHashAlgorithm<Sha256Algorithm, Sha256Accumulator, Digest256>
{
    public static HashAlgorithmId Id => HashAlgorithmId.Sha256;
    public static Sha256Accumulator CreateAccumulator() => new();

    public static Digest256 Hash(ReadOnlySpan<byte> source)
    {
        Span<byte> buffer = stackalloc byte[32];
        SHA256.HashData(source, buffer);
        return new Digest256(buffer);
    }

    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination) => SHA256.HashData(source, destination);
}

public struct Sha384Accumulator : IHashAccumulator<Sha384Accumulator, Digest384>
{
    private IncrementalHash _hash;

    public Sha384Accumulator() => _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA384);

    public void Append(ReadOnlySpan<byte> source) => _hash.AppendData(source);

    public Digest384 GetDigest(bool reset = false)
    {
        Span<byte> buffer = stackalloc byte[48];
        if (reset) _hash.GetHashAndReset(buffer); else _hash.GetCurrentHash(buffer);
        return new Digest384(buffer);
    }

    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < 48) { bytesWritten = 0; return false; }
        if (reset) return _hash.TryGetHashAndReset(destination, out bytesWritten);
        return _hash.TryGetCurrentHash(destination, out bytesWritten);
    }

    public void Reset() { Span<byte> dump = stackalloc byte[48]; _hash.TryGetHashAndReset(dump, out _); }
    public void Dispose() => _hash?.Dispose();
}

public sealed class Sha384Algorithm : IIncrementalHashAlgorithm<Sha384Algorithm, Sha384Accumulator, Digest384>
{
    public static HashAlgorithmId Id => HashAlgorithmId.Sha384;
    public static Sha384Accumulator CreateAccumulator() => new();

    public static Digest384 Hash(ReadOnlySpan<byte> source)
    {
        Span<byte> buffer = stackalloc byte[48];
        SHA384.HashData(source, buffer);
        return new Digest384(buffer);
    }

    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination) => SHA384.HashData(source, destination);
}

public struct Sha512Accumulator : IHashAccumulator<Sha512Accumulator, Digest512>
{
    private IncrementalHash _hash;

    public Sha512Accumulator() => _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);

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

public sealed class Sha512Algorithm : IIncrementalHashAlgorithm<Sha512Algorithm, Sha512Accumulator, Digest512>
{
    public static HashAlgorithmId Id => HashAlgorithmId.Sha512;
    public static Sha512Accumulator CreateAccumulator() => new();

    public static Digest512 Hash(ReadOnlySpan<byte> source)
    {
        Span<byte> buffer = stackalloc byte[64];
        SHA512.HashData(source, buffer);
        return new Digest512(buffer);
    }

    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination) => SHA512.HashData(source, destination);
}
