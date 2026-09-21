namespace Axrone.Utility.Backoff.Policies;

public readonly struct DecorrelatedJitterPolicy : IBackoffPolicy<DecorrelatedJitterPolicy>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BackoffDuration ComputeDuration(uint step, in BackoffConfiguration config, ref ulong rngState)
    {
        double minNs = config.MinDuration.Nanoseconds;
        double maxNs = config.MaxDuration.Nanoseconds;
        double expLimit = Math.Min(step, 15U);
        double rangeMax = Math.Min(maxNs, minNs * Math.Pow(3.0, expLimit));
        double delta = Math.Max(0.0, rangeMax - minNs);
        double randomFraction = FastPcgRng.NextDouble(ref rngState);
        long calculated = (long)(minNs + (delta * randomFraction));

        return new BackoffDuration(Math.Clamp(calculated, config.MinDuration.Nanoseconds, config.MaxDuration.Nanoseconds));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffKind ClassifyKind(uint step, in BackoffConfiguration config) => BackoffKind.AsyncDelay;
}
