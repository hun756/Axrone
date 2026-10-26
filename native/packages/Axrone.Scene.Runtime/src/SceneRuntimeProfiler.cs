namespace Axrone.Scene.Runtime;

/// <summary>
/// Rolling frame profiler for the scene runtime. Records per-phase timings,
/// render and physics counters, memory samples and optional stack captures in
/// preallocated ring buffers. Begin, measure and end never allocate, so the
/// profiler can stay enabled during steady-state frames.
/// </summary>
public sealed class SceneRuntimeProfiler : IDisposable
{
    private const double MaxFrameWallDeltaMs = 5000.0;

    private readonly Lock _gate = new();
    private readonly SceneRuntimeFrameRecord[] _records;
    private readonly float[] _summaryScratch;
    private readonly float[] _phaseAccumulator = new float[5];
    private readonly SceneRuntimeStackSample?[] _stackSamples;

    private SceneRuntimeProfilerListener[] _listeners = [];
    private int _start;
    private int _head;
    private int _count;
    private int _stackHead;
    private int _stackCount;
    private int _stackCapacity;

    private readonly double _memorySampleIntervalMs;
    private bool _enabled;
    private bool _frameOpen;
    private bool _disposed;
    private int _frame;
    private double _frameStartMs;
    private double _deltaMs;
    private double? _wallDeltaMs;
    private double? _lastFrameBeginMs;
    private double _lastMemorySampleMs = double.NegativeInfinity;
    private long _lastMemorySampleBytes;
    private bool _hasMemorySample;
    private bool _hasRenderStats;
    private SceneRenderStats _pendingRenderStats;
    private bool _hasPhysicsStats;
    private ScenePhysicsStats _pendingPhysicsStats;

    /// <summary>Creates a profiler.</summary>
    public SceneRuntimeProfiler(SceneRuntimeProfilerOptions? options = null)
    {
        options ??= new SceneRuntimeProfilerOptions();

        int capacity = options.Capacity > 0 ? options.Capacity : 300;
        _memorySampleIntervalMs = options.MemorySampleIntervalMs > 0.0
            ? options.MemorySampleIntervalMs
            : 250.0;
        _stackCapacity = options.StackSampleCapacity > 0 ? options.StackSampleCapacity : 0;
        _enabled = options.Enabled;

        _records = new SceneRuntimeFrameRecord[capacity];
        _summaryScratch = new float[capacity];
        _stackSamples = _stackCapacity > 0 ? new SceneRuntimeStackSample?[_stackCapacity] : [];
    }

    /// <summary>Whether recording is active.</summary>
    public bool IsEnabled => _enabled;

    /// <summary>Whether the profiler has been disposed.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>Number of retained frame records.</summary>
    public int Count => _count;

    /// <summary>Enables recording.</summary>
    public void Enable()
    {
        ThrowIfDisposed();
        _enabled = true;
    }

    /// <summary>Disables recording and closes any open frame.</summary>
    public void Disable()
    {
        ThrowIfDisposed();
        _enabled = false;
        _frameOpen = false;
    }

    /// <summary>Subscribes a frame listener.</summary>
    public void Subscribe(SceneRuntimeProfilerListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ThrowIfDisposed();

        lock (_gate)
        {
            SceneRuntimeProfilerListener[] current = _listeners;
            var next = new SceneRuntimeProfilerListener[current.Length + 1];
            current.AsSpan().CopyTo(next);
            next[^1] = listener;
            _listeners = next;
        }
    }

    /// <summary>Removes a frame listener.</summary>
    public bool Unsubscribe(SceneRuntimeProfilerListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ThrowIfDisposed();

        lock (_gate)
        {
            SceneRuntimeProfilerListener[] current = _listeners;
            int index = Array.IndexOf(current, listener);
            if (index < 0)
            {
                return false;
            }

            var next = new SceneRuntimeProfilerListener[current.Length - 1];
            current.AsSpan(0, index).CopyTo(next);
            current.AsSpan(index + 1).CopyTo(next.AsSpan(index));
            _listeners = next;
            return true;
        }
    }

    /// <summary>Starts a frame and resets the phase accumulator.</summary>
    public void BeginFrame(int frame, double nowMs, double deltaMs)
    {
        if (_disposed || !_enabled)
        {
            return;
        }

        _wallDeltaMs = _lastFrameBeginMs is { } last
            ? Math.Min(Math.Max(0.0, nowMs - last), MaxFrameWallDeltaMs)
            : null;
        _lastFrameBeginMs = nowMs;
        _frameOpen = true;
        _frame = frame;
        _frameStartMs = nowMs;
        _deltaMs = deltaMs;
        _hasRenderStats = false;
        _hasPhysicsStats = false;
        Array.Clear(_phaseAccumulator);
    }

