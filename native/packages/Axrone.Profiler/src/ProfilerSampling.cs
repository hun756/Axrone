namespace Axrone.Profiler;

/// <summary>Sampler runtime snapshot.</summary>
public readonly record struct SamplerState(
    double IntervalMs,
    long TickCount,
    double LastDurationMs,
    bool IsRunning,
    bool IsAdaptive);

/// <summary>Periodic sampler with overhead-driven adaptive intervals.</summary>
public sealed class ContinuousSampler : IDisposable, IAsyncDisposable
{
    private readonly Action<Nanoseconds> _onTick;
    private readonly double _minIntervalMs;
    private readonly double _maxIntervalMs;
    private readonly object _gate = new();
    private Timer? _timer;
    private double _intervalMs;
    private double _targetFrameBudgetMs;
    private bool _adaptive;
    private long _tickCount;
    private double _lastDurationMs;
    private int _disposed;

    /// <summary>Creates a sampler.</summary>
    public ContinuousSampler(
        Action<Nanoseconds> onTick,
        double intervalMs = 10.0,
        double minIntervalMs = 1.0,
        double maxIntervalMs = 100.0,
        double targetFrameBudgetMs = 16.0,
        bool adaptive = true)
    {
        ArgumentNullException.ThrowIfNull(onTick);
        if (!(intervalMs >= minIntervalMs && intervalMs <= maxIntervalMs) || double.IsNaN(intervalMs) || double.IsInfinity(intervalMs))
        {
            ProfilerThrowHelper.ThrowArgument(nameof(intervalMs), "Interval must sit inside [min, max].");
        }

        if (!(minIntervalMs >= 1.0) || double.IsNaN(minIntervalMs) || double.IsInfinity(minIntervalMs))
        {
            ProfilerThrowHelper.ThrowArgument(nameof(minIntervalMs), "Minimum interval must be at least 1 ms.");
        }

        if (!(maxIntervalMs >= minIntervalMs) || double.IsNaN(maxIntervalMs) || double.IsInfinity(maxIntervalMs))
        {
            ProfilerThrowHelper.ThrowArgument(nameof(maxIntervalMs), "Maximum interval must cover the minimum.");
        }

        if (!(targetFrameBudgetMs > 0.0) || double.IsNaN(targetFrameBudgetMs) || double.IsInfinity(targetFrameBudgetMs))
        {
            ProfilerThrowHelper.ThrowArgument(nameof(targetFrameBudgetMs), "Frame budget must be positive.");
        }

        _onTick = onTick;
        _intervalMs = intervalMs;
        _minIntervalMs = minIntervalMs;
        _maxIntervalMs = maxIntervalMs;
        _targetFrameBudgetMs = targetFrameBudgetMs;
        _adaptive = adaptive;
    }

    /// <summary>Current interval.</summary>
    public double IntervalMs
    {
        get { lock (_gate) { return _intervalMs; } }
    }

    /// <summary>Total ticks fired.</summary>
    public long TickCount => Interlocked.Read(ref _tickCount);

    /// <summary>Last tick body duration.</summary>
    public double LastDurationMs => Volatile.Read(ref _lastDurationMs);

    /// <summary>Whether the timer is armed.</summary>
    public bool IsRunning
    {
        get { lock (_gate) { return _timer is not null; } }
    }

    /// <summary>Starts sampling.</summary>
    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _timer ??= new Timer(OnTick, null, TimeSpan.FromMilliseconds(_intervalMs), TimeSpan.FromMilliseconds(_intervalMs));
        }
    }

    /// <summary>Stops sampling.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    /// <summary>Changes the interval.</summary>
    public void SetInterval(double intervalMs)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!(intervalMs >= _minIntervalMs) || intervalMs > _maxIntervalMs || double.IsNaN(intervalMs) || double.IsInfinity(intervalMs))
            {
                ProfilerThrowHelper.ThrowArgument(nameof(intervalMs), "Interval must sit inside [min, max].");
            }

            _intervalMs = intervalMs;
            _timer?.Change(TimeSpan.FromMilliseconds(_intervalMs), TimeSpan.FromMilliseconds(_intervalMs));
        }
    }

    /// <summary>Toggles adaptation.</summary>
    public void SetAdaptation(bool enabled)
    {
        lock (_gate)
        {
            _adaptive = enabled;
        }
    }

    /// <summary>Changes the frame budget the adaptation steers by.</summary>
    public void SetTargetFrameBudget(double budgetMs)
    {
        lock (_gate)
        {
            if (!(budgetMs > 0.0) || double.IsNaN(budgetMs) || double.IsInfinity(budgetMs))
            {
                ProfilerThrowHelper.ThrowArgument(nameof(budgetMs), "Frame budget must be positive.");
            }

            _targetFrameBudgetMs = budgetMs;
        }
    }

    /// <summary>Returns a snapshot of the sampler state.</summary>
    public SamplerState GetState()
    {
        lock (_gate)
        {
            return new SamplerState(_intervalMs, Interlocked.Read(ref _tickCount), Volatile.Read(ref _lastDurationMs), _timer is not null, _adaptive);
        }
    }

    private void OnTick(object? _)
    {
        long start = Stopwatch.GetTimestamp();
        Nanoseconds timestamp = new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * TimeConversion.Milliseconds);
        try
        {
            _onTick(timestamp);
        }
        catch (Exception ex)
        {
            throw new ProfilerTickException("Sampler tick callback failed.", ex);
        }
        finally
        {
            double elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            Volatile.Write(ref _lastDurationMs, elapsedMs);
            Interlocked.Increment(ref _tickCount);

            lock (_gate)
            {
                if (_adaptive && _timer is not null)
                {
                    double next = elapsedMs > _targetFrameBudgetMs
                        ? Math.Min(_intervalMs * 2.0, _maxIntervalMs)
                        : Math.Max(_intervalMs / 2.0, _minIntervalMs);
                    if (next != _intervalMs)
                    {
                        _intervalMs = next;
                        _timer.Change(TimeSpan.FromMilliseconds(_intervalMs), TimeSpan.FromMilliseconds(_intervalMs));
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _timer?.Dispose();
            _timer = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
