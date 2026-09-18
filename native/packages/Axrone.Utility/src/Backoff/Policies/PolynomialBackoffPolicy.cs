namespace Axrone.Utility.Backoff.Policies;

public readonly struct PolynomialBackoffPolicy : IBackoffPolicy<PolynomialBackoffPolicy>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BackoffDuration ComputeDuration(uint step, in BackoffConfiguration config, ref ulong rngState)
    {
        uint clamped = Math.Min(step, 100U);
        long poly = (long)clamped * clamped * clamped;
        long stepInc = config.StepIncrement.Nanoseconds;
        long target;

        if (stepInc > 0 && poly > (long.MaxValue - config.MinDuration.Nanoseconds) / stepInc)
        {
            target = config.MaxDuration.Nanoseconds;
        }
        else
        {
            target = Math.Min(config.MaxDuration.Nanoseconds, config.MinDuration.Nanoseconds + (poly * stepInc));
        }

        if (config.JitterRatio > 0.0)
        {
            double randomNorm = FastPcgRng.NextDouble(ref rngState);
            double jitterScale = (1.0 - config.JitterRatio) + (randomNorm * config.JitterRatio);
            target = (long)(target * jitterScale);
        }

        return new BackoffDuration(Math.Clamp(target, config.MinDuration.Nanoseconds, config.MaxDuration.Nanoseconds));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffKind ClassifyKind(uint step, in BackoffConfiguration config) => BackoffKind.Sleep;
}
