namespace Axrone.Utility.Backoff;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct BackoffConfiguration : IEquatable<BackoffConfiguration>
{
    public readonly BackoffDuration MinDuration;
    public readonly BackoffDuration MaxDuration;
    public readonly BackoffDuration StepIncrement;
    public readonly uint SpinIterationsThreshold;
    public readonly uint YieldIterationsThreshold;
    public readonly double Multiplier;
    public readonly double JitterRatio;
    public readonly uint MaxRetryLimit;

    public BackoffConfiguration(
        BackoffDuration minDuration,
        BackoffDuration maxDuration,
        BackoffDuration stepIncrement,
        uint spinIterationsThreshold,
        uint yieldIterationsThreshold,
        double multiplier,
        double jitterRatio,
        uint maxRetryLimit)
    {
        if (maxDuration < minDuration)
        {
            ThrowHelper.ThrowArgumentException("MaxDuration cannot be less than MinDuration.");
        }
        if (yieldIterationsThreshold < spinIterationsThreshold)
        {
            ThrowHelper.ThrowArgumentException("YieldIterationsThreshold cannot be less than SpinIterationsThreshold.");
        }
        if (multiplier < 1.0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be greater than or equal to 1.0.");
        }
        if (jitterRatio is < 0.0 or > 1.0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(jitterRatio), "JitterRatio must be bounded between 0.0 and 1.0.");
        }

        MinDuration = minDuration;
        MaxDuration = maxDuration;
        StepIncrement = stepIncrement;
        SpinIterationsThreshold = spinIterationsThreshold;
        YieldIterationsThreshold = yieldIterationsThreshold;
        Multiplier = multiplier;
        JitterRatio = jitterRatio;
        MaxRetryLimit = maxRetryLimit;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(BackoffConfiguration other) =>
        MinDuration.Equals(other.MinDuration) &&
        MaxDuration.Equals(other.MaxDuration) &&
        StepIncrement.Equals(other.StepIncrement) &&
        SpinIterationsThreshold == other.SpinIterationsThreshold &&
        YieldIterationsThreshold == other.YieldIterationsThreshold &&
        Multiplier.Equals(other.Multiplier) &&
        JitterRatio.Equals(other.JitterRatio) &&
        MaxRetryLimit == other.MaxRetryLimit;

    public override bool Equals(object? obj) => obj is BackoffConfiguration other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(MinDuration, MaxDuration, StepIncrement, SpinIterationsThreshold, YieldIterationsThreshold, Multiplier, JitterRatio, MaxRetryLimit);

    public static bool operator ==(BackoffConfiguration left, BackoffConfiguration right) => left.Equals(right);

    public static bool operator !=(BackoffConfiguration left, BackoffConfiguration right) => !left.Equals(right);
}
