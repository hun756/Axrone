namespace Axrone.Batching;

using System.Diagnostics.Tracing;

/// <summary>
/// Structured diagnostics for batch pipelines.
/// </summary>
/// <remarks>
/// Cold paths only — fault, state transition, stale drop. Per-slice events would reintroduce the
/// per-item telemetry tax; slices are counted in <see cref="BatchingTelemetry"/>, not traced.
/// Follows the house precedent <c>Axrone.Utility.Backoff.BackoffDiagnosticsEventSource</c>.
/// </remarks>
[EventSource(Name = "Axrone-Batching")]
public sealed class BatchingEventSource : EventSource
{
    /// <summary>Shared instance.</summary>
    public static readonly BatchingEventSource Log = new();

    private BatchingEventSource()
        : base(EventSourceSettings.EtwSelfDescribingEventFormat)
    {
    }

    /// <summary>Terminal fault captured.</summary>
    [Event(1, Level = EventLevel.Error)]
    public void FaultOccurred(string exceptionType, string message)
    {
        if (IsEnabled())
        {
            WriteEvent(1, exceptionType ?? string.Empty, message ?? string.Empty);
        }
    }

    /// <summary>Lifecycle state changed.</summary>
    [Event(2, Level = EventLevel.Informational)]
    public void StateTransition(int oldState, int newState)
    {
        if (IsEnabled())
        {
            WriteEvent(2, oldState, newState);
        }
    }

    /// <summary>Stale snapshot remainder abandoned.</summary>
    [Event(3, Level = EventLevel.Warning)]
    public void StaleSnapshotDropped(long count)
    {
        if (IsEnabled())
        {
            WriteEvent(3, count);
        }
    }
}
