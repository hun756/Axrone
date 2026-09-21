namespace Axrone.Event;

using System.Diagnostics.Metrics;

/// <summary>
/// OpenTelemetry meter for event routers.
/// </summary>
/// <remarks>
/// Counters with zero steady-state allocation and no tags. Dispatch is reported per batch, not
/// per envelope — per-item events would reintroduce the telemetry tax. Follows the house
/// pattern in <c>Axrone.Batching</c>.
/// </remarks>
internal sealed class EventTelemetry : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _published;
    private readonly Counter<long> _dispatched;
    private readonly Counter<long> _droppedUnsubscribed;
    private readonly Counter<long> _droppedPaused;
    private readonly Counter<long> _deadLettered;
    private readonly Counter<long> _deadLetterDropped;
    private readonly Histogram<double> _dispatchDurationMs;
    private int _disposed;

    /// <summary>Creates telemetry.</summary>
    /// <param name="meterName">Meter name, e.g. "Axrone.Event".</param>
    public EventTelemetry(string meterName)
    {
        _meter = new Meter(meterName, "1.0.0");
        _published = _meter.CreateCounter<long>("events.published", "{events}");
        _dispatched = _meter.CreateCounter<long>("events.dispatched", "{events}");
        _droppedUnsubscribed = _meter.CreateCounter<long>("events.dropped.unsubscribed", "{events}");
        _droppedPaused = _meter.CreateCounter<long>("events.dropped.paused", "{events}");
        _deadLettered = _meter.CreateCounter<long>("events.deadlettered", "{events}");
        _deadLetterDropped = _meter.CreateCounter<long>("events.deadletter.dropped", "{events}");
        _dispatchDurationMs = _meter.CreateHistogram<double>("events.dispatch.duration", "ms");
    }

    /// <summary>Records accepted publishes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordPublished(int count) => _published.Add(count);

    /// <summary>Records one dispatch batch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDispatch(int dispatched, double elapsedMilliseconds)
    {
        _dispatched.Add(dispatched);
        _dispatchDurationMs.Record(elapsedMilliseconds);
    }

    /// <summary>Records envelopes dequeued while no subscription existed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDroppedUnsubscribed(int count) => _droppedUnsubscribed.Add(count);

    /// <summary>Records envelopes skipped while every subscription was paused.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDroppedPaused(int count) => _droppedPaused.Add(count);

    /// <summary>Records one retained dead letter.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDeadLetter() => _deadLettered.Add(1);

    /// <summary>Records one dead letter discarded by the bound.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDeadLetterDropped() => _deadLetterDropped.Add(1);

    /// <summary>Releases the meter. Idempotent and race-free.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _meter.Dispose();
        }
    }
}
