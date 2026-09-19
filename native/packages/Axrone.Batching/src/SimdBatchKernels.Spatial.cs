namespace Axrone.Batching;

/// <summary>
/// Spatial-index and data-health kernels: 3D Morton encoding, non-finite scan.
/// </summary>
/// <remarks>
/// Morton SIMD is gated on <c>Avx2.IsSupported</c>, not on <c>Vector256.IsHardwareAccelerated</c>:
/// the widening path uses x86-only shifts, and ARM64 AdvSIMD reports 256-bit acceleration without
/// them. The scalar tail is the reference the tests compare against.
/// </remarks>
public static unsafe partial class SimdBatchKernels
{
    /// <summary>
    /// Encodes 10 bits per component into 30-bit Z-order codes.
    /// </summary>
    /// <param name="x">X components; only the low 10 bits are used.</param>
    /// <param name="y">Y components.</param>
    /// <param name="z">Z components.</param>
    /// <param name="destination">Receives the codes; must be at least <paramref name="x"/>.Length.</param>
    /// <exception cref="ArgumentException">The spans differ in length.</exception>
    public static void MortonEncode3D(ReadOnlySpan<uint> x, ReadOnlySpan<uint> y, ReadOnlySpan<uint> z, Span<uint> destination)
    {
        ThrowHelper.ValidateTernarySpans(x, y, z, destination);

        var count = (nuint)x.Length;
        if (count == 0)
        {
            return;
        }

        ref uint xBase = ref MemoryMarshal.GetReference(x);
        ref uint yBase = ref MemoryMarshal.GetReference(y);
        ref uint zBase = ref MemoryMarshal.GetReference(z);
        ref uint targetBase = ref MemoryMarshal.GetReference(destination);
        nuint index = 0;

        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported && count >= 8)
        {
            var mask10 = Vector256.Create(0x3FFu);
            nuint step = 8, limit = count - step + 1;
            for (; index < limit; index += step)
            {
                var vx = Vector256.LoadUnsafe(ref xBase, index) & mask10;
                var vy = Vector256.LoadUnsafe(ref yBase, index) & mask10;
                var vz = Vector256.LoadUnsafe(ref zBase, index) & mask10;

                vx = SpreadBitsAvx2(vx);
                vy = SpreadBitsAvx2(vy);
                vz = SpreadBitsAvx2(vz);

                (vx | System.Runtime.Intrinsics.X86.Avx2.ShiftLeftLogical(vy, 1) | System.Runtime.Intrinsics.X86.Avx2.ShiftLeftLogical(vz, 2))
                    .StoreUnsafe(ref targetBase, index);
            }
        }

        for (; index < count; index++)
        {
            Unsafe.Add(ref targetBase, index) =
                SpreadBits10(Unsafe.Add(ref xBase, index))
                | (SpreadBits10(Unsafe.Add(ref yBase, index)) << 1)
                | (SpreadBits10(Unsafe.Add(ref zBase, index)) << 2);
        }
    }

    /// <summary>Spreads the low 10 bits to 3D-Morton digit positions.</summary>
    private static uint SpreadBits10(uint value)
    {
        value &= 0x000003FFu;
        value = (value | (value << 16)) & 0x030000FFu;
        value = (value | (value << 8)) & 0x0300F00Fu;
        value = (value | (value << 4)) & 0x030C30C3u;
        value = (value | (value << 2)) & 0x09249249u;
        return value;
    }

    /// <summary>Vectorized <see cref="SpreadBits10"/>; call only when Avx2 is supported.</summary>
    private static Vector256<uint> SpreadBitsAvx2(Vector256<uint> value)
    {
        value = (value | System.Runtime.Intrinsics.X86.Avx2.ShiftLeftLogical(value, 16)) & Vector256.Create(0x030000FFu);
        value = (value | System.Runtime.Intrinsics.X86.Avx2.ShiftLeftLogical(value, 8)) & Vector256.Create(0x0300F00Fu);
        value = (value | System.Runtime.Intrinsics.X86.Avx2.ShiftLeftLogical(value, 4)) & Vector256.Create(0x030C30C3u);
        return (value | System.Runtime.Intrinsics.X86.Avx2.ShiftLeftLogical(value, 2)) & Vector256.Create(0x09249249u);
    }

    /// <summary>
    /// Scans for non-finite elements, writing their indices.
    /// </summary>
    /// <param name="source">Values to scan.</param>
    /// <param name="destination">Receives indices of NaN/±Infinity elements; may be shorter (truncates).</param>
    /// <returns>Indices written. When the return equals <paramref name="destination"/>.Length and the
    /// source was not exhausted, the scan truncated.</returns>
    /// <remarks>
    /// The 8-wide fast path skips fully-finite blocks with one <c>|v| &lt; +inf</c> compare and only
    /// drops to scalar confirmation for blocks that fail it.
    /// </remarks>
    public static int ScanNonFinite(ReadOnlySpan<float> source, Span<int> destination)
    {
        var count = (nuint)source.Length;
        if (count == 0 || destination.IsEmpty)
        {
            return 0;
        }

        ref float sourceBase = ref MemoryMarshal.GetReference(source);
        ref int targetBase = ref MemoryMarshal.GetReference(destination);
        var capacity = (nuint)destination.Length;
        var found = nuint.Zero;
        nuint index = 0;

        if (Vector256.IsHardwareAccelerated && count >= 8)
        {
            var infinity = Vector256.Create(float.PositiveInfinity);
            nuint step = 8, limit = count - step + 1;
            for (; index < limit && found < capacity; index += step)
            {
                var values = Vector256.LoadUnsafe(ref sourceBase, index);
                var finite = Vector256.LessThan(Vector256.Abs(values), infinity);

                var clean = true;
                for (var lane = 0; lane < 8; lane++)
                {
                    if (finite.GetElement(lane) == 0f)
                    {
                        clean = false;
                        break;
                    }
                }

                if (clean)
                {
                    continue;
                }

                for (var lane = 0; lane < 8 && found < capacity; lane++)
                {
                    var value = Unsafe.Add(ref sourceBase, index + (nuint)lane);
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        Unsafe.Add(ref targetBase, found) = (int)(index + (nuint)lane);
                        found++;
                    }
                }
            }
        }

        for (; index < count && found < capacity; index++)
        {
            if (!float.IsFinite(Unsafe.Add(ref sourceBase, index)))
            {
                Unsafe.Add(ref targetBase, found) = (int)index;
                found++;
            }
        }

        return (int)found;
    }
}
