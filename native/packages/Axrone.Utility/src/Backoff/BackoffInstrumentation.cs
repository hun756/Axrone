using System.Diagnostics.Metrics;

namespace Axrone.Utility.Backoff;

public sealed class BackoffInstrumentation : IDisposable
{
    private static readonly Meter s_meter = new("Axrone.Utility.Backoff", "1.0.0");

    private readonly Counter<long> _stepCounter;
    private readonly Histogram<double> _durationHistogram;
    private readonly UpDownCounter<long> _activeContentionCounter;
    private readonly IBackoffTelemetrySink? _customSink;

    public BackoffInstrumentation(string componentName)
        : this(componentName, customSink: null)
    {
    }

    public BackoffInstrumentation(string componentName, IBackoffTelemetrySink? customSink)
    {
        _stepCounter = s_meter.CreateCounter<long>(
            "backoff.operations.total",
            unit: "{operations}",
            description: "Number of progressive backoff operations initiated.");

        _durationHistogram = s_meter.CreateHistogram<double>(
            "backoff.latency.ms",
            unit: "ms",
            description: "Observed duration pause of the backoff execution.");

        _activeContentionCounter = s_meter.CreateUpDownCounter<long>(
            "backoff.contention.active",
            unit: "{threads}",
            description: "Number of workers currently paused.");

        _customSink = customSink;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordExecution(BackoffKind kind, double durationMs, long durationNs)
    {
        TagList tags = default;
        tags.Add("kind", kind switch
        {
            BackoffKind.Spin => "spin",
            BackoffKind.Yield => "yield",
            BackoffKind.Sleep => "sleep",
            BackoffKind.AsyncDelay => "async_delay",
            _ => "none"
        });

        _stepCounter.Add(1L, in tags);
        if (durationMs > 0.0)
        {
            _durationHistogram.Record(durationMs, in tags);
        }

        _customSink?.RecordStep(kind, durationNs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordExecution(BackoffKind kind, double durationMs)
    {
        RecordExecution(kind, durationMs, (long)(durationMs * BackoffDuration.NanosecondsPerMillisecond));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementContention()
    {
        _activeContentionCounter.Add(1L);
        _customSink?.RecordContentionDelta(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecrementContention()
    {
        _activeContentionCounter.Add(-1L);
        _customSink?.RecordContentionDelta(-1);
    }

    public void Dispose()
    {
    }
}
