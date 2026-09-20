namespace Axrone.Utility.Backoff;

using Axrone.Utility.Builders;

/// <summary>
/// Mutable accumulator for <see cref="BackoffConfigurationBuilder"/>; materializes an immutable
/// <see cref="BackoffConfiguration"/>.
/// </summary>
/// <remarks>
/// All fields carry working defaults, so no slot is "missing-able": the builder's required mask
/// is empty and <see cref="TryValidate"/> is the authoritative gate. Invariant rules live in
/// <see cref="BackoffConfiguration"/> and are shared, never restated.
/// </remarks>
public struct BackoffConfigurationState : IAggregateDefinition<BackoffConfigurationState, BackoffConfiguration>,
    IEquatable<BackoffConfigurationState>
{
    /// <summary>Working defaults matching the builder's documented starting point.</summary>
    public static BackoffConfigurationState Default => new()
    {
        MinDuration = BackoffDuration.FromMicroseconds(50),
        MaxDuration = BackoffDuration.FromMilliseconds(2000),
        StepIncrement = BackoffDuration.FromMilliseconds(5),
        SpinIterationsThreshold = 8,
        YieldIterationsThreshold = 16,
        Multiplier = 2.0,
        JitterRatio = 0.25,
        MaxRetryLimit = uint.MaxValue,
    };

    public BackoffDuration MinDuration { get; set; }
    public BackoffDuration MaxDuration { get; set; }
    public BackoffDuration StepIncrement { get; set; }
    public uint SpinIterationsThreshold { get; set; }
    public uint YieldIterationsThreshold { get; set; }
    public double Multiplier { get; set; }
    public double JitterRatio { get; set; }
    public uint MaxRetryLimit { get; set; }

    /// <inheritdoc/>
    public static BackoffConfiguration Materialize(in BackoffConfigurationState state) =>
        new(
            state.MinDuration,
            state.MaxDuration,
            state.StepIncrement,
            state.SpinIterationsThreshold,
            state.YieldIterationsThreshold,
            state.Multiplier,
            state.JitterRatio,
            state.MaxRetryLimit);

    /// <inheritdoc/>
    public static bool TryValidate(in BackoffConfigurationState state, out BuilderDiagnostic diagnostic)
    {
        BackoffInvariant violation = BackoffConfiguration.CheckInvariants(
            state.MinDuration,
            state.MaxDuration,
            state.SpinIterationsThreshold,
            state.YieldIterationsThreshold,
            state.Multiplier,
            state.JitterRatio);

        if (violation != BackoffInvariant.None)
        {
            diagnostic = BuilderDiagnostic.Fail(
                BuilderStatusCode.ValidationFailed,
                BackoffConfiguration.DescribeViolation(violation));
            return false;
        }

        diagnostic = BuilderDiagnostic.Ok;
        return true;
    }

    /// <inheritdoc/>
    public bool Equals(BackoffConfigurationState other) =>
        MinDuration.Equals(other.MinDuration) &&
        MaxDuration.Equals(other.MaxDuration) &&
        StepIncrement.Equals(other.StepIncrement) &&
        SpinIterationsThreshold == other.SpinIterationsThreshold &&
        YieldIterationsThreshold == other.YieldIterationsThreshold &&
        Multiplier.Equals(other.Multiplier) &&
        JitterRatio.Equals(other.JitterRatio) &&
        MaxRetryLimit == other.MaxRetryLimit;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BackoffConfigurationState other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(MinDuration, MaxDuration, StepIncrement, SpinIterationsThreshold, YieldIterationsThreshold, Multiplier, JitterRatio, MaxRetryLimit);

    public static bool operator ==(BackoffConfigurationState left, BackoffConfigurationState right) => left.Equals(right);

    public static bool operator !=(BackoffConfigurationState left, BackoffConfigurationState right) => !left.Equals(right);
}
