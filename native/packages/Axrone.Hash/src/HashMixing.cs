namespace Axrone.Hash;

/// <summary>
/// Shared 64-bit avalanche mixers: tiny invertible finalizers that spread one
/// input-bit flip over roughly half the output bits. Kept in one place so
/// hash tables and accumulators share the exact same mixing step instead of
/// each inlining the Murmur constants.
/// </summary>
public static class HashMixing
{
    /// <summary>MurmurHash3 64-bit finalizer: avalanches <paramref name="key"/>.</summary>
    /// <param name="key">The value to mix.</param>
    /// <returns>The avalanched value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong Fmix64(ulong key)
    {
        key ^= key >> 33;
        key *= 0xff51afd7ed558ccdUL;
        key ^= key >> 33;
        key *= 0xc4ceb9fe1a85ec53UL;
        key ^= key >> 33;
        return key;
    }
}
