namespace Axrone.Profiler;

/// <summary>Clock lifecycle.</summary>
public enum ClockState : byte
{
    /// <summary>Created, never started or stopped.</summary>
    Stopped = 0,

    /// <summary>Counting.</summary>
    Running = 1,

    /// <summary>Suspended; resume continues.</summary>
    Paused = 2,
}

/// <summary>Requested timestamp resolution; informational on fixed QPC hardware.</summary>
public enum ClockPrecision : byte
{
    /// <summary>Millisecond class.</summary>
    Low = 0,

    /// <summary>Microsecond class.</summary>
    Medium = 1,

    /// <summary>Nanosecond class.</summary>
    High = 2,

    /// <summary>Best available; QueryPerformanceCounter on Windows.</summary>
    Ultra = 3,
}

/// <summary>Point-in-time clock read.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct ClockSnapshot(Nanoseconds Timestamp, long Ticks);

/// <summary>Monotonic high-resolution clock over QueryPerformanceCounter.</summary>
public sealed class MonotonicClock
{
    private readonly string _id;
    private readonly long _frequency = Stopwatch.Frequency;
    private readonly long _startWallMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private long _startTimestamp;
    private long _pausedAccumulated;
    private long _tickCount;
    private int _state = (int)ClockState.Stopped;

    /// <summary>Creates a clock.</summary>
    public MonotonicClock(string id = "monotonic", ClockPrecision precision = ClockPrecision.Ultra)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _id = id;
        Precision = precision;
    }

    /// <summary>Clock identity.</summary>
    public string Id => _id;

    /// <summary>Requested precision.</summary>
    public ClockPrecision Precision { get; }

    /// <summary>Timer frequency in ticks per second.</summary>
    public long ResolutionHz => _frequency;

    /// <summary>Wall time at construction.</summary>
    public long StartWallTimeMs => _startWallMs;

    /// <summary>Lifecycle state.</summary>
    public ClockState State => (ClockState)Volatile.Read(ref _state);

    /// <summary>Recorded ticks.</summary>
    public long TickCount => Volatile.Read(ref _tickCount);

    /// <summary>Starts counting.</summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _state, (int)ClockState.Running, (int)ClockState.Stopped) != (int)ClockState.Stopped)
        {
            ProfilerThrowHelper.ThrowAlreadyRunning(nameof(MonotonicClock));
        }

        Volatile.Write(ref _startTimestamp, Stopwatch.GetTimestamp());
        Volatile.Write(ref _pausedAccumulated, 0L);
    }

    /// <summary>Stops counting.</summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _state, (int)ClockState.Stopped) == (int)ClockState.Stopped)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(MonotonicClock));
        }
    }

    /// <summary>Suspends counting.</summary>
    public void Pause()
    {
        if (Interlocked.CompareExchange(ref _state, (int)ClockState.Paused, (int)ClockState.Running) != (int)ClockState.Running)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(MonotonicClock));
        }

        Interlocked.Add(ref _pausedAccumulated, Stopwatch.GetTimestamp() - Volatile.Read(ref _startTimestamp));
    }

    /// <summary>Continues counting after a pause.</summary>
    public void Resume()
    {
        if (Interlocked.CompareExchange(ref _state, (int)ClockState.Running, (int)ClockState.Paused) != (int)ClockState.Paused)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(MonotonicClock));
        }

        Volatile.Write(ref _startTimestamp, Stopwatch.GetTimestamp());
    }

    /// <summary>Nanoseconds since start, excluding paused spans.</summary>
    public Nanoseconds Now()
    {
        if (State == ClockState.Stopped)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(MonotonicClock));
        }

        long active = State == ClockState.Paused
            ? Volatile.Read(ref _pausedAccumulated)
            : Volatile.Read(ref _pausedAccumulated) + (Stopwatch.GetTimestamp() - Volatile.Read(ref _startTimestamp));
        return Nanoseconds.FromStopwatchTicks(active, _frequency);
    }

    /// <summary>Elapsed span.</summary>
    public Duration Elapsed() => new(Now());

    /// <summary>Span since an earlier point.</summary>
    public Duration ElapsedSince(Nanoseconds point) => new(Now() - point);

    /// <summary>Records a tick; returns its timestamp.</summary>
    public Nanoseconds Tick()
    {
        Interlocked.Increment(ref _tickCount);
        return Now();
    }

    /// <summary>Captures a snapshot.</summary>
    public ClockSnapshot Sample() => new(Now(), Volatile.Read(ref _tickCount));
}

