namespace Axrone.Utility.Backoff;

public interface ISynchronousBackoff
{
    void Step();
    bool TryStep(CancellationToken cancellationToken = default);
    void Reset();
}

public interface IAsynchronousBackoff
{
    ValueTask<BackoffDuration> StepAsync(CancellationToken cancellationToken = default);
    void Reset();
}

public interface IBatchBackoffProcessor
{
    void ComputeBatch(ReadOnlySpan<uint> steps, Span<BackoffDuration> outputs, in BackoffConfiguration config);
}

public interface IBackoffReceiver<TReceiver> where TReceiver : allows ref struct
{
    void ProcessBatch(scoped ReadOnlySpan<BackoffDuration> durations);
}

public interface IBackoffTelemetryEndpoint
{
    BackoffMetricsSnapshot TakeSnapshot();
    bool IsHealthy { get; }
    BackoffHealthStatus QueryHealthStatus();
}

public interface IBackoffLifecycle : IDisposable, IAsyncDisposable
{
    void Complete(Exception? fault = null);
    ValueTask DrainAsync(CancellationToken cancellationToken = default);
}

public interface IBackoffTelemetrySink
{
    void RecordStep(BackoffKind kind, long durationNs);
    void RecordContentionDelta(int delta);
}
