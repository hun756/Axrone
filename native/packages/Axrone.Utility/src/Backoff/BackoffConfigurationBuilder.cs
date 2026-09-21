namespace Axrone.Utility.Backoff;

using Axrone.Utility.Builders;

/// <summary>
/// Fluent builder for <see cref="BackoffConfiguration"/> over a mutable accumulator state.
/// </summary>
/// <remarks>
/// Every field carries a working default, so nothing is "missing-able": the required mask is
/// empty and domain validation is authoritative. Supports fork/reset for template workflows.
/// </remarks>
public sealed class BackoffConfigurationBuilder
    : AggregateBuilder<BackoffConfigurationBuilder, BackoffConfigurationState, BackoffConfiguration>
{
    public BackoffConfigurationBuilder()
    {
        State = BackoffConfigurationState.Default;
    }

    protected override BackoffConfigurationBuilder Self => this;

    protected override PropertyBitmask64 RequiredMask => PropertyBitmask64.None;

    public BackoffConfigurationBuilder WithMinDuration(BackoffDuration duration)
    {
        State.MinDuration = duration;
        return this;
    }

    public BackoffConfigurationBuilder WithMaxDuration(BackoffDuration duration)
    {
        State.MaxDuration = duration;
        return this;
    }

    public BackoffConfigurationBuilder WithStepIncrement(BackoffDuration increment)
    {
        State.StepIncrement = increment;
        return this;
    }

    public BackoffConfigurationBuilder WithSpinIterationsThreshold(uint threshold)
    {
        State.SpinIterationsThreshold = threshold;
        return this;
    }

    public BackoffConfigurationBuilder WithYieldIterationsThreshold(uint threshold)
    {
        State.YieldIterationsThreshold = threshold;
        return this;
    }

    public BackoffConfigurationBuilder WithMultiplier(double multiplier)
    {
        State.Multiplier = multiplier;
        return this;
    }

    public BackoffConfigurationBuilder WithJitterRatio(double jitterRatio)
    {
        State.JitterRatio = jitterRatio;
        return this;
    }

    public BackoffConfigurationBuilder WithMaxRetryLimit(uint maxRetryLimit)
    {
        State.MaxRetryLimit = maxRetryLimit;
        return this;
    }

    /// <summary>Builds the configuration; preserves the product's throw contract.</summary>
    public override BackoffConfiguration Build() => BackoffConfigurationState.Materialize(in State);

    /// <inheritdoc/>
    public override void Reset() => State = BackoffConfigurationState.Default;

    /// <inheritdoc/>
    public override BackoffConfigurationBuilder Fork() => CopyTo(new BackoffConfigurationBuilder());
}
