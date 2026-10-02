namespace Axrone.Hash;

/// <summary>
/// 32-bit FNV-1a non-cryptographic hash: the offset basis XORed with each input
/// octet, multiplied by the FNV prime. Single-shot and allocation-free; the
/// canonical home for the FNV-1a copies previously scattered across consumers.
/// Hash values are deduplication keys only — not stable across library versions.
/// </summary>
public static class Fnv1a32
{
    /// <summary>32-bit FNV offset basis.</summary>
    public const uint OffsetBasis = 2166136261u;

    /// <summary>32-bit FNV prime.</summary>
    public const uint Prime = 16777619u;

    /// <summary>
    /// Hashes raw bytes.
    /// </summary>
    /// <param name="data">Bytes to hash.</param>
    /// <returns>32-bit FNV-1a digest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint hash = OffsetBasis;
        for (int i = 0; i < data.Length; i++)
            hash = (hash ^ data[i]) * Prime;
        return hash;
    }

    /// <summary>
    /// Hashes float lanes by their bit patterns (no conversion).
    /// </summary>
    /// <param name="values">Lanes to hash.</param>
    /// <returns>32-bit FNV-1a digest over the lane bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(scoped ReadOnlySpan<float> values) =>
        Compute(MemoryMarshal.AsBytes(values));

    /// <summary>
    /// Hashes UTF-16 code units (e.g. shader source keys).
    /// </summary>
    /// <param name="values">Characters to hash.</param>
    /// <returns>32-bit FNV-1a digest over the character bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(scoped ReadOnlySpan<char> values) =>
        Compute(MemoryMarshal.AsBytes(values));
}
