namespace Axrone.Hash;

public enum HashAlgorithmId : byte
{
    XxHash64 = 1,
    WyHash64 = 2,
    Murmur3_128 = 3,
    Sha256 = 4,
    Sha384 = 5,
    Sha512 = 6,
    Sha3_256 = 7,
    Sha3_512 = 8,
    HmacSha256 = 9,
    HmacSha512 = 10
}

public interface IHashDigest<TSelf> :
    IEquatable<TSelf>,
    IComparable<TSelf>,
    ISpanFormattable,
    IUtf8SpanFormattable
    where TSelf : unmanaged, IHashDigest<TSelf>
{
    static abstract int ByteCount { get; }

    [UnscopedRef]
    ReadOnlySpan<byte> AsSpan();

    void CopyTo(Span<byte> destination);
    bool TryCopyTo(Span<byte> destination);
    static abstract TSelf FromSpan(ReadOnlySpan<byte> span);
    static abstract bool TryParse(ReadOnlySpan<char> chars, out TSelf result);
    static abstract bool TryParse(ReadOnlySpan<byte> utf8Chars, out TSelf result);
}

public interface IHashAccumulator<TSelf, TDigest> : IDisposable
    where TSelf : struct, IHashAccumulator<TSelf, TDigest>
    where TDigest : unmanaged, IHashDigest<TDigest>
{
    void Append(ReadOnlySpan<byte> source);
    TDigest GetDigest(bool reset = false);
    bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false);
    void Reset();
}

public interface IHashAlgorithm<TSelf, TDigest>
    where TSelf : IHashAlgorithm<TSelf, TDigest>
    where TDigest : unmanaged, IHashDigest<TDigest>
{
    static abstract HashAlgorithmId Id { get; }
    static abstract TDigest Hash(ReadOnlySpan<byte> source);
    static abstract void Hash(ReadOnlySpan<byte> source, Span<byte> destination);
}

public interface IKeyedHashAlgorithm<TSelf, TDigest>
    where TSelf : IKeyedHashAlgorithm<TSelf, TDigest>
    where TDigest : unmanaged, IHashDigest<TDigest>
{
    static abstract HashAlgorithmId Id { get; }
    static abstract TDigest Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source);
    static abstract void Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination);
}

public interface IIncrementalHashAlgorithm<TSelf, TState, TDigest> : IHashAlgorithm<TSelf, TDigest>
    where TSelf : IIncrementalHashAlgorithm<TSelf, TState, TDigest>
    where TState : struct, IHashAccumulator<TState, TDigest>
    where TDigest : unmanaged, IHashDigest<TDigest>
{
    static abstract TState CreateAccumulator();
}

public interface IHasher : IDisposable
{
    HashAlgorithmId Id { get; }
    int DigestLength { get; }
    void Append(ReadOnlySpan<byte> source);
    void AppendUtf8(ReadOnlySpan<char> source);
    HashDigest GetCurrentDigest(bool reset = false);
    bool TryGetCurrentDigest(Span<byte> destination, out int bytesWritten, bool reset = false);
    void Reset();
    HashDigest ComputeHash(ReadOnlySpan<byte> source);
    ValueTask<HashDigest> ComputeHashAsync(Stream stream, CancellationToken cancellationToken = default);
}
