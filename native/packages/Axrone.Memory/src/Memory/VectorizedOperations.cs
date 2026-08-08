using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Axrone.Memory;

/// <summary>
/// SIMD-accelerated memory operations for unmanaged memory.
/// Uses AVX2 when available, falls back to SSE2, then scalar.
/// </summary>
public static class VectorizedOperations
{
    /// <summary>Clears (zeroes) <paramref name="size"/> bytes at <paramref name="destination"/> using SIMD.</summary>
    /// <param name="destination">Pointer to the memory to clear.</param>
    /// <param name="size">Number of bytes to zero.</param>
    public static unsafe void ClearMemory(void* destination, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        if (size == 0) return;

        if (size >= 128 && Vector256.IsHardwareAccelerated && Avx2.IsSupported)
        {
            var zeroVector = Vector256<byte>.Zero;
            var dest = (Vector256<byte>*)destination;
            int vectorCount = size / 32;

            for (int i = 0; i < vectorCount; i++)
                dest[i] = zeroVector;

            int processed = vectorCount * 32;
            if (processed < size)
                new Span<byte>((byte*)destination + processed, size - processed).Clear();
        }
        else if (size >= 64 && Vector128.IsHardwareAccelerated && Sse2.IsSupported)
        {
            var zeroVector = Vector128<byte>.Zero;
            var dest = (Vector128<byte>*)destination;
            int vectorCount = size / 16;

            for (int i = 0; i < vectorCount; i++)
                dest[i] = zeroVector;

            int processed = vectorCount * 16;
            if (processed < size)
                new Span<byte>((byte*)destination + processed, size - processed).Clear();
        }
        else
        {
            new Span<byte>(destination, size).Clear();
        }
    }

    /// <summary>Copies <paramref name="size"/> bytes from <paramref name="source"/> to <paramref name="destination"/> using SIMD.</summary>
    /// <param name="source">Source pointer.</param>
    /// <param name="destination">Destination pointer.</param>
    /// <param name="size">Number of bytes to copy.</param>
    public static unsafe void CopyMemory(void* source, void* destination, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        if (size == 0) return;

        nint gap = (nint)((byte*)destination - (byte*)source);
        bool overlaps = gap > 0 ? gap < size : -gap < size;

        if (overlaps)
        {
            Buffer.MemoryCopy(source, destination, size, size);
            return;
        }

        if (size >= 128 && Vector256.IsHardwareAccelerated && Avx2.IsSupported)
        {
            var src = (Vector256<byte>*)source;
            var dest = (Vector256<byte>*)destination;
            int vectorCount = size / 32;

            for (int i = 0; i < vectorCount; i++)
                dest[i] = src[i];

            int processed = vectorCount * 32;
            if (processed < size)
            {
                Buffer.MemoryCopy(
                    (byte*)source + processed,
                    (byte*)destination + processed,
                    size - processed,
                    size - processed);
            }
        }
        else if (size >= 64 && Vector128.IsHardwareAccelerated && Sse2.IsSupported)
        {
            var src = (Vector128<byte>*)source;
            var dest = (Vector128<byte>*)destination;
            int vectorCount = size / 16;

            for (int i = 0; i < vectorCount; i++)
                dest[i] = src[i];

            int processed = vectorCount * 16;
            if (processed < size)
            {
                Buffer.MemoryCopy(
                    (byte*)source + processed,
                    (byte*)destination + processed,
                    size - processed,
                    size - processed);
            }
        }
        else
        {
            Buffer.MemoryCopy(source, destination, size, size);
        }
    }
}
