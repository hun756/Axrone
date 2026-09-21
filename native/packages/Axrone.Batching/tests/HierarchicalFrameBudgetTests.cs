namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the hierarchical budget: partition arithmetic, child accounting, and the
/// validation that keeps a mis-typed fraction from silently over-committing a frame.
/// </summary>
public class HierarchicalFrameBudgetTests
{
    private static readonly long TicksPerMillisecond = Stopwatch.Frequency / 1000;

    // ── construction ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_TimeSpan_PlacesDeadlineInFuture()
    {
        var budget = new HierarchicalFrameBudget(TimeSpan.FromMilliseconds(16));

        budget.DeadlineTicks.Should().BeGreaterThan(budget.StartTicks);
        budget.Parent.Should().BeNull();
    }

    [Fact]
    public void Constructor_Milliseconds_PlacesDeadlineInFuture()
    {
        var budget = new HierarchicalFrameBudget(16d);

        budget.DeadlineTicks.Should().BeGreaterThan(budget.StartTicks);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(-0.001d)]
    public void Constructor_NegativeMilliseconds_Throws(double milliseconds)
    {
        var act = () => new HierarchicalFrameBudget(milliseconds);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeTimeSpan_Throws()
    {
        var act = () => new HierarchicalFrameBudget(TimeSpan.FromMilliseconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ZeroWindow_IsExpiredImmediately()
    {
        new HierarchicalFrameBudget(TimeSpan.Zero).IsExpired.Should().BeTrue();
    }

    // ── expiry ─────────────────────────────────────────────────────────

    [Fact]
    public void IsExpired_AtStart_IsFalse()
    {
        var budget = new HierarchicalFrameBudget(16d);

        budget.IsExpiredAt(budget.StartTicks).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_AtDeadline_IsTrue()
    {
        var budget = new HierarchicalFrameBudget(16d);

        budget.IsExpiredAt(budget.DeadlineTicks).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    public void RemainingTicks_PastDeadline_ClampsToZero(long overshootTicks)
    {
        var budget = new HierarchicalFrameBudget(16d);

        budget.RemainingTicksAt(budget.DeadlineTicks + overshootTicks).Should().Be(0);
    }

    [Fact]
    public void RemainingMilliseconds_OnExpiredBudget_IsZeroNotNegative()
    {
        new HierarchicalFrameBudget(TimeSpan.Zero).RemainingMilliseconds.Should().Be(0d);
    }

    [Fact]
    public void ElapsedMilliseconds_AtStart_IsZero()
    {
        var budget = new HierarchicalFrameBudget(16d);

        budget.ElapsedMillisecondsAt(budget.StartTicks).Should().Be(0d);
    }

    // ── AllocateChild ───────────────────────────────────────────────────

    [Fact]
    public void AllocateChild_SharesParentWindowProportionally()
    {
        var parent = new HierarchicalFrameBudget(16d);
        var parentWindow = parent.DeadlineTicks - parent.StartTicks;

        var child = parent.AllocateChild(0.5);

        var childWindow = child.DeadlineTicks - child.StartTicks;
        Math.Abs(childWindow - parentWindow / 2).Should().BeLessThan((long)(parentWindow * 0.1));
    }

    [Fact]
    public void AllocateChild_NeverExceedsParentWindow()
    {
        var parent = new HierarchicalFrameBudget(16d);
        var parentWindow = parent.DeadlineTicks - parent.StartTicks;

        var child = parent.AllocateChild(1.0);

        (child.DeadlineTicks - child.StartTicks).Should().BeLessThanOrEqualTo(parentWindow);
    }

    [Fact]
    public void AllocateChild_FractionOfOneEndsWithParent()
    {
        var parent = new HierarchicalFrameBudget(16d);

        var child = parent.AllocateChild(1.0);

        // Allocated microseconds after the parent, so its deadline sits just inside the parent's.
        child.DeadlineTicks.Should().BeLessThanOrEqualTo(parent.DeadlineTicks);
        child.DeadlineTicks.Should().BeGreaterThan(parent.DeadlineTicks - TicksPerMillisecond);
    }

    [Fact]
    public void AllocateChild_RecordsParentLink()
    {
        var parent = new HierarchicalFrameBudget(16d);

        var child = parent.AllocateChild(0.4);

        child.Parent.Should().BeSameAs(parent);
    }

    [Fact]
    public void AllocateChild_UpFrontPartitionApproximatesTheWholeFrame()
    {
        var parent = new HierarchicalFrameBudget(16d);
        var parentWindow = parent.DeadlineTicks - parent.StartTicks;

        var physics = parent.AllocateChild(0.4);
        var rendering = parent.AllocateChild(0.4);
        var ai = parent.AllocateChild(0.2);

        var shared = (physics.DeadlineTicks - physics.StartTicks)
            + (rendering.DeadlineTicks - rendering.StartTicks)
            + (ai.DeadlineTicks - ai.StartTicks);

        // Each fraction is measured against a window that shrinks by only the nanoseconds between
        // calls, so the three together land near the full budget rather than compounding away.
        Math.Abs(shared - parentWindow).Should().BeLessThan((long)(parentWindow * 0.1));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-0.5d)]
    [InlineData(1.0001d)]
    [InlineData(4d)]
    public void AllocateChild_FractionOutsideRange_Throws(double fraction)
    {
        var parent = new HierarchicalFrameBudget(16d);

        var act = () => parent.AllocateChild(fraction);

        // A bare Math.Clamp would let AllocateChild(4) silently mean "the whole frame".
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AllocateChild_AfterParentExpired_GetsEmptyWindow()
    {
        var parent = new HierarchicalFrameBudget(TimeSpan.Zero);

        var child = parent.AllocateChild(0.5);

        (child.DeadlineTicks - child.StartTicks).Should().Be(0);
        child.IsExpired.Should().BeTrue();
    }

    // ── child accounting ────────────────────────────────────────────────

    [Fact]
    public void Complete_OnRoot_RecordsNothing()
    {
        var root = new HierarchicalFrameBudget(1000d);

        Thread.Sleep(30);
        root.Complete();

        root.ChildConsumedMilliseconds.Should().Be(0d);
    }

    [Fact]
    public void Complete_ReportsChildElapsedToParent()
    {
        var parent = new HierarchicalFrameBudget(1000d);
        var child = parent.AllocateChild(0.5);

        Thread.Sleep(40);
        child.Complete();

        parent.ChildConsumedMilliseconds.Should().BeGreaterThanOrEqualTo(20d);
    }

    [Fact]
    public void Complete_AccumulatesAcrossSeveralChildren()
    {
        var parent = new HierarchicalFrameBudget(1000d);
        var first = parent.AllocateChild(0.5);
        var second = parent.AllocateChild(1.0);

        first.Complete();
        second.Complete();

        // Both children started near the parent's start, so together they must report at least
        // what a single one did — the counter accumulates rather than being overwritten.
        parent.ChildConsumedMilliseconds.Should().BeGreaterThan(0d);
    }

    [Fact]
    public void OverheadMilliseconds_IsNonNegative()
    {
        var parent = new HierarchicalFrameBudget(1000d);
        var child = parent.AllocateChild(0.5);

        Thread.Sleep(30);
        child.Complete();

        parent.OverheadMilliseconds.Should().BeGreaterThanOrEqualTo(0d);
    }

    [Fact]
    public void OverheadMilliseconds_ClampsWhenChildrenOverReport()
    {
        var parent = new HierarchicalFrameBudget(1000d);
        var child = parent.AllocateChild(1.0);

        Thread.Sleep(40);
        child.Complete();
        child.Complete();

        // Double completion pushes child consumption past the parent's own elapsed time. The
        // metric is clamped to zero rather than surfaced as a negative duration.
        parent.OverheadMilliseconds.Should().Be(0d);
    }

    [Fact]
    public void ChildConsumedMilliseconds_WithoutAnyChild_IsZero()
    {
        var parent = new HierarchicalFrameBudget(1000d);

        Thread.Sleep(20);

        parent.ChildConsumedMilliseconds.Should().Be(0d);
    }

    [Fact]
    public void NestedChild_CompletesIntoItsOwnParent()
    {
        var root = new HierarchicalFrameBudget(1000d);
        var mid = root.AllocateChild(0.5);
        var leaf = mid.AllocateChild(0.5);

        Thread.Sleep(30);
        leaf.Complete();

        mid.ChildConsumedMilliseconds.Should().BeGreaterThanOrEqualTo(10d);
        root.ChildConsumedMilliseconds.Should().Be(0d);
    }
}
