namespace Axrone.Profiler;

/// <summary>Profiler capture mode.</summary>
public enum ProfilerMode : byte
{
    /// <summary>Periodic heartbeat sampling plus manual timers.</summary>
    Sampled = 0,

    /// <summary>Timer recording without heartbeats.</summary>
    Continuous = 1,

    /// <summary>Manual timers only.</summary>
    OnDemand = 2,
}

/// <summary>Profiler session options.</summary>
public readonly record struct ProfilerOptions(
    ProfilerMode Mode = ProfilerMode.OnDemand,
    int BufferCapacity = 1024,
    bool AutoStart = false,
    Action? OnFrameEnd = null);

/// <summary>Profiler self-cost and volume counters.</summary>
public readonly record struct OverheadMetrics(long OverheadNs, int TimerCount, int MarkerCount);

/// <summary>
/// Profiling facade: timer hierarchy, markers, overhead accounting,
/// timeline and optional heartbeat sampling over a shared monotonic clock.
/// </summary>
public sealed class Profiler : IDisposable, IAsyncDisposable
{
    private readonly struct ActiveTimer
    {
        public readonly int Id;
        public readonly string Name;
        public readonly long StartTimestamp;

        public ActiveTimer(int id, string name, long startTimestamp)
        {
            Id = id;
            Name = name;
            StartTimestamp = startTimestamp;
        }
    }

    private readonly ProfilerOptions _options;
    private readonly FlameGraphBuilder _flames = new();
    private readonly LogHistogram _histogram = new();
    private readonly MonotonicClock _clock = new("profiler");
    private readonly TimelineRecorder _timeline = new();
    private readonly Lock _gate = new();
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private readonly Stack<ActiveTimer> _stack = new();
    private readonly Dictionary<int, ActiveTimer> _paused = new();
    private ContinuousSampler? _sampler;
    private MemoryTracker? _memory;
    private int _nextTimerId = 1;
    private long _completedTimers;
    private long _markers;
    private long _overheadTicks;
    private long _startedAtMs;
    private long _endedAtMs;
    private int _started;
    private int _disposed;