    /// <summary>Measures one phase with a using scope.</summary>
    public SceneProfilerScope MeasurePhase(SceneRuntimePhase phase) =>
        _disposed || !_enabled || !_frameOpen
            ? default
            : new SceneProfilerScope(this, phase, Stopwatch.GetTimestamp());

    /// <summary>Adds an externally measured phase duration.</summary>
    public void AccumulatePhase(SceneRuntimePhase phase, float milliseconds)
    {
        if (_disposed || !_enabled || !_frameOpen)
        {
            return;
        }

        _phaseAccumulator[(int)phase] += Math.Max(0.0f, milliseconds);
    }

    /// <summary>Attaches the render counters for the current frame.</summary>
    public void AttachRenderStats(in SceneRenderStats stats)
    {
        if (_enabled && _frameOpen)
        {
            _pendingRenderStats = stats;
            _hasRenderStats = true;
        }
    }

    /// <summary>Attaches the physics timings for the current frame.</summary>
    public void AttachPhysicsStats(in ScenePhysicsStats stats)
    {
        if (_enabled && _frameOpen)
        {
            _pendingPhysicsStats = stats;
            _hasPhysicsStats = true;
        }
    }

    /// <summary>Captures a stack sample for a phase.</summary>
    public void CapturePhaseSample(SceneRuntimePhase phase)
    {
        if (_disposed || !_enabled || !_frameOpen || _stackCapacity == 0)
        {
            return;
        }

        var frames = new List<SceneRuntimeStackFrame>(16);
        foreach (string line in Environment.StackTrace.Split('\n'))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("at ", StringComparison.Ordinal))
            {
                continue;
            }

            string body = trimmed[3..];
            int fileIndex = body.IndexOf(" in ", StringComparison.Ordinal);
            if (fileIndex < 0)
            {
                frames.Add(new SceneRuntimeStackFrame(body, null));
                continue;
            }

