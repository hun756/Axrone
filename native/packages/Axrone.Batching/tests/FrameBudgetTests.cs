namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the frame budget value type.
/// </summary>
/// <remarks>
/// Expiry arithmetic is tested against explicit timestamps read back from <c>StartTicks</c> and
/// <c>DeadlineTicks</c>, so the assertions are deterministic. Only one test sleeps, and it bounds
/// generously enough to survive a loaded CI agent.
/// </remarks>
public class FrameBudgetTests
{
    private static readonly long TicksPerMillisecond = Stopwatch.Frequency / 1000;

    // ── construction ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_TimeSpan_PlacesDeadlineInFuture()
    {
        var budget = new FrameBudget(TimeSpan.FromMilliseconds(16));

        budget.DeadlineTicks.Should().BeGreaterThan(budget.StartTicks);
    }

    [Fact]
    public void Constructor_TimeSpan_DeadlineMatchesRequestedSpan()
    {
        var budget = new FrameBudget(TimeSpan.FromMilliseconds(16));

        var grantedTicks = budget.DeadlineTicks - budget.StartTicks;

        grantedTicks.Should().BeGreaterThan(0);
        Math.Abs(grantedTicks - 16 * TicksPerMillisecond).Should().BeLessThan(2 * TicksPerMillisecond);
    }

