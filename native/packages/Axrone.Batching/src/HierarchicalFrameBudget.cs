namespace Axrone.Batching;

/// <summary>
/// A frame budget that partitions its remaining window across child systems and accounts for what
/// each one actually consumed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AllocateChild"/> grants a child a fraction of the parent's <em>remaining</em> window
/// measured at the moment of the call. Allocating every child up front therefore partitions the
/// frame exactly — <c>0.4 + 0.4 + 0.2</c> sums to the whole budget. Interleaving allocation with
/// execution partitions a shrinking window instead, and issuing fractions that sum above one will
/// over-commit; nothing here prevents that, because the same call pattern is correct when the
/// fractions are meant against what is left.
/// </para>
/// <para>
/// This is deliberately a reference type. Child accounting mutates shared state, and a value type
/// would fork that accounting silently the moment a budget was copied, passed by value, or boxed.
/// </para>
/// <para>
/// <see cref="OverheadMilliseconds"/> assumes children ran sequentially. Concurrent children each
/// report their own wall-clock span against a single parent span, so the subtraction can go
/// negative; it is clamped to zero rather than reported as a meaningless figure.
/// </para>
/// </remarks>
public sealed class HierarchicalFrameBudget
{
    private readonly long _startTicks;
    private readonly long _deadlineTicks;
    private readonly HierarchicalFrameBudget? _parent;

    // Ticks reported back by children. Interlocked because a scheduler may complete children on
    // different threads; safe here only because this is a reference type.
    private long _childConsumedTicks;

    /// <summary>Ticks at which this budget started.</summary>
    public long StartTicks
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _startTicks;
    }

    /// <summary>Ticks at which this budget expires.</summary>
    public long DeadlineTicks
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _deadlineTicks;
    }

    /// <summary>The budget this one was carved out of, or <see langword="null"/> for a root.</summary>
    public HierarchicalFrameBudget? Parent => _parent;

    /// <summary>Creates a root budget of <paramref name="totalBudget"/>, starting now.</summary>
    /// <param name="totalBudget">Total window available. Zero is legal and expires immediately.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="totalBudget"/> is negative.</exception>
    public HierarchicalFrameBudget(TimeSpan totalBudget)
    {
        if (totalBudget < TimeSpan.Zero)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(totalBudget));
        }

        _startTicks = Stopwatch.GetTimestamp();
        _deadlineTicks = _startTicks + (long)(totalBudget.TotalSeconds * Stopwatch.Frequency);
    }

    /// <summary>Creates a root budget of <paramref name="totalMilliseconds"/>, starting now.</summary>
    /// <param name="totalMilliseconds">Total window available. Zero is legal and expires immediately.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="totalMilliseconds"/> is negative.</exception>
    public HierarchicalFrameBudget(double totalMilliseconds)
    {
        if (totalMilliseconds < 0d)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(totalMilliseconds));
        }

        _startTicks = Stopwatch.GetTimestamp();
        _deadlineTicks = _startTicks + (long)(totalMilliseconds * (Stopwatch.Frequency / 1000d));
    }

    private HierarchicalFrameBudget(HierarchicalFrameBudget parent, long startTicks, long deadlineTicks)
    {
        _parent = parent;
        _startTicks = startTicks;
        _deadlineTicks = deadlineTicks;
    }

    /// <summary>Whether the budget has run out as of now.</summary>
    public bool IsExpired
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsExpiredAt(Stopwatch.GetTimestamp());
    }

    /// <summary>Whether the budget had run out as of <paramref name="timestamp"/>.</summary>
    /// <param name="timestamp">A value previously returned by <see cref="Stopwatch.GetTimestamp"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExpiredAt(long timestamp) => timestamp >= _deadlineTicks;

    /// <summary>Ticks left before expiry as of now, clamped at zero.</summary>
    public long RemainingTicks
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RemainingTicksAt(Stopwatch.GetTimestamp());
    }

    /// <summary>Ticks left before expiry as of <paramref name="timestamp"/>, clamped at zero.</summary>
    /// <param name="timestamp">A value previously returned by <see cref="Stopwatch.GetTimestamp"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long RemainingTicksAt(long timestamp)
    {
        var remaining = _deadlineTicks - timestamp;
        return remaining > 0 ? remaining : 0;
    }

    /// <summary>Milliseconds until expiry, clamped at zero.</summary>
    public double RemainingMilliseconds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RemainingTicksAt(Stopwatch.GetTimestamp()) * 1000d / Stopwatch.Frequency;
    }

    /// <summary>Milliseconds elapsed since this budget started.</summary>
    public double ElapsedMilliseconds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ElapsedMillisecondsAt(Stopwatch.GetTimestamp());
    }

    /// <summary>Milliseconds elapsed since this budget started, as of <paramref name="timestamp"/>.</summary>
    /// <param name="timestamp">A value previously returned by <see cref="Stopwatch.GetTimestamp"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ElapsedMillisecondsAt(long timestamp) =>
        (timestamp - _startTicks) * 1000d / Stopwatch.Frequency;

    /// <summary>
    /// Carves a child budget worth <paramref name="fraction"/> of this budget's remaining window.
    /// </summary>
    /// <param name="fraction">Share of the remaining window, in the exclusive range (0, 1].</param>
    /// <returns>A child budget that reports its consumption back to this one on <see cref="Complete"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fraction"/> is not in (0, 1].</exception>
    public HierarchicalFrameBudget AllocateChild(double fraction)
    {
        if (fraction <= 0d || fraction > 1d)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(fraction));
        }

        var now = Stopwatch.GetTimestamp();
        var available = RemainingTicksAt(now);
        var childStart = now;

        return new HierarchicalFrameBudget(this, childStart, childStart + (long)(available * fraction));
    }

    /// <summary>
    /// Reports this budget's wall-clock consumption to its parent. A root budget ignores the call.
    /// </summary>
    /// <remarks>
    /// Calling this twice on the same child double-counts its consumption; the child does not track
    /// whether it has already reported, because a system may legitimately be scheduled in several
    /// passes within one frame.
    /// </remarks>
    public void Complete()
    {
        _parent?.RecordConsumedTicks(Stopwatch.GetTimestamp() - _startTicks);
    }

    /// <summary>Total milliseconds consumed by children that have called <see cref="Complete"/>.</summary>
    public double ChildConsumedMilliseconds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.Read(ref _childConsumedTicks) * 1000d / Stopwatch.Frequency;
    }

    /// <summary>
    /// Milliseconds this budget spent outside of child execution — scheduling, dispatch, and
    /// bookkeeping. Clamped at zero; see the remarks on <see cref="HierarchicalFrameBudget"/> for
    /// why concurrent children make this figure meaningless.
    /// </summary>
    public double OverheadMilliseconds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var totalElapsed = Stopwatch.GetTimestamp() - _startTicks;
            var overhead = totalElapsed - Interlocked.Read(ref _childConsumedTicks);
            return overhead > 0 ? overhead * 1000d / Stopwatch.Frequency : 0d;
        }
    }

    private void RecordConsumedTicks(long ticks)
    {
        if (ticks > 0)
        {
            Interlocked.Add(ref _childConsumedTicks, ticks);
        }
    }
}
