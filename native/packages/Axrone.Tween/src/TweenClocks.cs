namespace Axrone.Tween;

/// <summary>Wall-clock source driven by <see cref="Stopwatch"/>.</summary>
public sealed class StopwatchTweenClock : ITweenClock
{
    private long _lastTimestamp;

    /// <summary>Creates a clock starting now.</summary>
    public StopwatchTweenClock() => _lastTimestamp = Stopwatch.GetTimestamp();

    /// <inheritdoc/>
    public TimestampNs Now() =>
        new((long)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency)));

    /// <inheritdoc/>
    public DurationNs Tick()
    {
        long current = Stopwatch.GetTimestamp();
        long deltaTicks = current - Interlocked.Exchange(ref _lastTimestamp, current);
        return new DurationNs((long)(deltaTicks * (1_000_000_000.0 / Stopwatch.Frequency)));
    }
}

/// <summary>Manually advanced clock for deterministic tests and fixed-step simulation.</summary>
public sealed class ManualTweenClock : ITweenClock
{
    private TimestampNs _now;
    private DurationNs _delta;

    /// <summary>Creates a clock with a default 60fps step.</summary>
    public ManualTweenClock()
    {
        _now = default;
        _delta = DurationNs.FromMilliseconds(16.666f);
    }

    /// <summary>Advances the clock.</summary>
    public void Advance(DurationNs delta)
    {
        _delta = delta;
        _now += delta;
    }

    /// <inheritdoc/>
    public TimestampNs Now() => _now;

    /// <inheritdoc/>
    public DurationNs Tick() => _delta;
}
