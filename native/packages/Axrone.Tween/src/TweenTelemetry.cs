namespace Axrone.Tween;

using System.Diagnostics.Metrics;

/// <summary>
/// OpenTelemetry meter for the tween engine.
/// </summary>
/// <remarks>
/// Per-tween lifecycle counters and per-tick latency only — never per-item events. Follows the
/// house pattern in <c>Axrone.Batching</c>.
/// </remarks>
internal sealed class TweenTelemetry : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _submitted;
    private readonly Counter<long> _completed;
    private readonly Counter<long> _canceled;
    private readonly UpDownCounter<long> _active;
    private readonly Histogram<double> _tickLatencyMs;
    private int _disposed;

    /// <summary>Creates telemetry.</summary>
    /// <param name="meterName">Meter name, e.g. "Axrone.Tween".</param>
    public TweenTelemetry(string meterName)
    {
        _meter = new Meter(meterName, "1.0.0");
        _submitted = _meter.CreateCounter<long>("tween.submitted", "{tweens}");
        _completed = _meter.CreateCounter<long>("tween.completed", "{tweens}");
        _canceled = _meter.CreateCounter<long>("tween.canceled", "{tweens}");
        _active = _meter.CreateUpDownCounter<long>("tween.active", "{tweens}");
        _tickLatencyMs = _meter.CreateHistogram<double>("tween.tick.duration", "ms");
    }

    /// <summary>Records one accepted schedule.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordSubmitted()
    {
        _submitted.Add(1);
        _active.Add(1);
    }

    /// <summary>Records finished tweens.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordCompleted(int count)
    {
        _completed.Add(count);
        _active.Add(-count);
    }

    /// <summary>Records one cancel.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordCanceled()
    {
        _canceled.Add(1);
        _active.Add(-1);
    }

    /// <summary>Records one pump pass.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordTick(double elapsedMilliseconds) => _tickLatencyMs.Record(elapsedMilliseconds);

    /// <summary>Releases the meter. Idempotent and race-free.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _meter.Dispose();
        }
    }
}
