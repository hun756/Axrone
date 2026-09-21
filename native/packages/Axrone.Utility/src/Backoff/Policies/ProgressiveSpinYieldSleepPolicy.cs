namespace Axrone.Utility.Backoff.Policies;

public readonly struct ProgressiveSpinYieldSleepPolicy : IBackoffPolicy<ProgressiveSpinYieldSleepPolicy>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BackoffDuration ComputeDuration(uint step, in BackoffConfiguration config, ref ulong rngState)
    {
        if (step < config.SpinIterationsThreshold) return BackoffDuration.Zero;
        if (step < config.YieldIterationsThreshold) return BackoffDuration.Zero;

        uint sleepIndex = step - config.YieldIterationsThreshold;
        long durationNs;

        if (config.Multiplier == 2.0)
        {
            uint shift = Math.Min(sleepIndex, 62U);
            long minNs = config.MinDuration.Nanoseconds;
            if (minNs > 0 && shift >= (uint)BitOperations.LeadingZeroCount((ulong)minNs) - 1U)
            {
                durationNs = config.MaxDuration.Nanoseconds;
            }
            else
            {
                durationNs = Math.Min(config.MaxDuration.Nanoseconds, minNs << (int)shift);
            }
        }
        else
        {
            double targetNs = config.MinDuration.Nanoseconds * Math.Pow(config.Multiplier, Math.Min(sleepIndex, 62U));
            durationNs = (long)Math.Min((double)config.MaxDuration.Nanoseconds, targetNs);
        }

        if (config.JitterRatio > 0.0)
        {
            double randomNorm = FastPcgRng.NextDouble(ref rngState);
            double jitterScale = (1.0 - config.JitterRatio) + (randomNorm * config.JitterRatio);
            durationNs = (long)(durationNs * jitterScale);
        }

        return new BackoffDuration(Math.Clamp(durationNs, config.MinDuration.Nanoseconds, config.MaxDuration.Nanoseconds));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffKind ClassifyKind(uint step, in BackoffConfiguration config)
    {
        if (step < config.SpinIterationsThreshold) return BackoffKind.Spin;
        if (step < config.YieldIterationsThreshold) return BackoffKind.Yield;
        return BackoffKind.Sleep;
    }
}
