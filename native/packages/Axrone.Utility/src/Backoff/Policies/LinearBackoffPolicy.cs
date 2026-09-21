namespace Axrone.Utility.Backoff.Policies;

public readonly struct LinearBackoffPolicy : IBackoffPolicy<LinearBackoffPolicy>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static BackoffDuration ComputeDuration(uint step, in BackoffConfiguration config, ref ulong rngState)
    {
        long stepInc = config.StepIncrement.Nanoseconds;
        long delta;

        if (step > 0 && stepInc > long.MaxValue / step)
        {
            delta = config.MaxDuration.Nanoseconds;
        }
        else
        {
            delta = step * stepInc;
        }

        long target = (long.MaxValue - delta < config.MinDuration.Nanoseconds)
            ? config.MaxDuration.Nanoseconds
            : Math.Min(config.MaxDuration.Nanoseconds, config.MinDuration.Nanoseconds + delta);

        if (config.JitterRatio > 0.0)
        {
            double randomNorm = FastPcgRng.NextDouble(ref rngState);
            double jitterScale = (1.0 - config.JitterRatio) + (randomNorm * config.JitterRatio);
            target = (long)(target * jitterScale);
        }

        return new BackoffDuration(Math.Clamp(target, config.MinDuration.Nanoseconds, config.MaxDuration.Nanoseconds));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffKind ClassifyKind(uint step, in BackoffConfiguration config) =>
        step < config.SpinIterationsThreshold ? BackoffKind.Spin : BackoffKind.Sleep;
}
