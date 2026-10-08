namespace Axrone.Hash;

/// <summary>
/// Incremental 32-bit FNV-1a accumulator: a running offset-basis state XORed
/// with each input octet and multiplied by the FNV prime. Genuinely streaming
/// (no buffering), so this is the cheapest incremental hasher in the package
/// for short keys such as uniform value hashes and shader source keys.
/// </summary>
public struct Fnv1a32Accumulator : IHashAccumulator<Fnv1a32Accumulator, Digest32>
{
    /// <summary>32-bit FNV offset basis.</summary>
    public const uint OffsetBasis = 2166136261u;

    /// <summary>32-bit FNV prime.</summary>
    public const uint Prime = 16777619u;

    private readonly uint _seed;
    private uint _hash;

    /// <summary>
    /// Creates an accumulator.
    /// </summary>
    /// <param name="seed">Initial state. Defaults to the FNV offset basis.</param>
    public Fnv1a32Accumulator(uint seed = OffsetBasis)
    {
        _seed = seed;
        _hash = seed;
    }

    /// <inheritdoc/>
    public void Reset() => _hash = _seed;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ReadOnlySpan<byte> source)
    {
        uint hash = _hash;
        for (int i = 0; i < source.Length; i++)
            hash = (hash ^ source[i]) * Prime;
        _hash = hash;
    }

    /// <inheritdoc/>
    public Digest32 GetDigest(bool reset = false)
    {
        Digest32 digest = new(_hash);
        if (reset) Reset();
        return digest;
    }

    /// <inheritdoc/>
    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < 4) { bytesWritten = 0; return false; }
        BinaryPrimitives.WriteUInt32BigEndian(destination, _hash);
        bytesWritten = 4;
        if (reset) Reset();
        return true;
    }

    /// <inheritdoc/>
    public void Dispose() => Reset();
}

/// <summary>
/// 32-bit FNV-1a algorithm entry point: one-shot hashing plus accumulator
/// creation, following the package's algorithm/accumulator/digest pattern.
/// Hash values are deduplication keys only — not stable across library versions.
/// </summary>
public sealed class Fnv1a32Algorithm : IIncrementalHashAlgorithm<Fnv1a32Algorithm, Fnv1a32Accumulator, Digest32>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Fnv1a32;

    /// <inheritdoc/>
    /// <remarks>
    /// The seed travels explicitly: a bare <c>new()</c> on this struct
    /// zero-initializes instead of running the optional-parameter constructor,
    /// which would silently rebase every digest to zero.
    /// </remarks>
    public static Fnv1a32Accumulator CreateAccumulator() => new(Fnv1a32Accumulator.OffsetBasis);

    /// <inheritdoc/>
    public static Digest32 Hash(ReadOnlySpan<byte> source)
    {
        Fnv1a32Accumulator acc = new(Fnv1a32Accumulator.OffsetBasis);
        acc.Append(source);
        return acc.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Fnv1a32Accumulator acc = new(Fnv1a32Accumulator.OffsetBasis);
        acc.Append(source);
        acc.TryGetDigest(destination, out _);
    }
}