    /// <summary>Creates a profiler.</summary>
    public Profiler(ProfilerOptions options = default)
    {
        if (options.BufferCapacity < 0)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(options), "Buffer capacity must be positive.");
        }

        _options = options.BufferCapacity == 0
            ? new ProfilerOptions(options.Mode, BufferCapacity: 1024, options.AutoStart)
            : options;
        if (options.AutoStart)
        {
            Start();
        }
    }

    /// <summary>Session id.</summary>
    public string SessionId => _sessionId;

    private void AddOverhead(long startTimestamp) =>
        Interlocked.Add(ref _overheadTicks, Stopwatch.GetTimestamp() - startTimestamp);

    private void ThrowIfStopped()
    {
        if (Volatile.Read(ref _started) == 0)
        {
            ProfilerThrowHelper.ThrowNotRunning(nameof(Profiler));
        }
    }

    /// <summary>Starts the session.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            throw new ProfilerStartupException("Profiler session is already running.");
        }

        Volatile.Write(ref _endedAtMs, 0L);
        Volatile.Write(ref _startedAtMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _clock.Start();
        _timeline.Start();
        if (_options.Mode == ProfilerMode.Sampled && _sampler is null)
        {
            EnableSampling();
        }
    }

    /// <summary>Stops the session.</summary>
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.CompareExchange(ref _started, 0, 1) != 1)
        {
            throw new ProfilerShutdownException("Profiler session is not running.");
        }

        Volatile.Write(ref _endedAtMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _sampler?.Stop();
        _timeline.Stop();
        _clock.Stop();
    }

    /// <summary>Starts a timer; returns its id.</summary>
    public int AddTimer(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ThrowIfStopped();

        long start = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            int id = _nextTimerId++;
            _stack.Push(new ActiveTimer(id, name, _clock.Now().Value));
            _flames.BeginFrame(name);
            AddOverhead(start);
            return id;
        }
    }

    private ActiveTimer PopTop(int? timerId, string operation)
    {
        if (_stack.Count == 0)
        {
            ProfilerThrowHelper.ThrowArgument(operation, "No active timer to close.");
        }

        ActiveTimer top = _stack.Peek();
        if (timerId.HasValue && timerId.Value != top.Id)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(timerId), "Timers close in LIFO order.");
        }

        return _stack.Pop();
    }

    private void CompleteTimer(ActiveTimer timer, bool record)
    {
        Nanoseconds now = _clock.Now();
        long duration = Math.Max(0L, now.Value - timer.StartTimestamp);
        if (record)
        {
            _flames.EndFrame(duration);
            _histogram.Record(duration / 1_000_000.0);
            Interlocked.Increment(ref _completedTimers);
        }
        else
        {
            _flames.AbortFrame();
        }
    }

    /// <summary>Ends a timer; without an id closes the innermost.</summary>
    public void EndTimer(int? timerId = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ThrowIfStopped();

        long start = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            CompleteTimer(PopTop(timerId, nameof(EndTimer)), record: true);
            AddOverhead(start);
        }
    }

    /// <summary>Pauses the innermost timer.</summary>
    public void PauseTimer(int? timerId = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ThrowIfStopped();

        long start = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            ActiveTimer timer = PopTop(timerId, nameof(PauseTimer));
            _paused[timer.Id] = timer;
            _flames.AbortFrame();
            AddOverhead(start);
        }
    }

    /// <summary>Resumes a paused timer.</summary>
    public void ResumeTimer(int timerId)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ThrowIfStopped();

        long start = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (!_paused.Remove(timerId, out ActiveTimer timer))
            {
                ProfilerThrowHelper.ThrowArgument(nameof(timerId), "Timer is not paused.");
            }

            _stack.Push(new ActiveTimer(timer.Id, timer.Name, _clock.Now().Value));
            _flames.BeginFrame(timer.Name);
            AddOverhead(start);
        }
    }

    /// <summary>Cancels a timer without recording.</summary>
    public void CancelTimer(int? timerId = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ThrowIfStopped();

        long start = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            ActiveTimer timer = PopTop(timerId, nameof(CancelTimer));
            _paused.Remove(timer.Id);
            _flames.AbortFrame();
            AddOverhead(start);
        }
    }

    /// <summary>Records a marker event.</summary>
    public void AddMarker(string name, int? parentId = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ThrowIfStopped();

        long start = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            _timeline.RecordEvent(TimelineCategory.Cpu, parentId.HasValue
                ? string.Create(CultureInfo.InvariantCulture, $"marker:{name} parent:{parentId.Value}")
                : string.Create(CultureInfo.InvariantCulture, $"marker:{name}"));
            Interlocked.Increment(ref _markers);
            AddOverhead(start);
        }
    }

    /// <summary>Records a marker whose name comes from a callback; returns the marker index.</summary>
    public int AddMarkerWithCallback(Func<string> nameFactory, int? parentId = null)
    {
        ArgumentNullException.ThrowIfNull(nameFactory);
        string name;
        try
        {
            name = nameFactory();
        }
        catch (Exception ex)
        {
            throw new ProfilerException("PROFILER_ERROR", "Marker name callback failed.", ex);
        }

        AddMarker(name, parentId);
        return (int)Interlocked.Read(ref _markers) - 1;
    }

    /// <summary>Arms periodic heartbeat sampling.</summary>
    public void EnableSampling(double intervalMs = 10.0)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        lock (_gate)
        {
            _sampler?.Dispose();
            _sampler = new ContinuousSampler(
                onTick: _ => _timeline.RecordEvent(TimelineCategory.Cpu, "sample"),
                intervalMs: intervalMs);
            if (Volatile.Read(ref _started) != 0)
            {
                _sampler.Start();
            }
        }
    }

    /// <summary>Disarms heartbeat sampling.</summary>
    public void DisableSampling()
    {
        lock (_gate)
        {
            _sampler?.Dispose();
            _sampler = null;
        }
    }

    /// <summary>Advances the frame boundary.</summary>
    public void EndFrame()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ThrowIfStopped();
        _timeline.Tick();
        _options.OnFrameEnd?.Invoke();
    }

    /// <summary>Drops timers, hierarchy, metrics and timeline.</summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        lock (_gate)
        {
            _stack.Clear();
            _paused.Clear();
            _flames.Clear();
            _histogram.Clear();
            _timeline.Clear();
            Interlocked.Exchange(ref _completedTimers, 0L);
            Interlocked.Exchange(ref _markers, 0L);
            Interlocked.Exchange(ref _overheadTicks, 0L);
        }
    }

    /// <summary>Timer hierarchy roots.</summary>
    public IReadOnlyList<FlameGraphNode> GetHierarchy() => _flames.GetRoots();

    /// <summary>Sampled duration statistics.</summary>
    public HistogramStatistics GetSampledMetrics() => _histogram.GetStatistics();

    /// <summary>Profiler self-cost and volume counters.</summary>
    public OverheadMetrics GetOverheadMetrics() => new(
        Interlocked.Read(ref _overheadTicks) * 1_000_000_000L / Stopwatch.Frequency,
        (int)Interlocked.Read(ref _completedTimers),
        (int)Interlocked.Read(ref _markers));

    /// <summary>Timeline recorder.</summary>
    public TimelineRecorder GetTimelineRecorder() => _timeline;

    /// <summary>Memory tracker, created on first use.</summary>
    public MemoryTracker GetMemoryTracker()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        lock (_gate)
        {
            _memory ??= new MemoryTracker();
            return _memory;
        }
    }

    /// <summary>Assembles the session result.</summary>
    public ProfilerResult GetResult()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        long ended = Volatile.Read(ref _endedAtMs);
        if (ended == 0L)
        {
            ended = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        return new ProfilerResult(
            _sessionId,
            Volatile.Read(ref _startedAtMs),
            ended,
            Interlocked.Read(ref _completedTimers) + (_sampler?.TickCount ?? 0L),
            _histogram.GetStatistics(),
            _flames.GetRoots(),
            _timeline.GetEvents());
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

            _sampler?.Dispose();
            _sampler = null;
            _timeline.Dispose();
            _memory?.Dispose();
            _memory = null;
            _flames.Dispose();
            _histogram.Dispose();
            try
            {
                if (_clock.State != ClockState.Stopped)
                {
                    _clock.Stop();
                }
            }
            catch (ClockNotRunningException)
            {
                // Already stopped; teardown continues.
            }
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
