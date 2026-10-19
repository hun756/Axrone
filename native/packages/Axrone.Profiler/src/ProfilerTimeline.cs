namespace Axrone.Profiler;

/// <summary>Timeline event category.</summary>
public enum TimelineCategory : byte
{
    /// <summary>Processor activity.</summary>
    Cpu = 0,

    /// <summary>Heap activity.</summary>
    Memory = 1,

    /// <summary>Collector activity.</summary>
    Gc = 2,

    /// <summary>Object creation.</summary>
    Allocation = 3,
}

/// <summary>One recorded timeline entry.</summary>
public readonly record struct TimelineRecordEvent(long TimestampMs, TimelineCategory Category, string Message);

/// <summary>Bounded timeline recorder with a heartbeat timer.</summary>
public sealed class TimelineRecorder : IDisposable, IAsyncDisposable
{
    private const int DefaultMaxEvents = 1_000_000;
    private const int DefaultHeartbeatMs = 50;

    private readonly List<TimelineRecordEvent> _events = new();
    private readonly int _maxEvents;
    private readonly int _heartbeatMs;
    private readonly Lock _gate = new();
    private Timer? _timer;
    private bool _active;
    private int _disposed;

    /// <summary>Creates a recorder.</summary>
    public TimelineRecorder(int maxEvents = DefaultMaxEvents, int heartbeatMs = DefaultHeartbeatMs)
    {
        if (maxEvents < 1)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(maxEvents), "Capacity must be positive.");
        }

        if (heartbeatMs < 1)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(heartbeatMs), "Heartbeat must be positive.");
        }

        _maxEvents = maxEvents;
        _heartbeatMs = heartbeatMs;
    }

    /// <summary>Recorded event count.</summary>
    public int Count
    {
        get { lock (_gate) { return _events.Count; } }
    }

    /// <summary>Whether recording is armed.</summary>
    public bool IsActive
    {
        get { lock (_gate) { return _active; } }
    }

    /// <summary>Arms recording and starts the heartbeat.</summary>
    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (_active)
            {
                return;
            }

            _active = true;
            _timer = new Timer(OnHeartbeat, null, TimeSpan.FromMilliseconds(_heartbeatMs), TimeSpan.FromMilliseconds(_heartbeatMs));
        }
    }

    /// <summary>Stops the heartbeat; recorded events are kept.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _active = false;
            _timer?.Dispose();
            _timer = null;
        }
    }

    /// <summary>Records a manual heartbeat tick.</summary>
    public void Tick() => RecordEvent(TimelineCategory.Cpu, "tick");

    /// <summary>Records one event; drops the oldest past capacity.</summary>
    public void RecordEvent(TimelineCategory category, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_active)
            {
                throw new InvalidOperationException("Timeline recorder is not active.");
            }

            if (_events.Count >= _maxEvents)
            {
                _events.RemoveRange(0, Math.Max(1, _maxEvents / 2));
            }

            _events.Add(new TimelineRecordEvent(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), category, message));
        }
    }

    /// <summary>Returns a copy of all recorded events.</summary>
    public IReadOnlyList<TimelineRecordEvent> GetEvents()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }

    /// <summary>Drops all recorded events.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }

    private void OnHeartbeat(object? _)
    {
        try
        {
            RecordEvent(TimelineCategory.Cpu, "tick");
        }
        catch (InvalidOperationException)
        {
            // Stopped or disposed concurrently; the next heartbeat finds no timer.
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

            _active = false;
            _timer?.Dispose();
            _timer = null;
            _events.Clear();
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
