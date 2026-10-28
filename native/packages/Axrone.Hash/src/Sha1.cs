namespace Axrone.Hash;

/// <summary>Incremental SHA-1 accumulator backed by the platform hash implementation.</summary>
public sealed class Sha1Accumulator : IHashAccumulator<Sha1Accumulator, Digest160>
{
    private IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> source) => _hash.AppendData(source);

    /// <inheritdoc/>
    public Digest160 GetDigest(bool reset = false)
    {
        Span<byte> buffer = stackalloc byte[Digest160.ByteCount];
        if (reset)
        {
            _hash.GetHashAndReset(buffer);
        }
        else
        {
            _hash.GetCurrentHash(buffer);
        }

        return new Digest160(buffer);
    }

    /// <inheritdoc/>
    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < Digest160.ByteCount)
        {
            bytesWritten = 0;
            return false;
        }

        if (reset)
        {
            return _hash.TryGetHashAndReset(destination, out bytesWritten);
        }

        return _hash.TryGetCurrentHash(destination, out bytesWritten);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        Span<byte> dump = stackalloc byte[Digest160.ByteCount];
        _hash.TryGetHashAndReset(dump, out _);
    }

    /// <inheritdoc/>
    public void Dispose() => _hash?.Dispose();
}

/// <summary>SHA-1 algorithm entry point.</summary>
public sealed class Sha1Algorithm : IIncrementalHashAlgorithm<Sha1Algorithm, Sha1Accumulator, Digest160>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Sha1;

    /// <inheritdoc/>
    public static Sha1Accumulator CreateAccumulator() => new();

    /// <inheritdoc/>
    public static Digest160 Hash(ReadOnlySpan<byte> source)
    {
        Span<byte> buffer = stackalloc byte[Digest160.ByteCount];
        SHA1.HashData(source, buffer);
        return new Digest160(buffer);
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination) =>
        SHA1.HashData(source, destination);
}