/// <summary>Wall clock over UTC with skew detection.</summary>
public sealed class WallClock
{
    private readonly string _id;
    private int _state = (int)ClockState.Stopped;

    /// <summary>Creates a clock.</summary>
    public WallClock(string id = "wall", long maxSkewMs = 1000L)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (maxSkewMs < 0L)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(maxSkewMs), "Max skew must not be negative.");
        }

        _id = id;
        MaxSkewMs = maxSkewMs;
    }

    /// <summary>Clock identity.</summary>
    public string Id => _id;

    /// <summary>Maximum tolerated skew in milliseconds.</summary>
    public long MaxSkewMs { get; }

    /// <summary>Lifecycle state.</summary>
    public ClockState State => (ClockState)Volatile.Read(ref _state);

    /// <summary>Starts the clock.</summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _state, (int)ClockState.Running, (int)ClockState.Stopped) != (int)ClockState.Stopped)
        {
            ProfilerThrowHelper.ThrowAlreadyRunning(nameof(WallClock));
        }
    }

    /// <summary>Stops the clock.</summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _state, (int)ClockState.Stopped) == (int)ClockState.Stopped)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(WallClock));
        }
    }

    private void ThrowIfStopped()
    {
        if (State == ClockState.Stopped)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(WallClock));
        }
    }

    /// <summary>Current Unix milliseconds.</summary>
    public long ToUnixMs()
    {
        ThrowIfStopped();
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>Current Unix nanoseconds.</summary>
    public Nanoseconds ToUnixNs()
    {
        ThrowIfStopped();
        return new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * TimeConversion.Milliseconds);
    }

    /// <summary>Current time as nanoseconds since the Unix epoch.</summary>
    public Nanoseconds Now() => ToUnixNs();

    /// <summary>Validates an external wall reading against this clock.</summary>
    /// <returns>The observed skew in milliseconds.</returns>
    public long DetectSkew(long wallTimeMs)
    {
        ThrowIfStopped();
        long skew = Math.Abs(ToUnixMs() - wallTimeMs);
        if (skew > MaxSkewMs)
        {
            ProfilerThrowHelper.ThrowSkew(skew, MaxSkewMs);
        }

        return skew;
    }
}

/// <summary>Programmable clock for tests and simulations.</summary>
public sealed class VirtualClock
{
    private readonly string _id;
    private long _nowNs;
    private double _timeScale;
    private int _state = (int)ClockState.Stopped;

    /// <summary>Creates a clock.</summary>
    public VirtualClock(string id = "virtual", long maxTimeNs = long.MaxValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _id = id;
        MaxTimeNs = maxTimeNs;
        _timeScale = 1.0;
    }

    /// <summary>Clock identity.</summary>
    public string Id => _id;

    /// <summary>Upper time bound; advances clamp here.</summary>
    public long MaxTimeNs { get; }

    /// <summary>Current time scale.</summary>
    public double TimeScale => Volatile.Read(ref _timeScale);

    /// <summary>Lifecycle state.</summary>
    public ClockState State => (ClockState)Volatile.Read(ref _state);

    /// <summary>Starts the clock.</summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _state, (int)ClockState.Running, (int)ClockState.Stopped) != (int)ClockState.Stopped)
        {
            ProfilerThrowHelper.ThrowAlreadyRunning(nameof(VirtualClock));
        }
    }

    /// <summary>Stops the clock.</summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _state, (int)ClockState.Stopped) == (int)ClockState.Stopped)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(VirtualClock));
        }
    }

    private void ThrowIfStopped()
    {
        if (State == ClockState.Stopped)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(VirtualClock));
        }
    }

    /// <summary>Current virtual time.</summary>
    public Nanoseconds Now()
    {
        ThrowIfStopped();
        return new(Volatile.Read(ref _nowNs));
    }

    /// <summary>Sets virtual time.</summary>
    public void SetTime(Nanoseconds time)
    {
        ThrowIfStopped();
        if (time.Value < 0L || time.Value > MaxTimeNs)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(time), "Virtual time is outside [0, MaxTimeNs].");
        }

        Volatile.Write(ref _nowNs, time.Value);
    }

    /// <summary>Advances virtual time, scaled and clamped at the bound.</summary>
    public void AdvanceBy(Nanoseconds delta)
    {
        ThrowIfStopped();
        if (delta.Value < 0L)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(delta), "Advance delta must not be negative.");
        }

        double scaled = delta.Value * Volatile.Read(ref _timeScale);
        if (double.IsInfinity(scaled) || double.IsNaN(scaled) || scaled > (double)long.MaxValue)
        {
            ProfilerThrowHelper.ThrowOverflow();
        }

        long next = Volatile.Read(ref _nowNs) + (long)scaled;
        if (next < 0L)
        {
            ProfilerThrowHelper.ThrowOverflow();
        }

        Volatile.Write(ref _nowNs, Math.Min(next, MaxTimeNs));
    }

    /// <summary>Changes the time scale.</summary>
    public void SetTimeScale(double scale)
    {
        ThrowIfStopped();
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale < 0.0)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(scale), "Time scale must be finite and non-negative.");
        }

        Volatile.Write(ref _timeScale, scale);
    }
}