    [Fact]
    public void Constructor_Milliseconds_DeadlineMatchesRequestedSpan()
    {
        var budget = new FrameBudget(8d);

        var grantedTicks = budget.DeadlineTicks - budget.StartTicks;

        Math.Abs(grantedTicks - 8 * TicksPerMillisecond).Should().BeLessThan(2 * TicksPerMillisecond);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(-0.001d)]
    public void Constructor_NegativeMilliseconds_Throws(double milliseconds)
    {
        var act = () => new FrameBudget(milliseconds);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeTimeSpan_Throws()
    {
        var act = () => new FrameBudget(TimeSpan.FromMilliseconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ZeroTimeSpan_StartAndDeadlineCoincide()
    {
        var budget = new FrameBudget(TimeSpan.Zero);

        budget.DeadlineTicks.Should().Be(budget.StartTicks);
    }

    // ── expiry ──────────────────────────────────────────────────────────

    [Fact]
    public void IsExpired_AtStart_IsFalse()
    {
        var budget = new FrameBudget(16d);

        budget.IsExpiredAt(budget.StartTicks).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_OneTickBeforeDeadline_IsFalse()
    {
        var budget = new FrameBudget(16d);

        budget.IsExpiredAt(budget.DeadlineTicks - 1).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_AtDeadline_IsTrue()
    {
        var budget = new FrameBudget(16d);

        budget.IsExpiredAt(budget.DeadlineTicks).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_PastDeadline_IsTrue()
    {
        var budget = new FrameBudget(16d);

        budget.IsExpiredAt(budget.DeadlineTicks + TicksPerMillisecond).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ZeroBudget_IsTrueImmediately()
    {
        new FrameBudget(TimeSpan.Zero).IsExpired.Should().BeTrue();
    }

    // ── remaining ───────────────────────────────────────────────────────

    [Fact]
    public void RemainingTicks_AtStart_IsFullBudget()
    {
        var budget = new FrameBudget(16d);

        budget.RemainingTicksAt(budget.StartTicks)
            .Should().Be(budget.DeadlineTicks - budget.StartTicks);
    }

    [Fact]
    public void RemainingTicks_Halfway_IsHalfBudget()
    {
        var budget = new FrameBudget(16d);
        var total = budget.DeadlineTicks - budget.StartTicks;

        budget.RemainingTicksAt(budget.StartTicks + total / 2).Should().Be(total - total / 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1_000_000)]
    public void RemainingTicks_PastDeadline_ClampsToZero(long overshootTicks)
    {
        var budget = new FrameBudget(16d);

        budget.RemainingTicksAt(budget.DeadlineTicks + overshootTicks).Should().Be(0);
    }

    [Fact]
    public void RemainingMilliseconds_AtStart_IsJustUnderGrantedSpan()
    {
        var budget = new FrameBudget(16d);

        budget.RemainingMilliseconds.Should().BeInRange(10d, 17d);
    }

    [Fact]
    public void RemainingMilliseconds_OnExpiredBudget_IsZeroNotNegative()
    {
        new FrameBudget(TimeSpan.Zero).RemainingMilliseconds.Should().Be(0d);
    }

    // ── elapsed ────────────────────────────────────────────────────────

    [Fact]
    public void ElapsedMilliseconds_AtStart_IsZero()
    {
        var budget = new FrameBudget(16d);

        budget.ElapsedMillisecondsAt(budget.StartTicks).Should().Be(0d);
    }

    [Fact]
    public void ElapsedMilliseconds_OneMillisecondLater_IsOne()
    {
        var budget = new FrameBudget(16d);

        budget.ElapsedMillisecondsAt(budget.StartTicks + TicksPerMillisecond)
            .Should().BeApproximately(1d, 0.01d);
    }

    [Fact]
    public void ElapsedMilliseconds_GrowsWithRealTime()
    {
        var budget = new FrameBudget(1000d);

        Thread.Sleep(40);

        budget.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(20d);
    }

    // ── infinite ────────────────────────────────────────────────────────

    [Fact]
    public void Infinite_IsNeverExpired()
    {
        FrameBudget.Infinite.IsExpiredAt(long.MaxValue - 1).Should().BeFalse();
    }

    [Fact]
    public void Infinite_HasVastRemainingBudget()
    {
        FrameBudget.Infinite.RemainingTicks.Should().BeGreaterThan(1L << 40);
    }

    [Fact]
    public void Infinite_StartsAtNow()
    {
        var before = Stopwatch.GetTimestamp();
        var budget = FrameBudget.Infinite;
        var after = Stopwatch.GetTimestamp();

        budget.StartTicks.Should().BeInRange(before, after);
    }
}

/// <summary>Coverage for the three <see cref="SliceResult"/> factories.</summary>
public class SliceResultTests
{
    [Fact]
    public void Empty_ReportsNoWorkAndNoRemainder()
    {
        var result = SliceResult.Empty(0.5);

        result.Status.Should().Be(FrameBudgetStatus.Empty);
        result.ProcessedCount.Should().Be(0);
        result.RemainingCount.Should().Be(0);
        result.ElapsedMilliseconds.Should().Be(0.5);
    }

    [Fact]
    public void Completed_ForcesRemainderToZero()
    {
        var result = SliceResult.Completed(128, 2.25);

        result.Status.Should().Be(FrameBudgetStatus.Completed);
        result.ProcessedCount.Should().Be(128);
        result.RemainingCount.Should().Be(0);
        result.ElapsedMilliseconds.Should().Be(2.25);
    }

    [Fact]
    public void BudgetExceeded_PreservesBothCounts()
    {
        var result = SliceResult.BudgetExceeded(64, 4096, 4.0);

        result.Status.Should().Be(FrameBudgetStatus.BudgetExceeded);
        result.ProcessedCount.Should().Be(64);
        result.RemainingCount.Should().Be(4096);
        result.ElapsedMilliseconds.Should().Be(4.0);
    }

    [Fact]
    public void EnumMembers_ArePinnedToTheirDocumentedValues()
    {
        // A zero-initialised SliceResult must read as Empty, not as a completed batch. The numeric
        // values are therefore contractual, not incidental.
        ((int)FrameBudgetStatus.Empty).Should().Be(0);
        ((int)FrameBudgetStatus.Completed).Should().Be(1);
        ((int)FrameBudgetStatus.BudgetExceeded).Should().Be(2);
    }

    [Fact]
    public void DefaultSlice_ReadsAsEmptyNotCompleted()
    {
        default(SliceResult).Status.Should().Be(FrameBudgetStatus.Empty);
    }
}
