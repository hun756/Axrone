namespace Axrone.Utility.Backoff;

using System.Diagnostics.CodeAnalysis;
using Axrone.Utility.Builders;

public sealed class BackoffConfigurationBuilder : BuilderBase<BackoffConfigurationBuilder, BackoffConfiguration>
{
    private BackoffDuration _minDuration = BackoffDuration.FromMicroseconds(50);
    private BackoffDuration _maxDuration = BackoffDuration.FromMilliseconds(2000);
    private BackoffDuration _stepIncrement = BackoffDuration.FromMilliseconds(5);
    private uint _spinIterationsThreshold = 8;
    private uint _yieldIterationsThreshold = 16;
    private double _multiplier = 2.0;
    private double _jitterRatio = 0.25;
    private uint _maxRetryLimit = uint.MaxValue;

    public BackoffConfigurationBuilder WithMinDuration(BackoffDuration duration)
    {
        _minDuration = duration;
        return this;
    }

    public BackoffConfigurationBuilder WithMaxDuration(BackoffDuration duration)
    {
        _maxDuration = duration;
        return this;
    }

    public BackoffConfigurationBuilder WithStepIncrement(BackoffDuration increment)
    {
        _stepIncrement = increment;
        return this;
    }

    public BackoffConfigurationBuilder WithSpinIterationsThreshold(uint threshold)
    {
        _spinIterationsThreshold = threshold;
        return this;
    }

    public BackoffConfigurationBuilder WithYieldIterationsThreshold(uint threshold)
    {
        _yieldIterationsThreshold = threshold;
        return this;
    }

    public BackoffConfigurationBuilder WithMultiplier(double multiplier)
    {
        _multiplier = multiplier;
        return this;
    }

    public BackoffConfigurationBuilder WithJitterRatio(double jitterRatio)
    {
        _jitterRatio = jitterRatio;
        return this;
    }

    public BackoffConfigurationBuilder WithMaxRetryLimit(uint maxRetryLimit)
    {
        _maxRetryLimit = maxRetryLimit;
        return this;
    }

    protected override BackoffConfigurationBuilder Self => this;

    public override bool TryBuild([MaybeNullWhen(false)] out BackoffConfiguration result, out BuilderDiagnostic diagnostic) =>
        TryCreate(Build, out result, out diagnostic);

    public override BackoffConfiguration Build() =>
        new(
            _minDuration,
            _maxDuration,
            _stepIncrement,
            _spinIterationsThreshold,
            _yieldIterationsThreshold,
            _multiplier,
            _jitterRatio,
            _maxRetryLimit);
}
