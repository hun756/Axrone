using Axrone.Hash;

namespace Axrone.Asset2D;

/// <summary>
/// Atlas name hashing bridge over <see cref="Fnv1a32Accumulator"/> so the
/// canonical algorithm lives in <c>Axrone.Hash</c>. Hashes UTF-16 code units
/// directly: zero allocation on both build and lookup paths, and consistent
/// within a process for index construction and probing.
/// </summary>
internal static class Asset2DHashing
{
    /// <summary>Hashes a name for the open-addressing index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static uint ComputeNameHash(ReadOnlySpan<char> name)
    {
        Fnv1a32Accumulator acc = new(Fnv1a32Accumulator.OffsetBasis);
        acc.Append(MemoryMarshal.AsBytes(name));
        return acc.GetDigest().Value;
    }
}
