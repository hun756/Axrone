namespace Axrone.Utility.Backoff.Policies;

public readonly struct FibonacciBackoffPolicy : IBackoffPolicy<FibonacciBackoffPolicy>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BackoffDuration ComputeDuration(uint step, in BackoffConfiguration config, ref ulong rngState)
    {
        long fib = EvaluateFibonacci(step);
        long stepInc = config.StepIncrement.Nanoseconds;
        long rawNs;

        if (fib > 0 && stepInc > long.MaxValue / fib)
        {
            rawNs = config.MaxDuration.Nanoseconds;
        }
        else
        {
            rawNs = fib * stepInc;
        }

        long durationNs = Math.Clamp(rawNs, config.MinDuration.Nanoseconds, config.MaxDuration.Nanoseconds);

        if (config.JitterRatio > 0.0)
        {
            double randomNorm = FastPcgRng.NextDouble(ref rngState);
            double jitterScale = (1.0 - config.JitterRatio) + (randomNorm * config.JitterRatio);
            durationNs = (long)(durationNs * jitterScale);
        }

        return new BackoffDuration(Math.Clamp(durationNs, config.MinDuration.Nanoseconds, config.MaxDuration.Nanoseconds));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffKind ClassifyKind(uint step, in BackoffConfiguration config) =>
        step < config.SpinIterationsThreshold ? BackoffKind.Spin : BackoffKind.Sleep;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long EvaluateFibonacci(uint n)
    {
        if (n <= 1U) return 1L;
        long previous = 1L;
        long current = 1L;
        uint capped = Math.Min(n, 45U);

        for (uint i = 2U; i <= capped; i++)
        {
            long next = previous + current;
            previous = current;
            current = next;
        }

        return current;
    }
}
