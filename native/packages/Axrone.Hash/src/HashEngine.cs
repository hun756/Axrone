namespace Axrone.Hash;

public static class HashEngine
{
    public static async ValueTask<TDigest> HashStreamAsync<TAlgo, TState, TDigest>(
        Stream stream,
        CancellationToken cancellationToken = default)
        where TAlgo : IIncrementalHashAlgorithm<TAlgo, TState, TDigest>
        where TState : struct, IHashAccumulator<TState, TDigest>
        where TDigest : unmanaged, IHashDigest<TDigest>
    {
        ArgumentNullException.ThrowIfNull(stream);

        TState accumulator = TAlgo.CreateAccumulator();
        byte[] poolBuffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(poolBuffer.AsMemory(0, poolBuffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                accumulator.Append(poolBuffer.AsSpan(0, bytesRead));
            return accumulator.GetDigest();
        }
        finally
        {
            accumulator.Dispose();
            ArrayPool<byte>.Shared.Return(poolBuffer);
        }
    }

    public static IHasher CreateHasher(HashAlgorithmId id, ReadOnlySpan<byte> key = default) => id switch
    {
        HashAlgorithmId.XxHash64 => new Adapter<XxHash64Algorithm, XxHash64Accumulator, Digest64>(),
        HashAlgorithmId.WyHash64 => new Adapter<WyHash64Algorithm, WyHash64Accumulator, Digest64>(),
        HashAlgorithmId.Murmur3_128 => new Adapter<Murmur3_128Algorithm, Murmur3_128Accumulator, Digest128>(),
        HashAlgorithmId.Sha256 => new Adapter<Sha256Algorithm, Sha256Accumulator, Digest256>(),
        HashAlgorithmId.Sha384 => new Adapter<Sha384Algorithm, Sha384Accumulator, Digest384>(),
        HashAlgorithmId.Sha512 => new Adapter<Sha512Algorithm, Sha512Accumulator, Digest512>(),
        HashAlgorithmId.Sha3_256 => new Adapter<Sha3_256Algorithm, Sha3_256Accumulator, Digest256>(),
        HashAlgorithmId.Sha3_512 => new Adapter<Sha3_512Algorithm, Sha3_512Accumulator, Digest512>(),
        HashAlgorithmId.HmacSha256 => new HmacAdapter(HashAlgorithmId.HmacSha256, HashAlgorithmName.SHA256, 32, key),
        HashAlgorithmId.HmacSha512 => new HmacAdapter(HashAlgorithmId.HmacSha512, HashAlgorithmName.SHA512, 64, key),
        _ => throw new NotSupportedException($"Algorithm {id} is not supported in dynamic mode.")
    };

    private sealed class Adapter<TAlgo, TState, TDigest> : IHasher
        where TAlgo : IIncrementalHashAlgorithm<TAlgo, TState, TDigest>
        where TState : struct, IHashAccumulator<TState, TDigest>
        where TDigest : unmanaged, IHashDigest<TDigest>
    {
        private TState _accumulator;

        public Adapter() => _accumulator = TAlgo.CreateAccumulator();

        public HashAlgorithmId Id => TAlgo.Id;
        public int DigestLength => TDigest.ByteCount;

        public void Append(ReadOnlySpan<byte> source) => _accumulator.Append(source);

        public void AppendUtf8(ReadOnlySpan<char> source) =>
            Utf8EncodingHelper.AppendUtf8<TState, TDigest>(ref _accumulator, source);

        public HashDigest GetCurrentDigest(bool reset = false) =>
            new(_accumulator.GetDigest(reset).AsSpan());

        public bool TryGetCurrentDigest(Span<byte> destination, out int bytesWritten, bool reset = false) =>
            _accumulator.TryGetDigest(destination, out bytesWritten, reset);

        public void Reset() => _accumulator.Reset();

        public HashDigest ComputeHash(ReadOnlySpan<byte> source) =>
            new(TAlgo.Hash(source).AsSpan());

        public async ValueTask<HashDigest> ComputeHashAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            TDigest digest = await HashStreamAsync<TAlgo, TState, TDigest>(stream, cancellationToken).ConfigureAwait(false);
            return new HashDigest(digest.AsSpan());
        }

        public void Dispose() => _accumulator.Dispose();
    }

    private sealed class HmacAdapter : IHasher
    {
        private readonly HashAlgorithmId _id;
        private readonly int _length;
        private IncrementalHash _hash;

        public HmacAdapter(HashAlgorithmId id, HashAlgorithmName name, int length, ReadOnlySpan<byte> key)
        {
            _id = id;
            _length = length;
            _hash = IncrementalHash.CreateHMAC(name, key);
        }

        public HashAlgorithmId Id => _id;
        public int DigestLength => _length;

        public void Append(ReadOnlySpan<byte> source) => _hash.AppendData(source);

        public void AppendUtf8(ReadOnlySpan<char> source) =>
            Utf8EncodingHelper.AppendUtf8(_hash, source);

        public HashDigest GetCurrentDigest(bool reset = false)
        {
            Span<byte> buffer = stackalloc byte[_length];
            if (reset) _hash.GetHashAndReset(buffer); else _hash.GetCurrentHash(buffer);
            return new HashDigest(buffer);
        }

        public bool TryGetCurrentDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
        {
            if (destination.Length < _length) { bytesWritten = 0; return false; }
            if (reset) return _hash.TryGetHashAndReset(destination, out bytesWritten);
            return _hash.TryGetCurrentHash(destination, out bytesWritten);
        }

        public void Reset()
        {
            Span<byte> dump = stackalloc byte[_length];
            _hash.TryGetHashAndReset(dump, out _);
        }

        public HashDigest ComputeHash(ReadOnlySpan<byte> source)
        {
            Reset();
            _hash.AppendData(source);
            return GetCurrentDigest(reset: true);
        }

        public async ValueTask<HashDigest> ComputeHashAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Reset();
            byte[] pool = ArrayPool<byte>.Shared.Rent(65536);
            try
            {
                int read;
                while ((read = await stream.ReadAsync(pool.AsMemory(0, pool.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    _hash.AppendData(pool.AsSpan(0, read));
                return GetCurrentDigest(reset: true);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pool);
            }
        }

        public void Dispose() => _hash?.Dispose();
    }
}
