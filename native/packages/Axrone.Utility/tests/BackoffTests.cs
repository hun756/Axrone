namespace Axrone.Utility.Tests.Backoff;

using System.Diagnostics;
using Axrone.Utility.Backoff;
using Axrone.Utility.Backoff.SpinPolicies;
using Axrone.Utility.Builders;

public class BackoffDurationTests
{
    [Fact]
    public void Zero_ReturnsZeroNanoseconds()
    {
        BackoffDuration.Zero.Nanoseconds.Should().Be(0);
    }

    [Fact]
    public void FromNanoseconds_RoundTrips()
    {
        BackoffDuration.FromNanoseconds(42).Nanoseconds.Should().Be(42);
    }

    [Fact]
    public void FromMicroseconds_ConvertsCorrectly()
    {
        BackoffDuration.FromMicroseconds(5).Nanoseconds.Should().Be(5_000);
    }

    [Fact]
    public void FromMilliseconds_ConvertsCorrectly()
    {
        BackoffDuration.FromMilliseconds(1.0).Nanoseconds.Should().Be(1_000_000);
    }

    [Fact]
    public void FromSeconds_ConvertsCorrectly()
    {
        BackoffDuration.FromSeconds(0.5).Nanoseconds.Should().Be(500_000_000);
    }

    [Fact]
    public void FromTimeSpan_ConvertsCorrectly()
    {
        BackoffDuration.FromTimeSpan(TimeSpan.FromMilliseconds(2)).Nanoseconds.Should().Be(2_000_000);
    }

    [Fact]
    public void ToTimeSpan_RoundTrips()
    {
        var original = TimeSpan.FromMilliseconds(10);
        BackoffDuration.FromTimeSpan(original).ToTimeSpan().Should().Be(original);
    }

