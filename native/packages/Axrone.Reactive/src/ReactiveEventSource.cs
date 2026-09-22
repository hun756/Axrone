namespace Axrone.Reactive;

using System.Diagnostics.Tracing;

/// <summary>
/// Structured diagnostics for reactive terminal transitions.
/// </summary>
/// <remarks>
/// Cold paths only — one event per terminal call, never per subscriber or notification.
/// Follows the house pattern in <c>Axrone.Batching</c>.
/// </remarks>
[EventSource(Name = "Axrone-Reactive")]
public sealed class ReactiveEventSource : EventSource
{
    /// <summary>Shared instance.</summary>
    public static readonly ReactiveEventSource Log = new();

    private ReactiveEventSource()
        : base(EventSourceSettings.EtwSelfDescribingEventFormat)
    {
    }

    /// <summary>Terminal fault delivered.</summary>
    [Event(1, Level = EventLevel.Error)]
    public void FaultDelivered(string exceptionType, string message)
    {
        if (IsEnabled())
        {
            WriteEvent(1, exceptionType ?? string.Empty, message ?? string.Empty);
        }
    }

    /// <summary>Source completed.</summary>
    [Event(2, Level = EventLevel.Informational)]
    public void SourceCompleted()
    {
        if (IsEnabled())
        {
            WriteEvent(2);
        }
    }
}
