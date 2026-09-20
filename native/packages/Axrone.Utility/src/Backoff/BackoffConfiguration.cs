namespace Axrone.Utility.Backoff;

/// <summary>Invariant rule violated by a backoff configuration; none when valid.</summary>
internal enum BackoffInvariant : byte
{
    None = 0,
    MaxLessThanMin = 1,
    YieldLessThanSpin = 2,
    MultiplierTooSmall = 3,
    JitterOutOfRange = 4,
}

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
        switch (CheckInvariants(minDuration, maxDuration, spinIterationsThreshold, yieldIterationsThreshold, multiplier, jitterRatio))
        {
            case BackoffInvariant.None:
                break;
            case BackoffInvariant.MaxLessThanMin:
                ThrowHelper.ThrowArgumentException("MaxDuration cannot be less than MinDuration.");
                break;
            case BackoffInvariant.YieldLessThanSpin:
                ThrowHelper.ThrowArgumentException("YieldIterationsThreshold cannot be less than SpinIterationsThreshold.");
                break;
            case BackoffInvariant.MultiplierTooSmall:
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be greater than or equal to 1.0.");
                break;
            default:
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(jitterRatio), "JitterRatio must be bounded between 0.0 and 1.0.");
                break;
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

    /// <summary>Evaluates the invariant rule table; single source of truth for ctor and state validation.</summary>
    internal static BackoffInvariant CheckInvariants(
        BackoffDuration minDuration,
        BackoffDuration maxDuration,
        uint spinIterationsThreshold,
        uint yieldIterationsThreshold,
        double multiplier,
        double jitterRatio)
    {
        if (maxDuration < minDuration)
        {
            return BackoffInvariant.MaxLessThanMin;
        }
        if (yieldIterationsThreshold < spinIterationsThreshold)
        {
            return BackoffInvariant.YieldLessThanSpin;
        }
        if (multiplier < 1.0)
        {
            return BackoffInvariant.MultiplierTooSmall;
        }
        if (jitterRatio is < 0.0 or > 1.0)
        {
            return BackoffInvariant.JitterOutOfRange;
        }

        return BackoffInvariant.None;
    }

    /// <summary>Human-readable description of a violated invariant.</summary>
    internal static string DescribeViolation(BackoffInvariant violation) => violation switch
    {
        BackoffInvariant.MaxLessThanMin => "MaxDuration cannot be less than MinDuration.",
        BackoffInvariant.YieldLessThanSpin => "YieldIterationsThreshold cannot be less than SpinIterationsThreshold.",
        BackoffInvariant.MultiplierTooSmall => "Multiplier must be greater than or equal to 1.0.",
        BackoffInvariant.JitterOutOfRange => "JitterRatio must be bounded between 0.0 and 1.0.",
        _ => "Unknown invariant violation.",
    };

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
