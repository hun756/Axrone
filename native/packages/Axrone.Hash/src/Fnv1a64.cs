namespace Axrone.Hash;

/// <summary>
/// Incremental 64-bit FNV-1a accumulator: a running offset-basis state XORed
/// with each input octet and multiplied by the FNV prime. Genuinely streaming
/// (no buffering), so this is the cheapest incremental hasher in the package
/// for short keys such as asset identifiers and kind discriminators.
/// </summary>
public struct Fnv1a64Accumulator : IHashAccumulator<Fnv1a64Accumulator, Digest64>
{
    /// <summary>64-bit FNV offset basis.</summary>
    public const ulong OffsetBasis = 14695981039346656037ul;

    /// <summary>64-bit FNV prime.</summary>
    public const ulong Prime = 1099511628211ul;

    private readonly ulong _seed;
    private ulong _hash;

    /// <summary>
    /// Creates an accumulator.
    /// </summary>
    /// <param name="seed">Initial state. Defaults to the FNV offset basis.</param>
    public Fnv1a64Accumulator(ulong seed = OffsetBasis)
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
        ulong hash = _hash;
        for (int i = 0; i < source.Length; i++)
        {
            hash = (hash ^ source[i]) * Prime;
        }

        _hash = hash;
    }

    /// <inheritdoc/>
    public Digest64 GetDigest(bool reset = false)
    {
        Digest64 digest = new(_hash);
        if (reset)
        {
            Reset();
        }

        return digest;
    }

    /// <inheritdoc/>
    public bool TryGetDigest(Span<byte> destination, out int bytesWritten, bool reset = false)
    {
        if (destination.Length < 8)
        {
            bytesWritten = 0;
            return false;
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination, _hash);
        bytesWritten = 8;
        if (reset)
        {
            Reset();
        }

        return true;
    }

    /// <inheritdoc/>
    public void Dispose() => Reset();
}

/// <summary>
/// 64-bit FNV-1a algorithm entry point: one-shot hashing plus accumulator
/// creation, following the package's algorithm/accumulator/digest pattern.
/// </summary>
public sealed class Fnv1a64Algorithm : IIncrementalHashAlgorithm<Fnv1a64Algorithm, Fnv1a64Accumulator, Digest64>
{
    /// <inheritdoc/>
    public static HashAlgorithmId Id => HashAlgorithmId.Fnv1a64;

    /// <inheritdoc/>
    /// <remarks>
    /// The seed travels explicitly: a bare <c>new()</c> on this struct
    /// zero-initializes instead of running the optional-parameter constructor,
    /// which would silently rebase every digest to zero.
    /// </remarks>
    public static Fnv1a64Accumulator CreateAccumulator() => new(Fnv1a64Accumulator.OffsetBasis);

    /// <inheritdoc/>
    public static Digest64 Hash(ReadOnlySpan<byte> source)
    {
        Fnv1a64Accumulator acc = new(Fnv1a64Accumulator.OffsetBasis);
        acc.Append(source);
        return acc.GetDigest();
    }

    /// <inheritdoc/>
    public static void Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        Fnv1a64Accumulator acc = new(Fnv1a64Accumulator.OffsetBasis);
        acc.Append(source);
        acc.TryGetDigest(destination, out _);
    }
}
