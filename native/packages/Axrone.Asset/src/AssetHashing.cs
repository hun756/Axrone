namespace Axrone.Asset;

/// <summary>
/// 64-bit FNV-1a hasher: offset basis XORed per octet, multiplied by the
/// prime. Span-based and allocation-free on both ingest and digest paths.
/// </summary>
public static class Fnv1a64
{
    /// <summary>FNV offset basis.</summary>
    public const ulong OffsetBasis = 14695981039346656037UL;

    /// <summary>FNV prime.</summary>
    public const ulong Prime = 1099511628211UL;

    /// <summary>Hashes a span, folding into an optional seed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong Compute(ReadOnlySpan<byte> bytes, ulong hash = OffsetBasis)
    {
        ref byte ptr = ref MemoryMarshal.GetReference(bytes);
        nuint len = (nuint)bytes.Length;
        for (nuint i = 0; i < len; i++)
        {
            hash ^= Unsafe.Add(ref ptr, i);
            hash *= Prime;
        }

        return hash;
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