            string function = body[..fileIndex];
            string path = body[(fileIndex + 4)..];
            int lineIndex = path.LastIndexOf(":line ", StringComparison.Ordinal);
            frames.Add(new SceneRuntimeStackFrame(
                function,
                lineIndex >= 0 ? path[..lineIndex] : path));
        }

        if (frames.Count == 0)
        {
            return;
        }

        _stackSamples[_stackHead] = new SceneRuntimeStackSample(phase, frames);
        _stackHead = (_stackHead + 1) % _stackCapacity;
        if (_stackCount < _stackCapacity)
        {
            _stackCount++;
        }
    }

    /// <summary>Completes the frame, stores the record and notifies listeners.</summary>
    public void EndFrame(double nowMs, int fixedSteps)
    {
        if (_disposed || !_enabled || !_frameOpen)
        {
            return;
        }

        _frameOpen = false;

        double frameTimeMs = _wallDeltaMs ?? Math.Max(0.0, nowMs - _frameStartMs);
        _wallDeltaMs = null;

        if (nowMs - _lastMemorySampleMs >= _memorySampleIntervalMs)
        {
            _lastMemorySampleBytes = GC.GetTotalMemory(forceFullCollection: false);
            _lastMemorySampleMs = nowMs;
            _hasMemorySample = true;
        }

        var record = new SceneRuntimeFrameRecord(
            _frame,
            nowMs,
            frameTimeMs,
            frameTimeMs > 0.0 ? 1000.0 / frameTimeMs : 0.0,
            _deltaMs,
            fixedSteps,
            new SceneRuntimePhaseTimes(
                _phaseAccumulator[(int)SceneRuntimePhase.PreUpdate],
                _phaseAccumulator[(int)SceneRuntimePhase.FixedUpdate],
                _phaseAccumulator[(int)SceneRuntimePhase.Update],
                _phaseAccumulator[(int)SceneRuntimePhase.PostUpdate],
                _phaseAccumulator[(int)SceneRuntimePhase.Render]),
            _hasRenderStats,
            _pendingRenderStats,
            _hasPhysicsStats,
            _pendingPhysicsStats,
            _hasMemorySample,
            _lastMemorySampleBytes);

        Array.Clear(_phaseAccumulator);
        _hasRenderStats = false;
        _pendingRenderStats = default;
        _hasPhysicsStats = false;
        _pendingPhysicsStats = default;

        StoreRecord(in record);

        SceneRuntimeProfilerListener[] listeners = _listeners;
        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i](in record);
        }
    }

    /// <summary>Copies retained records from oldest to newest.</summary>
    /// <returns>Number of records copied.</returns>
    public int CopyRecords(Span<SceneRuntimeFrameRecord> destination)
    {
        int count = Math.Min(_count, destination.Length);
        for (int i = 0; i < count; i++)
        {
            destination[i] = _records[(_start + i) % _records.Length];
        }

        return count;
    }

    /// <summary>Returns retained records from oldest to newest.</summary>
    public SceneRuntimeFrameRecord[] GetRecords()
    {
        var result = new SceneRuntimeFrameRecord[_count];
        CopyRecords(result);
        return result;
    }

    /// <summary>Most recent record, or null when none exist.</summary>
    public SceneRuntimeFrameRecord? LastRecord =>
        _count == 0 ? null : _records[(_start + _count - 1) % _records.Length];

    /// <summary>Computes a rolling summary over the newest records.</summary>
    /// <param name="cap">Maximum records to include; zero or negative uses all.</param>
    public SceneRuntimeProfilerSummary? GetSummary(int cap)
    {
        if (_count == 0)
        {
            return null;
        }

        int window = cap > 0 ? Math.Min(cap, _count) : _count;
        int skip = _count - window;
        double frameTimeSum = 0.0;
        double fpsSum = 0.0;
        double minFrameTime = double.PositiveInfinity;
        double maxFrameTime = 0.0;
        Span<float> phaseSums = stackalloc float[5];
        phaseSums.Clear();

        for (int i = 0; i < window; i++)
        {
            SceneRuntimeFrameRecord record = _records[(_start + skip + i) % _records.Length];
            double frameTime = record.FrameTimeMs;

            frameTimeSum += frameTime;
            fpsSum += record.Fps;
            minFrameTime = Math.Min(minFrameTime, frameTime);
            maxFrameTime = Math.Max(maxFrameTime, frameTime);
            _summaryScratch[i] = (float)frameTime;

            for (int phase = 0; phase < 5; phase++)
            {
                phaseSums[phase] += record.PhaseMs.GetPhaseMs((SceneRuntimePhase)phase);
            }
        }

        Array.Sort(_summaryScratch, 0, window);
        int p95Index = Math.Min(window - 1, Math.Max(0, (int)Math.Ceiling(window * 0.95) - 1));

        return new SceneRuntimeProfilerSummary(
            window,
            frameTimeSum / window,
            minFrameTime,
            maxFrameTime,
            _summaryScratch[p95Index],
            fpsSum / window,
            new SceneRuntimePhaseTimes(
                phaseSums[(int)SceneRuntimePhase.PreUpdate] / window,
                phaseSums[(int)SceneRuntimePhase.FixedUpdate] / window,
                phaseSums[(int)SceneRuntimePhase.Update] / window,
                phaseSums[(int)SceneRuntimePhase.PostUpdate] / window,
                phaseSums[(int)SceneRuntimePhase.Render] / window));
    }

    /// <summary>Returns retained stack samples from oldest to newest.</summary>
    public SceneRuntimeStackSample[] GetStackSamples()
    {
        var result = new SceneRuntimeStackSample[_stackCount];
        for (int i = 0; i < _stackCount; i++)
        {
            result[i] = _stackSamples[(_stackHead - _stackCount + i + _stackSamples.Length) % _stackSamples.Length]!;
        }

        return result;
    }

    /// <summary>Clears retained stack samples.</summary>
    public void ClearStackSamples()
    {
        Array.Clear(_stackSamples);
        _stackHead = 0;
        _stackCount = 0;
    }

    /// <summary>Clears records, samples and pending frame state.</summary>
    public void Clear()
    {
        Array.Clear(_records);
        Array.Clear(_phaseAccumulator);
        ClearStackSamples();
        _start = 0;
        _head = 0;
        _count = 0;
        _frameOpen = false;
        _wallDeltaMs = null;
        _lastFrameBeginMs = null;
        _hasRenderStats = false;
        _hasPhysicsStats = false;
        _pendingRenderStats = default;
        _pendingPhysicsStats = default;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _listeners = [];
        _enabled = false;
        _disposed = true;
    }

    internal void FinishPhase(SceneRuntimePhase phase, long startedAt)
    {
        double milliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        _phaseAccumulator[(int)phase] += (float)milliseconds;
    }

    private void StoreRecord(in SceneRuntimeFrameRecord record)
    {
        _records[_head] = record;
        _head = (_head + 1) % _records.Length;

        if (_count == _records.Length)
        {
            _start = (_start + 1) % _records.Length;
        }
        else
        {
            _count++;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new SceneDisposedException(nameof(SceneRuntimeProfiler));
        }
    }
}

/// <summary>Times one profiler phase; disposing the scope records the duration.</summary>
public readonly ref struct SceneProfilerScope
{
    private readonly SceneRuntimeProfiler? _profiler;
    private readonly SceneRuntimePhase _phase;
    private readonly long _startedAt;

    internal SceneProfilerScope(SceneRuntimeProfiler profiler, SceneRuntimePhase phase, long startedAt)
    {
        _profiler = profiler;
        _phase = phase;
        _startedAt = startedAt;
    }

    /// <summary>Records the elapsed phase duration.</summary>
    public void Dispose() => _profiler?.FinishPhase(_phase, _startedAt);
}