/// <summary>One recorded lap.</summary>
public readonly record struct LapResult(int LapNumber, Duration LapDuration, Duration TotalDuration, Nanoseconds TimestampNs);

/// <summary>Manual stopwatch with lap support. Named to avoid
/// <see cref="Stopwatch"/>; it wraps it instead of replacing it.</summary>
public sealed class ProfilerStopwatch
{
    private long _startTimestamp;
    private long _accumulatedTicks;
    private long _lapCount;
    private long _lastLapTicks;
    private int _running;

    /// <summary>Whether the stopwatch is running.</summary>
    public bool IsRunning => Volatile.Read(ref _running) != 0;

    /// <summary>Starts or resumes.</summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) == 0)
        {
            Volatile.Write(ref _startTimestamp, Stopwatch.GetTimestamp());
        }
    }

    /// <summary>Stops and banks the span.</summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _running, 0) == 0)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(ProfilerStopwatch));
        }

        Interlocked.Add(ref _accumulatedTicks, Stopwatch.GetTimestamp() - Volatile.Read(ref _startTimestamp));
    }

    /// <summary>Clears all banks.</summary>
    public void Reset()
    {
        Volatile.Write(ref _accumulatedTicks, 0L);
        Volatile.Write(ref _lastLapTicks, 0L);
        Volatile.Write(ref _lapCount, 0L);
        if (IsRunning)
        {
            Volatile.Write(ref _startTimestamp, Stopwatch.GetTimestamp());
        }
    }

    /// <summary>Clears and starts.</summary>
    public void Restart()
    {
        Volatile.Write(ref _accumulatedTicks, 0L);
        Volatile.Write(ref _lastLapTicks, 0L);
        Volatile.Write(ref _lapCount, 0L);
        Volatile.Write(ref _startTimestamp, Stopwatch.GetTimestamp());
        Volatile.Write(ref _running, 1);
    }

    private long ActiveTicks() => IsRunning
        ? Volatile.Read(ref _accumulatedTicks) + (Stopwatch.GetTimestamp() - Volatile.Read(ref _startTimestamp))
        : Volatile.Read(ref _accumulatedTicks);

    /// <summary>Elapsed span, if ever started.</summary>
    public Duration? Elapsed()
    {
        if (!IsRunning && Volatile.Read(ref _accumulatedTicks) == 0L && Volatile.Read(ref _lapCount) == 0L)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(ProfilerStopwatch));
        }

        return new Duration(Nanoseconds.FromStopwatchTicks(ActiveTicks(), Stopwatch.Frequency));
    }

    /// <summary>Records a lap; null while stopped with an empty bank.</summary>
    public LapResult? Lap()
    {
        if (!IsRunning)
        {
            return null;
        }

        long active = ActiveTicks();
        long lapTicks = active - Volatile.Read(ref _lastLapTicks);
        Volatile.Write(ref _lastLapTicks, active);
        long number = Interlocked.Increment(ref _lapCount);
        return new LapResult(
            (int)number,
            new Duration(Nanoseconds.FromStopwatchTicks(lapTicks, Stopwatch.Frequency)),
            new Duration(Nanoseconds.FromStopwatchTicks(active, Stopwatch.Frequency)),
            new Nanoseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * TimeConversion.Milliseconds));
    }
}
