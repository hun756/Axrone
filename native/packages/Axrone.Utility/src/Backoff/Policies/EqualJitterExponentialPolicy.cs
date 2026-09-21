namespace Axrone.Utility.Backoff.Policies;

public readonly struct EqualJitterExponentialPolicy : IBackoffPolicy<EqualJitterExponentialPolicy>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BackoffDuration ComputeDuration(uint step, in BackoffConfiguration config, ref ulong rngState)
    {
        long ceiling;
        if (config.Multiplier == 2.0)
        {
            uint shift = Math.Min(step, 62U);
            long minNs = config.MinDuration.Nanoseconds;
            if (minNs > 0 && shift >= (uint)BitOperations.LeadingZeroCount((ulong)minNs) - 1U)
            {
                ceiling = config.MaxDuration.Nanoseconds;
            }
            else
            {
                ceiling = Math.Min(config.MaxDuration.Nanoseconds, minNs << (int)shift);
            }
        }
        else
        {
            double calculated = config.MinDuration.Nanoseconds * Math.Pow(config.Multiplier, Math.Min(step, 62U));
            ceiling = (long)Math.Min((double)config.MaxDuration.Nanoseconds, calculated);
        }

        long half = ceiling / 2L;
        double randomFraction = FastPcgRng.NextDouble(ref rngState);
        long jittered = half + (long)(half * randomFraction);

        return new BackoffDuration(Math.Clamp(jittered, config.MinDuration.Nanoseconds, config.MaxDuration.Nanoseconds));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffKind ClassifyKind(uint step, in BackoffConfiguration config) => BackoffKind.AsyncDelay;
}
