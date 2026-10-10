namespace Axrone.Utility.NativeBuffer;

using Axrone.Utility.Alignment;

/// <summary>
/// Tiered vectorized memory primitives: 512-bit lanes where supported,
/// stepping down through 256 and 128 bits with a scalar tail.
/// </summary>
internal static unsafe class VectorizedOperations
{
    /// <summary>Zeroes <paramref name="byteLength"/> bytes at <paramref name="target"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ZeroMemory(byte* target, ByteSize byteLength)
    {
        nuint length = byteLength.Value;
        nuint cursor = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<byte>.Count)
        {
            nuint threshold = length - (nuint)Vector512<byte>.Count;
            do
            {
                Vector512<byte>.Zero.Store(target + cursor);
                cursor += (nuint)Vector512<byte>.Count;
            } while (cursor <= threshold);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<byte>.Count)
        {
            nuint threshold = length - (nuint)Vector256<byte>.Count;
            do
            {
                Vector256<byte>.Zero.Store(target + cursor);
                cursor += (nuint)Vector256<byte>.Count;
            } while (cursor <= threshold);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<byte>.Count)
        {
            nuint threshold = length - (nuint)Vector128<byte>.Count;
            do
            {
                Vector128<byte>.Zero.Store(target + cursor);
                cursor += (nuint)Vector128<byte>.Count;
            } while (cursor <= threshold);
        }

        while (cursor < length)
        {
            target[cursor] = 0;
            cursor++;
        }
    }

    /// <summary>Copies <paramref name="byteLength"/> bytes; regions must not overlap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CopyMemory(byte* source, byte* target, ByteSize byteLength)
    {
        nuint length = byteLength.Value;
        nuint cursor = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<byte>.Count)
        {
            nuint threshold = length - (nuint)Vector512<byte>.Count;
            do
            {
                Vector512.Load(source + cursor).Store(target + cursor);
                cursor += (nuint)Vector512<byte>.Count;
            } while (cursor <= threshold);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<byte>.Count)
        {
            nuint threshold = length - (nuint)Vector256<byte>.Count;
            do
            {
                Vector256.Load(source + cursor).Store(target + cursor);
                cursor += (nuint)Vector256<byte>.Count;
            } while (cursor <= threshold);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<byte>.Count)
        {
            nuint threshold = length - (nuint)Vector128<byte>.Count;
            do
            {
                Vector128.Load(source + cursor).Store(target + cursor);
                cursor += (nuint)Vector128<byte>.Count;
            } while (cursor <= threshold);
        }

        while (cursor < length)
        {
            target[cursor] = source[cursor];
            cursor++;
        }
    }

    /// <summary>Byte-wise comparison of two ranges.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool EqualsMemory(byte* left, byte* right, ByteSize byteLength)
    {
        nuint length = byteLength.Value;
        nuint cursor = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<byte>.Count)
        {
            nuint threshold = length - (nuint)Vector512<byte>.Count;
            do
            {
                if (Vector512.Load(left + cursor) != Vector512.Load(right + cursor))
                {
                    return false;
                }
                cursor += (nuint)Vector512<byte>.Count;
            } while (cursor <= threshold);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<byte>.Count)
        {
            nuint threshold = length - (nuint)Vector256<byte>.Count;
            do
            {
                if (Vector256.Load(left + cursor) != Vector256.Load(right + cursor))
                {
                    return false;
                }
                cursor += (nuint)Vector256<byte>.Count;
            } while (cursor <= threshold);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<byte>.Count)
        {
            nuint threshold = length - (nuint)Vector128<byte>.Count;
            do
            {
                if (Vector128.Load(left + cursor) != Vector128.Load(right + cursor))
                {
                    return false;
                }
                cursor += (nuint)Vector128<byte>.Count;
            } while (cursor <= threshold);
        }

        while (cursor < length)
        {
            if (left[cursor] != right[cursor])
            {
                return false;
            }
            cursor++;
        }

        return true;
    }
}
