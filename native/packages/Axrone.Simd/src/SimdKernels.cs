namespace Axrone.Simd;

/// <summary>Needle context for <see cref="ByteOccurrenceCounter"/>; a ref struct, so dispatch never allocates.</summary>
public readonly ref struct ByteOccurrenceContext
{
    /// <summary>Byte to count.</summary>
    public readonly byte Needle;

    /// <summary>Creates a context.</summary>
    public ByteOccurrenceContext(byte needle) => Needle = needle;
}

/// <summary>Byte-occurrence counter: one kernel across all four tiers, scalar peel shared.</summary>
public readonly struct ByteOccurrenceCounter : ISimdKernelStrategy<byte, ByteOccurrenceContext, nuint>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public nuint ExecuteAvx512(ReadOnlySpan<byte> input, ref ByteOccurrenceContext context)
    {
        nuint length = (nuint)input.Length;
        nuint index = 0;
        nuint count = 0;
        ref byte ptr = ref MemoryMarshal.GetReference(input);
        byte value = context.Needle;

        if (length >= 64)
        {
            Vector512<byte> target = Vector512.Create(value);
            nuint limit = length - 64;

            while (index <= limit)
            {
                Vector512<byte> vec = Vector512.LoadUnsafe(ref ptr, index);
                Vector512<byte> cmp = Vector512.Equals(vec, target);
                ulong bitmask = Vector512.ExtractMostSignificantBits(cmp);
                count += (nuint)BitOperations.PopCount(bitmask);
                index += 64;
            }
        }

        if (index < length)
        {
            count += ExecuteScalarPeel(ref ptr, index, length, value);
        }

        return count;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public nuint ExecuteAvx2(ReadOnlySpan<byte> input, ref ByteOccurrenceContext context)
    {
        nuint length = (nuint)input.Length;
        nuint index = 0;
        nuint count = 0;
        ref byte ptr = ref MemoryMarshal.GetReference(input);
        byte value = context.Needle;

        if (length >= 32)
        {
            Vector256<byte> target = Vector256.Create(value);
            nuint limit = length - 32;

            while (index <= limit)
            {
                Vector256<byte> vec = Vector256.LoadUnsafe(ref ptr, index);
                Vector256<byte> cmp = Vector256.Equals(vec, target);
                uint bitmask = Vector256.ExtractMostSignificantBits(cmp);
                count += (nuint)BitOperations.PopCount(bitmask);
                index += 32;
            }
        }

        if (index < length)
        {
            count += ExecuteScalarPeel(ref ptr, index, length, value);
        }

        return count;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public nuint ExecuteVector128(ReadOnlySpan<byte> input, ref ByteOccurrenceContext context)
    {
        nuint length = (nuint)input.Length;
        nuint index = 0;
        nuint count = 0;
        ref byte ptr = ref MemoryMarshal.GetReference(input);
        byte value = context.Needle;

        if (length >= 16)
        {
            Vector128<byte> target = Vector128.Create(value);
            nuint limit = length - 16;

            while (index <= limit)
            {
                Vector128<byte> vec = Vector128.LoadUnsafe(ref ptr, index);
                Vector128<byte> cmp = Vector128.Equals(vec, target);
                uint bitmask = Vector128.ExtractMostSignificantBits(cmp);
                count += (nuint)BitOperations.PopCount(bitmask);
                index += 16;
            }
        }

        if (index < length)
        {
            count += ExecuteScalarPeel(ref ptr, index, length, value);
        }

        return count;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public nuint ExecuteScalar(ReadOnlySpan<byte> input, ref ByteOccurrenceContext context)
    {
        ref byte ptr = ref MemoryMarshal.GetReference(input);
        return ExecuteScalarPeel(ref ptr, 0, (nuint)input.Length, context.Needle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static nuint ExecuteScalarPeel(ref byte ptr, nuint offset, nuint length, byte value)
    {
        nuint count = 0;
        nuint index = offset;

        if (length - index >= 4)
        {
            nuint unrollLimit = length - 4;
            while (index <= unrollLimit)
            {
                byte v0 = Unsafe.Add(ref ptr, index);
                byte v1 = Unsafe.Add(ref ptr, index + 1);
                byte v2 = Unsafe.Add(ref ptr, index + 2);
                byte v3 = Unsafe.Add(ref ptr, index + 3);

                count += (nuint)((v0 == value ? 1 : 0) +
                                 (v1 == value ? 1 : 0) +
                                 (v2 == value ? 1 : 0) +
                                 (v3 == value ? 1 : 0));
                index += 4;
            }
        }

        while (index < length)
        {
            if (Unsafe.Add(ref ptr, index) == value)
            {
                count++;
            }

            index++;
        }

        return count;
    }
}

/// <summary>Cascading byte engine: tier dispatch around the occurrence kernel.</summary>
public static class CascadingByteEngine
{
    /// <summary>Counts needle occurrences on the widest satisfied tier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountOccurrences(ReadOnlySpan<byte> source, byte value)
    {
        var ctx = new ByteOccurrenceContext(value);
        return SpecializedDispatcher.Dispatch<byte, ByteOccurrenceContext, nuint, ByteOccurrenceCounter>(
            source,
            ref ctx,
            default);
    }
}
