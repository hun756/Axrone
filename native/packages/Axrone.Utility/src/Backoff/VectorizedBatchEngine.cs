namespace Axrone.Utility.Backoff;

public static class VectorizedBatchEngine
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ComputeBatch(
        ReadOnlySpan<uint> steps,
        Span<BackoffDuration> outputs,
        in BackoffConfiguration config,
        ref ulong rngState)
    {
        if (outputs.Length < steps.Length)
        {
            ThrowHelper.ThrowArgumentException("Destination output span is smaller than the input steps span.");
        }

        int length = steps.Length;
        int i = 0;

        long minNs = config.MinDuration.Nanoseconds;
        long maxNs = config.MaxDuration.Nanoseconds;

        if (Vector256.IsHardwareAccelerated && length >= Vector256<long>.Count)
        {
            int stepChunk = Vector256<long>.Count;
            Vector256<long> vMin = Vector256.Create(minNs);
            Vector256<long> vMax = Vector256.Create(maxNs);
            Vector256<ulong> vMultiplier = Vector256.Create(FastPcgRng.DefaultMultiplier);
            Vector256<ulong> vStream = Vector256.Create(FastPcgRng.DefaultStreamConstant);

            Vector256<ulong> vRng = Vector256.Create(
                FastPcgRng.Next(ref rngState),
                FastPcgRng.Next(ref rngState),
                FastPcgRng.Next(ref rngState),
                FastPcgRng.Next(ref rngState));

            Span<long> powerBuffer = stackalloc long[stepChunk];

            for (; i <= length - stepChunk; i += stepChunk)
            {
                for (int k = 0; k < stepChunk; k++)
                {
                    uint exponent = Math.Min(steps[i + k], 30U);
                    powerBuffer[k] = 1L << (int)exponent;
                }

                Vector256<long> powers = Vector256.Create(
                    powerBuffer[0], powerBuffer[1], powerBuffer[2], powerBuffer[3]);
                Vector256<long> rawCeilings = Vector256.Multiply(vMin, powers);
                Vector256<long> ceilings = Vector256.Min(vMax, rawCeilings);

                vRng = unchecked((vRng * vMultiplier) + vStream);
                Vector256<ulong> randFraction = Vector256.ShiftRightLogical(vRng, 33);
                Vector256<ulong> jittered = Vector256.ShiftRightLogical(
                    Vector256.Multiply(ceilings.AsUInt64(), randFraction), 31);
                Vector256<long> results = Vector256.Max(vMin, jittered.AsInt64());

                unsafe
                {
                    fixed (long* dest = &Unsafe.As<BackoffDuration, long>(ref outputs[i]))
                    {
                        results.Store(dest);
                    }
                }
            }
        }

        if (Vector128.IsHardwareAccelerated && i <= length - Vector128<long>.Count)
        {
            int stepChunk = Vector128<long>.Count;
            Vector128<long> vMin = Vector128.Create(minNs);
            Vector128<long> vMax = Vector128.Create(maxNs);
            Vector128<ulong> vMultiplier = Vector128.Create(FastPcgRng.DefaultMultiplier);
            Vector128<ulong> vStream = Vector128.Create(FastPcgRng.DefaultStreamConstant);

            Vector128<ulong> vRng = Vector128.Create(
                FastPcgRng.Next(ref rngState),
                FastPcgRng.Next(ref rngState));

            Span<long> powerBuffer = stackalloc long[stepChunk];

            for (; i <= length - stepChunk; i += stepChunk)
            {
                for (int k = 0; k < stepChunk; k++)
                {
                    uint exponent = Math.Min(steps[i + k], 30U);
                    powerBuffer[k] = 1L << (int)exponent;
                }

                Vector128<long> powers = Vector128.Create(powerBuffer[0], powerBuffer[1]);
                Vector128<long> rawCeilings = Vector128.Multiply(vMin, powers);
                Vector128<long> ceilings = Vector128.Min(vMax, rawCeilings);

                vRng = unchecked((vRng * vMultiplier) + vStream);
                Vector128<ulong> randFraction = Vector128.ShiftRightLogical(vRng, 33);
                Vector128<ulong> jittered = Vector128.ShiftRightLogical(
                    Vector128.Multiply(ceilings.AsUInt64(), randFraction), 31);
                Vector128<long> results = Vector128.Max(vMin, jittered.AsInt64());

                unsafe
                {
                    fixed (long* dest = &Unsafe.As<BackoffDuration, long>(ref outputs[i]))
                    {
                        results.Store(dest);
                    }
                }
            }
        }

        ref uint stepsBase = ref MemoryMarshal.GetReference(steps);
        ref BackoffDuration outputsBase = ref MemoryMarshal.GetReference(outputs);

        for (; (uint)i < (uint)length; i++)
        {
            uint s = Unsafe.Add(ref stepsBase, (nuint)i);
            uint exponent = Math.Min(s, 30U);
            long ceiling = Math.Min(maxNs, minNs * (1L << (int)exponent));
            double rand = FastPcgRng.NextDouble(ref rngState);
            long jittered = (long)(ceiling * rand);

            Unsafe.Add(ref outputsBase, (nuint)i) = new BackoffDuration(Math.Max(minNs, jittered));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void ComputeBatchGeneric<TPolicy>(
        ReadOnlySpan<uint> steps,
        Span<BackoffDuration> outputs,
        in BackoffConfiguration config,
        ref ulong rngState)
        where TPolicy : struct, IBackoffPolicy<TPolicy>
    {
        if (outputs.Length < steps.Length)
        {
            ThrowHelper.ThrowArgumentException("Destination output span is smaller than the input steps span.");
        }

        ref uint stepsBase = ref MemoryMarshal.GetReference(steps);
        ref BackoffDuration outputsBase = ref MemoryMarshal.GetReference(outputs);

        for (uint idx = 0; (int)idx < steps.Length; idx++)
        {
            uint s = Unsafe.Add(ref stepsBase, (nuint)idx);
            Unsafe.Add(ref outputsBase, (nuint)idx) = TPolicy.ComputeDuration(s, in config, ref rngState);
        }
    }
}
