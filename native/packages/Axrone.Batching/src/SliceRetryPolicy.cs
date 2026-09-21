namespace Axrone.Batching;

/// <summary>
/// Bounded exponential retry with jitter for slice overruns.
/// </summary>
/// <remarks>
/// Answers a narrower question than <see cref="ISpinBackoff"/>: not how to wait, but whether to
/// try again and for how long to stand down. Pacing stays with the backoff policy; this only
/// bounds the attempt count and computes the stand-down. Randomness comes from the caller's
/// <c>ref ulong</c> PCG state (<c>Axrone.Utility.Backoff.FastPcgRng</c>) — no <see cref="Random"/>
/// allocation, deterministic under a fixed seed.
/// </remarks>
public readonly record struct SliceRetryPolicy
{
    /// <summary>Base stand-down in Stopwatch ticks (~125µs, one target slice).</summary>
    public long BaseDelayTicks { get; }

    /// <summary>Stand-down ceiling in Stopwatch ticks.</summary>
    public long MaxDelayTicks { get; }

    /// <summary>Jitter span in Stopwatch ticks.</summary>
    public long JitterSpanTicks { get; }

    /// <summary>Overrun slices tolerated before giving up.</summary>
    public int MaxAttempts { get; }

    /// <summary>No retries: the first overrun stops the spin.</summary>
    public static SliceRetryPolicy None => new(0, 0, 0, 0);

    /// <summary>Slice-frequency divisor shared with the pipeline calibrator (125µs target).</summary>
    private const int TargetSliceFrequencyDivisor = 8000;

    /// <summary>House tuning: 3 attempts, one-slice base, one-second ceiling, 1ms jitter.</summary>
    public static SliceRetryPolicy Default => new(
        Stopwatch.Frequency / TargetSliceFrequencyDivisor,
        Stopwatch.Frequency,
        Stopwatch.Frequency / 1000,
        3);

    /// <summary>Creates a policy.</summary>
    public SliceRetryPolicy(long baseDelayTicks, long maxDelayTicks, long jitterSpanTicks, int maxAttempts)
    {
        if (baseDelayTicks < 0 || maxDelayTicks < 0 || jitterSpanTicks < 0 || maxAttempts < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(maxAttempts));
        }

        BaseDelayTicks = baseDelayTicks;
        MaxDelayTicks = maxDelayTicks;
        JitterSpanTicks = jitterSpanTicks;
        MaxAttempts = maxAttempts;
    }

    /// <summary>Whether another attempt is allowed after <paramref name="overruns"/> overruns.</summary>
    /// <param name="overruns">Overrun slices so far, starting at 1.</param>
    public bool ShouldRetry(int overruns) => overruns >= 1 && overruns <= MaxAttempts;

    /// <summary>Stand-down for the <paramref name="overruns"/>-th overrun.</summary>
    /// <param name="overruns">Overrun count, starting at 1.</param>
    /// <param name="rngState">Caller PCG state, advanced by the jitter draw.</param>
    public long DelayTicks(int overruns, ref ulong rngState)
    {
        var shift = Math.Min(Math.Max(overruns, 1) - 1, 20);
        var backoff = BaseDelayTicks << shift;
        if (backoff < 0 || backoff > MaxDelayTicks)
        {
            backoff = MaxDelayTicks;
        }

        var jitter = JitterSpanTicks > 0 ? (long)(FastPcgRng.Next(ref rngState) % (ulong)JitterSpanTicks) : 0;
        var total = backoff + jitter;
        return total < 0 ? MaxDelayTicks : total;
    }
}
