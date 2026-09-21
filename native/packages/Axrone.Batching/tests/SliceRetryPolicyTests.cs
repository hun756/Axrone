namespace Axrone.Batching.Tests;

using Axrone.Utility.Backoff.SpinPolicies;

/// <summary>
/// Coverage for the slice retry policy and bounded spinning.
/// </summary>
public class SliceRetryPolicyTests
{
    private struct SpinKernel : IBatchKernel<int>
    {
        public void Execute(Span<int> batch) => Thread.SpinWait(20000);
    }

    [Fact]
    public void ShouldRetry_RespectsBounds()
    {
        var policy = new SliceRetryPolicy(1000, 1000000, 100, 2);

        policy.ShouldRetry(0).Should().BeFalse();
        policy.ShouldRetry(1).Should().BeTrue();
        policy.ShouldRetry(2).Should().BeTrue();
        policy.ShouldRetry(3).Should().BeFalse();
    }

    [Fact]
    public void DelayTicks_GrowsExponentially_ThenCaps()
    {
        var policy = new SliceRetryPolicy(1000, 5000, 0, 10);
        var rng = 0x9E3779B97F4A7C15UL;

        var first = policy.DelayTicks(1, ref rng);
        var second = policy.DelayTicks(2, ref rng);
        var capped = policy.DelayTicks(100, ref rng);

        first.Should().Be(1000);
        second.Should().Be(2000);
        capped.Should().Be(5000);
    }

    [Fact]
    public void DelayTicks_JitterStaysInSpan()
    {
        var policy = new SliceRetryPolicy(1000, 1000000, 500, 3);

        for (var i = 0; i < 50; i++)
        {
            var rng = (ulong)i + 1;
            var delay = policy.DelayTicks(1, ref rng);
            delay.Should().BeInRange(1000, 1499);
        }
    }

    [Fact]
    public void NonePolicy_NeverRetries()
    {
        SliceRetryPolicy.None.ShouldRetry(1).Should().BeFalse();
        SliceRetryPolicy.Default.MaxAttempts.Should().Be(3);
    }

    [Fact]
    public void BoundedSpinning_StopsWithRemainder()
    {
        using var pipeline = TimeSlicedPipeline.Create<int>(5000).Build();
        for (var i = 0; i < 5000; i++)
        {
            pipeline.TryWrite(i);
        }

        pipeline.SwapProducer();

        var kernel = new SpinKernel();
        var overruns = pipeline.ExecuteSpinning<SpinKernel, ProgressiveSpinBackoff>(
            ref kernel, new SliceRetryPolicy(1000, 1000000, 0, 1));

        overruns.Should().BeGreaterThanOrEqualTo(1);
        pipeline.HasRemainingWork.Should().BeTrue();
    }
}
