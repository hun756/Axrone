namespace Axrone.Batching;

/// <summary>
/// OpenTelemetry meter for batch pipelines.
/// </summary>
/// <remarks>
/// Follows the house pattern in <c>Axrone.Memory.Arena</c>: counters and a histogram, reported
/// with zero steady-state allocation. No tags — tag arrays allocate per report.
/// </remarks>
internal sealed class BatchingTelemetry : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _slices;
    private readonly Counter<long> _processedItems;
    private readonly Counter<long> _droppedItems;
    private readonly Histogram<double> _sliceLatencyMs;
    private int _disposed;

    /// <summary>Creates telemetry.</summary>
    /// <param name="meterName">Meter name, e.g. "Axrone.Batching".</param>
    public BatchingTelemetry(string meterName)
    {
        _meter = new Meter(meterName, "1.0.0");
        _slices = _meter.CreateCounter<long>("batching.slices.count", "{slices}");
        _processedItems = _meter.CreateCounter<long>("batching.processed.items", "{items}");
        _droppedItems = _meter.CreateCounter<long>("batching.dropped.items", "{items}");
        _sliceLatencyMs = _meter.CreateHistogram<double>("batching.slice.duration", "ms");
    }

    /// <summary>Records one execution pass.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordSlice(int processed, double elapsedMilliseconds)
    {
        _slices.Add(1);
        _processedItems.Add(processed);
        _sliceLatencyMs.Record(elapsedMilliseconds);
    }

    /// <summary>Records abandoned stale-remainder items.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDropped(long count) => _droppedItems.Add(count);

    /// <summary>Releases the meter. Idempotent and race-free.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _meter.Dispose();
        }
    }
}
