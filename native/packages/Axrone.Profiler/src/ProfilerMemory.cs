namespace Axrone.Profiler;

/// <summary>Memory sample origin.</summary>
public enum MemorySource : byte
{
    /// <summary>No provider.</summary>
    Unknown = 0,

    /// <summary>CLR GC plus process counters.</summary>
    DotNet = 1,
}

/// <summary>One memory sample.</summary>
public readonly record struct MemorySample(
    long TimestampMs,
    long UsedHeapBytes,
    long WorkingSetBytes,
    MemorySource Source,
    long HeapSizeChangeSinceLastSample = 0L,
    int GcEventsCount = 0,
    long AvailableHeapSizeBytes = 0L);

/// <summary>Memory sample provider contract.</summary>
public interface IMemoryProvider
{
    /// <summary>Provider name.</summary>
    string Name { get; }

    /// <summary>Takes one sample.</summary>
    MemorySample Sample();
}

/// <summary>CLR-backed provider: managed heap plus process working set.</summary>
public sealed class DotNetMemoryProvider : IMemoryProvider
{
    /// <inheritdoc/>
    public string Name => "dotnet";

    /// <inheritdoc/>
    public MemorySample Sample()
    {
        using System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
        return new MemorySample(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            GC.GetTotalMemory(forceFullCollection: false),
            process.WorkingSet64,
            MemorySource.DotNet);
    }
}

/// <summary>Fallback provider reporting zeros.</summary>
public sealed class NullMemoryProvider : IMemoryProvider
{
    /// <inheritdoc/>
    public string Name => "none";

    /// <inheritdoc/>
    public MemorySample Sample() => new(
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0L, 0L, MemorySource.Unknown);
}

/// <summary>Heap tracker with optional interval sampling.</summary>
public sealed class MemoryTracker : IDisposable, IAsyncDisposable
{
    private readonly IMemoryProvider _provider;
    private readonly List<MemorySample> _samples = new();
    private readonly int _maxSamples;
    private readonly Lock _gate = new();
    private Timer? _timer;
    private bool _active;
    private Exception? _lastError;
    private int _disposed;

    /// <summary>Creates a tracker.</summary>
    public MemoryTracker(IMemoryProvider? provider = null, int maxSamples = 10_000)
    {
        if (maxSamples < 1)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(maxSamples), "Capacity must be positive.");
        }

        _provider = provider ?? new DotNetMemoryProvider();
        _maxSamples = maxSamples;
    }

    /// <summary>Provider name.</summary>
    public string GetProviderName() => _provider.Name;

    /// <summary>Total samples taken.</summary>
    public long GetTotalSamples()
    {
        lock (_gate)
        {
            return _samples.Count;
        }
    }

    /// <summary>Last background sampling error, if any.</summary>
    public Exception? LastError
    {
        get { lock (_gate) { return _lastError; } }
    }

    /// <summary>Starts tracking; a positive interval arms background sampling.</summary>
    public void StartTracking(double intervalMs = 0.0)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (intervalMs < 0.0 || double.IsNaN(intervalMs) || double.IsInfinity(intervalMs))
            {
                ProfilerThrowHelper.ThrowArgument(nameof(intervalMs), "Interval must be a finite non-negative number.");
            }

            _active = true;
            _timer?.Dispose();
            _timer = intervalMs > 0.0
                ? new Timer(OnSample, null, TimeSpan.FromMilliseconds(intervalMs), TimeSpan.FromMilliseconds(intervalMs))
                : null;
        }
    }

    /// <summary>Stops background sampling; recorded samples are kept.</summary>
    public void StopTracking()
    {
        lock (_gate)
        {
            _active = false;
            _timer?.Dispose();
            _timer = null;
        }
    }

    /// <summary>Takes one manual sample.</summary>
    public MemorySample Sample()
    {
        MemorySample sample = _provider.Sample();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_active)
            {
                throw new InvalidOperationException("Memory tracker is not tracking.");
            }

            if (_samples.Count >= _maxSamples)
            {
                _samples.RemoveRange(0, Math.Max(1, _maxSamples / 2));
            }

            long change = _samples.Count == 0 ? 0L : sample.UsedHeapBytes - _samples[^1].UsedHeapBytes;
            int gcEvents = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            long available;
            try
            {
                available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            }
            catch (InvalidOperationException)
            {
                available = 0L;
            }

            MemorySample enriched = sample with
            {
                HeapSizeChangeSinceLastSample = change,
                GcEventsCount = gcEvents,
                AvailableHeapSizeBytes = available,
            };
            _samples.Add(enriched);
            return enriched;
        }
    }

    /// <summary>Returns a copy of all samples.</summary>
    public IReadOnlyList<MemorySample> GetSamples()
    {
        lock (_gate)
        {
            return _samples.ToArray();
        }
    }

    /// <summary>Drops all samples.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _samples.Clear();
        }
    }

    private void OnSample(object? _)
    {
        try
        {
            Sample();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            lock (_gate)
            {
                _lastError = ex;
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

            _active = false;
            _timer?.Dispose();
            _timer = null;
            _samples.Clear();
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
