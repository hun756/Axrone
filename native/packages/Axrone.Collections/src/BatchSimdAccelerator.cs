namespace Axrone.Collections;

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

/// <summary>
/// Hardware accelerator executing cascading SIMD sequence validation across
/// AVX-512 (Vector512), AVX2 (Vector256), and AdvSIMD/SSE2 (Vector128).
/// Validates that monotonic sequence slots are contiguous and ready for consumption.
/// </summary>
internal static unsafe class BatchSimdAccelerator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountAvailableSequences(
        nuint* sequences,
        SequenceNumber startIndex,
        nuint mask,
        SequenceNumber expectedBase,
        nuint maxCount)
    {
        nuint offset = startIndex.Value & mask;
        nuint contiguous = mask + 1 - offset;
        nuint limit = contiguous < maxCount ? contiguous : maxCount;

        nuint* p = sequences + offset;
        nuint i = 0;
        nuint expBase = expectedBase.Value;

        // Cascade 1: 512-bit SIMD (AVX10 / AVX-512)
        if (Vector512.IsHardwareAccelerated && limit >= 8)
        {
            Vector512<nuint> step = Vector512.Create((nuint)0, 1, 2, 3, 4, 5, 6, 7);
            nuint vecLimit = limit & ~(nuint)7;
            for (; i < vecLimit; i += 8)
            {
                Vector512<nuint> expected = Vector512.Create(expBase + i) + step;
                Vector512<nuint> actual = Vector512.Load(p + i);
                if (actual != expected)
                {
                    goto ScalarPeeledFallback;
                }
            }
        }

        // Cascade 2: 256-bit SIMD (AVX2)
        if (Vector256.IsHardwareAccelerated && (limit - i) >= 4)
        {
            Vector256<nuint> step = Vector256.Create((nuint)0, 1, 2, 3);
            nuint vecLimit = i + ((limit - i) & ~(nuint)3);
            for (; i < vecLimit; i += 4)
            {
                Vector256<nuint> expected = Vector256.Create(expBase + i) + step;
                Vector256<nuint> actual = Vector256.Load(p + i);
                if (actual != expected)
                {
                    goto ScalarPeeledFallback;
                }
            }
        }

        // Cascade 3: 128-bit SIMD (ARM64 AdvSIMD / SSE2)
        if (Vector128.IsHardwareAccelerated && (limit - i) >= 2)
        {
            Vector128<nuint> step = Vector128.Create((nuint)0, 1);
            nuint vecLimit = i + ((limit - i) & ~(nuint)1);
            for (; i < vecLimit; i += 2)
            {
                Vector128<nuint> expected = Vector128.Create(expBase + i) + step;
                Vector128<nuint> actual = Vector128.Load(p + i);
                if (actual != expected)
                {
                    goto ScalarPeeledFallback;
                }
            }
        }

    ScalarPeeledFallback:
        // Cascade 4: Scalar 4x unrolled peel
        while (i + 4 <= limit)
        {
            if (p[i] != expBase + i) return i; i++;
            if (p[i] != expBase + i) return i; i++;
            if (p[i] != expBase + i) return i; i++;
            if (p[i] != expBase + i) return i; i++;
        }

        while (i < limit)
        {
            if (p[i] != expBase + i) return i;
            i++;
        }

        return i;
    }
}