    [Fact]
    public void NegativeNanoseconds_Throws()
    {
        Action act = () => { _ = new BackoffDuration(-1); };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NegativeMicroseconds_Throws()
    {
        Action act = () => BackoffDuration.FromMicroseconds(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NegativeMilliseconds_Throws()
    {
        Action act = () => BackoffDuration.FromMilliseconds(-1.0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NaNMilliseconds_Throws()
    {
        Action act = () => BackoffDuration.FromMilliseconds(double.NaN);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NegativeSeconds_Throws()
    {
        Action act = () => BackoffDuration.FromSeconds(-1.0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NegativeTimeSpan_Throws()
    {
        Action act = () => BackoffDuration.FromTimeSpan(TimeSpan.FromSeconds(-1));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OverflowMicroseconds_ReturnsMaxValue()
    {
        BackoffDuration.FromMicroseconds(long.MaxValue).Should().Be(BackoffDuration.MaxValue);
    }

    [Fact]
    public void OverflowMilliseconds_ReturnsMaxValue()
    {
        BackoffDuration.FromMilliseconds(1e15).Should().Be(BackoffDuration.MaxValue);
    }

    [Fact]
    public void Addition_WorksWithoutOverflow()
    {
        var a = BackoffDuration.FromMicroseconds(10);
        var b = BackoffDuration.FromMicroseconds(20);
        (a + b).Nanoseconds.Should().Be(30_000);
    }

    [Fact]
    public void Addition_OverflowClampsToMaxValue()
    {
        var a = new BackoffDuration(long.MaxValue);
        var b = new BackoffDuration(1);
        (a + b).Should().Be(BackoffDuration.MaxValue);
    }

    [Fact]
    public void Subtraction_UnderflowClampsToZero()
    {
        var a = BackoffDuration.FromMicroseconds(5);
        var b = BackoffDuration.FromMicroseconds(10);
        (a - b).Should().Be(BackoffDuration.Zero);
    }

    [Fact]
    public void ComparisonOperators_Work()
    {
        var small = BackoffDuration.FromNanoseconds(10);
        var large = BackoffDuration.FromNanoseconds(20);

        (small < large).Should().BeTrue();
        (small <= large).Should().BeTrue();
        (large > small).Should().BeTrue();
        (large >= small).Should().BeTrue();
        var sameAsSmall = BackoffDuration.FromNanoseconds(10);
        (small == sameAsSmall).Should().BeTrue();
        (small != large).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_ReturnsCorrectSign()
    {
        var a = BackoffDuration.FromNanoseconds(5);
        var b = BackoffDuration.FromNanoseconds(10);

        a.CompareTo(b).Should().BeNegative();
        b.CompareTo(a).Should().BePositive();
        a.CompareTo(a).Should().Be(0);
    }
}

public class SpinPolicyTests
{
    [Fact]
    public void ProgressiveSpinBackoff_InitializeAdvanceReset()
    {
        ProgressiveSpinBackoff.Initialize(out int state);
        state.Should().Be(0);

        for (int i = 0; i < 25; i++)
        {
            ProgressiveSpinBackoff.Advance(ref state);
        }
        state.Should().Be(25);

        ProgressiveSpinBackoff.Reset(ref state);
        state.Should().Be(0);
    }

    [Fact]
    public void AdaptiveSpinBackoff_InitializeAdvanceReset()
    {
        AdaptiveSpinBackoff.Initialize(out int state);
        state.Should().Be(0);

        for (int i = 0; i < 35; i++)
        {
            AdaptiveSpinBackoff.Advance(ref state);
        }
        state.Should().Be(35);

        AdaptiveSpinBackoff.Reset(ref state);
        state.Should().Be(0);
    }

    [Fact]
    public void AggressiveSpinBackoff_InitializeAdvanceReset()
    {
        AggressiveSpinBackoff.Initialize(out int state);
        state.Should().Be(0);

        AggressiveSpinBackoff.Advance(ref state);
        state.Should().Be(1);

        AggressiveSpinBackoff.Reset(ref state);
        state.Should().Be(0);
    }

    [Fact]
    public void YieldingBackoff_InitializeAdvanceReset()
    {
        YieldingBackoff.Initialize(out int state);
        state.Should().Be(0);

        YieldingBackoff.Advance(ref state);
        state.Should().Be(1);

        YieldingBackoff.Reset(ref state);
        state.Should().Be(0);
    }

    [Fact]
    public void AllSpinPolicies_ImplementISpinBackoff()
    {
        void ValidatePolicy<T>() where T : struct, ISpinBackoff
        {
            T.Initialize(out int state);
            state.Should().Be(0);
            T.Advance(ref state);
            state.Should().Be(1);
            T.Reset(ref state);
            state.Should().Be(0);
        }

        ValidatePolicy<ProgressiveSpinBackoff>();
        ValidatePolicy<AdaptiveSpinBackoff>();
        ValidatePolicy<AggressiveSpinBackoff>();
        ValidatePolicy<YieldingBackoff>();
    }
}

public class BackoffConfigurationTests
{
    [Fact]
    public void ValidConfiguration_DoesNotThrow()
    {
        Action act = () => { _ = new BackoffConfiguration(
            BackoffDuration.FromMicroseconds(10),
            BackoffDuration.FromMilliseconds(100),
            BackoffDuration.FromMicroseconds(5),
            8, 16, 2.0, 0.25, 100); };

        act.Should().NotThrow();
    }

    [Fact]
    public void MaxLessThanMin_Throws()
    {
        Action act = () => { _ = new BackoffConfiguration(
            BackoffDuration.FromMilliseconds(100),
            BackoffDuration.FromMicroseconds(10),
            BackoffDuration.FromMicroseconds(5),
            8, 16, 2.0, 0.25, 100); };

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void YieldLessThanSpin_Throws()
    {
        Action act = () => { _ = new BackoffConfiguration(
            BackoffDuration.FromMicroseconds(10),
            BackoffDuration.FromMilliseconds(100),
            BackoffDuration.FromMicroseconds(5),
            16, 8, 2.0, 0.25, 100); };

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MultiplierBelowOne_Throws()
    {
        Action act = () => { _ = new BackoffConfiguration(
            BackoffDuration.FromMicroseconds(10),
            BackoffDuration.FromMilliseconds(100),
            BackoffDuration.FromMicroseconds(5),
            8, 16, 0.5, 0.25, 100); };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JitterRatioNegative_Throws()
    {
        Action act = () => { _ = new BackoffConfiguration(
            BackoffDuration.FromMicroseconds(10),
            BackoffDuration.FromMilliseconds(100),
            BackoffDuration.FromMicroseconds(5),
            8, 16, 2.0, -0.1, 100); };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JitterRatioAboveOne_Throws()
    {
        Action act = () => { _ = new BackoffConfiguration(
            BackoffDuration.FromMicroseconds(10),
            BackoffDuration.FromMilliseconds(100),
            BackoffDuration.FromMicroseconds(5),
            8, 16, 2.0, 1.5, 100); };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new BackoffConfiguration(
            BackoffDuration.FromMicroseconds(10),
            BackoffDuration.FromMilliseconds(100),
            BackoffDuration.FromMicroseconds(5),
            8, 16, 2.0, 0.25, 100);
        var b = a;

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new BackoffConfiguration(
            BackoffDuration.FromMicroseconds(10),
            BackoffDuration.FromMilliseconds(100),
            BackoffDuration.FromMicroseconds(5),
            8, 16, 2.0, 0.25, 100);
        var b = new BackoffConfiguration(
            BackoffDuration.FromMicroseconds(20),
            BackoffDuration.FromMilliseconds(100),
            BackoffDuration.FromMicroseconds(5),
            8, 16, 2.0, 0.25, 100);

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }
}

public class BackoffConfigurationBuilderTests
{
    [Fact]
    public void DefaultBuilder_ProducesValidConfiguration()
    {
        var config = new BackoffConfigurationBuilder().Build();

        config.MinDuration.Should().Be(BackoffDuration.FromMicroseconds(50));
        config.MaxDuration.Should().Be(BackoffDuration.FromMilliseconds(2000));
        config.Multiplier.Should().Be(2.0);
        config.JitterRatio.Should().Be(0.25);
    }

    [Fact]
    public void TryBuild_InvalidRange_ReportsDiagnostic()
    {
        var builder = new BackoffConfigurationBuilder()
            .WithMinDuration(BackoffDuration.FromSeconds(5))
            .WithMaxDuration(BackoffDuration.FromSeconds(1));

        builder.TryBuild(out _, out var diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
        diagnostic.Message.Should().Contain("ArgumentException");
    }

    [Fact]
    public void Build_InvalidRange_PreservesThrowContract()
    {
        var act = () => new BackoffConfigurationBuilder()
            .WithMinDuration(BackoffDuration.FromSeconds(5))
            .WithMaxDuration(BackoffDuration.FromSeconds(1))
            .Build();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FluentApi_OverridesDefaults()
    {
        var config = new BackoffConfigurationBuilder()
            .WithMinDuration(BackoffDuration.FromMicroseconds(100))
            .WithMaxDuration(BackoffDuration.FromSeconds(5))
            .WithMultiplier(3.0)
            .WithJitterRatio(0.5)
            .WithSpinIterationsThreshold(16)
            .WithYieldIterationsThreshold(32)
            .WithMaxRetryLimit(50)
            .Build();

        config.MinDuration.Should().Be(BackoffDuration.FromMicroseconds(100));
        config.MaxDuration.Should().Be(BackoffDuration.FromSeconds(5));
        config.Multiplier.Should().Be(3.0);
        config.JitterRatio.Should().Be(0.5);
        config.SpinIterationsThreshold.Should().Be(16);
        config.YieldIterationsThreshold.Should().Be(32);
        config.MaxRetryLimit.Should().Be(50);
    }
}

public class BackoffPresetsTests
{
    [Theory]
    [InlineData("LockAcquisition")]
    [InlineData("NetworkRetry")]
    [InlineData("AssetStreaming")]
    [InlineData("AudioThread")]
    [InlineData("GpuSubmission")]
    [InlineData("TaskStealing")]
    public void Preset_ConstructsSuccessfully(string presetName)
    {
        var config = presetName switch
        {
            "LockAcquisition" => BackoffPresets.LockAcquisition(),
            "NetworkRetry" => BackoffPresets.NetworkRetry(),
            "AssetStreaming" => BackoffPresets.AssetStreaming(),
            "AudioThread" => BackoffPresets.AudioThread(),
            "GpuSubmission" => BackoffPresets.GpuSubmission(),
            "TaskStealing" => BackoffPresets.TaskStealing(),
            _ => throw new InvalidOperationException()
        };

        config.MaxDuration.Should().BeGreaterThanOrEqualTo(config.MinDuration);
        config.Multiplier.Should().BeGreaterThanOrEqualTo(1.0);
        config.JitterRatio.Should().BeInRange(0.0, 1.0);
    }
}

public class FastPcgRngTests
{
    [Fact]
    public void Next_IsDeterministic()
    {
        ulong state1 = 42;
        ulong state2 = 42;

        for (int i = 0; i < 100; i++)
        {
            FastPcgRng.Next(ref state1).Should().Be(FastPcgRng.Next(ref state2));
        }
    }

    [Fact]
    public void Next_ProducesDistinctValues()
    {
        ulong state = 12345;
        var seen = new HashSet<ulong>();
        for (int i = 0; i < 1000; i++)
        {
            seen.Add(FastPcgRng.Next(ref state));
        }
        seen.Count.Should().Be(1000);
    }

    [Fact]
    public void NextDouble_ReturnsValuesInRange()
    {
        ulong state = 99;
        for (int i = 0; i < 1000; i++)
        {
            double value = FastPcgRng.NextDouble(ref state);
            value.Should().BeInRange(0.0, 1.0);
        }
    }
}

public class MicroPauseTests
{
    [Fact]
    public void Execute_DoesNotThrow()
    {
        Action act = () => MicroPause.Execute(4);
        act.Should().NotThrow();
    }

    [Fact]
    public void Execute_ZeroCount_DoesNotThrow()
    {
        Action act = () => MicroPause.Execute(0);
        act.Should().NotThrow();
    }
}

public class ProgressiveValueSpinnerTests
{
    [Fact]
    public void SpinOnce_IncrementsIteration()
    {
        var spinner = new ProgressiveValueSpinner(4, 8);
        spinner.Iteration.Should().Be(0);

        spinner.SpinOnce();
        spinner.Iteration.Should().Be(1);

        for (int i = 0; i < 10; i++) spinner.SpinOnce();
        spinner.Iteration.Should().Be(11);
    }

    [Fact]
    public void Reset_ResetsIteration()
    {
        var spinner = new ProgressiveValueSpinner(4, 8);
        for (int i = 0; i < 5; i++) spinner.SpinOnce();
        spinner.Iteration.Should().Be(5);

        spinner.Reset();
        spinner.Iteration.Should().Be(0);
    }

    [Fact]
    public void SpinUntil_TruePredicate_ReturnsTrue()
    {
        var spinner = new ProgressiveValueSpinner(4, 8);
        bool result = spinner.SpinUntil(() => true, TimeSpan.FromMilliseconds(100));
        result.Should().BeTrue();
    }

    [Fact]
    public void SpinUntil_Timeout_ReturnsFalse()
    {
        var spinner = new ProgressiveValueSpinner(4, 8);
        bool result = spinner.SpinUntil(() => false, TimeSpan.FromMilliseconds(10));
        result.Should().BeFalse();
    }

    [Fact]
    public void SpinUntil_WithCancellation_RespectsToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var spinner = new ProgressiveValueSpinner(4, 8);
        bool result = spinner.SpinUntil(() => false, TimeSpan.FromSeconds(5), cts.Token);
        result.Should().BeFalse();
    }

    [Fact]
    public void TrySpinOnce_BeforeDeadline_ReturnsTrue()
    {
        var spinner = new ProgressiveValueSpinner(4, 8);
        long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
        spinner.TrySpinOnce(deadline).Should().BeTrue();
    }

    [Fact]
    public void TrySpinOnce_AfterDeadline_ReturnsFalse()
    {
        var spinner = new ProgressiveValueSpinner(4, 8);
        long deadline = Stopwatch.GetTimestamp() - 1;
        spinner.TrySpinOnce(deadline).Should().BeFalse();
    }
}

public class BackoffKindTests
{
    [Fact]
    public void EnumValues_AreCorrect()
    {
        ((byte)BackoffKind.None).Should().Be(0);
        ((byte)BackoffKind.Spin).Should().Be(1);
        ((byte)BackoffKind.Yield).Should().Be(2);
        ((byte)BackoffKind.Sleep).Should().Be(3);
        ((byte)BackoffKind.AsyncDelay).Should().Be(4);
    }
}

public class BackoffHealthStatusTests
{
    [Fact]
    public void EnumValues_AreCorrect()
    {
        ((byte)BackoffHealthStatus.Healthy).Should().Be(0);
        ((byte)BackoffHealthStatus.Degraded).Should().Be(1);
        ((byte)BackoffHealthStatus.Unhealthy).Should().Be(2);
    }
}

public class BackoffStepResultTests
{
    [Fact]
    public void RecordStruct_PropertiesRoundTrip()
    {
        var result = new BackoffStepResult(5, BackoffKind.Spin, BackoffDuration.FromMicroseconds(10));
        result.Step.Should().Be(5);
        result.Kind.Should().Be(BackoffKind.Spin);
        result.Duration.Should().Be(BackoffDuration.FromMicroseconds(10));
    }
}
