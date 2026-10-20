using Axrone.Hash;

namespace Axrone.Asset;

/// <summary>
/// Asset-facing FNV-1a entry point. Thin bridge over
/// <see cref="Fnv1a64Accumulator"/> so the canonical implementation lives in
/// <c>Axrone.Hash</c>; this type only preserves the seeded/chained call shape
/// the database, query catalog and fingerprint paths rely on.
/// </summary>
public static class Fnv1a64
{
    /// <summary>FNV offset basis (mirrors the hash package).</summary>
    public const ulong OffsetBasis = Fnv1a64Accumulator.OffsetBasis;

    /// <summary>FNV prime (mirrors the hash package).</summary>
    public const ulong Prime = Fnv1a64Accumulator.Prime;

    /// <summary>Hashes a span, folding into an optional seed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong Compute(ReadOnlySpan<byte> bytes, ulong hash = OffsetBasis)
    {
        Fnv1a64Accumulator acc = new(hash);
        acc.Append(bytes);
        return acc.GetDigest().Value;
    }

    /// <summary>Hashes UTF-8 encoded chars, renting only past the stack budget.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong ComputeUtf8Chars(ReadOnlySpan<char> chars, ulong hash = OffsetBasis)
    {
        int maxBytes = checked(chars.Length * 3);
        byte[]? rented = null;
        Span<byte> buffer = maxBytes <= 256 ? stackalloc byte[maxBytes] : (rented = ArrayPool<byte>.Shared.Rent(maxBytes));

        try
        {
            int written = Encoding.UTF8.GetBytes(chars, buffer);
            return Compute(buffer[..written], hash);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
