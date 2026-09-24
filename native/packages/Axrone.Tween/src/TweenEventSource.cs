namespace Axrone.Tween;

using System.Diagnostics.Tracing;

/// <summary>
/// Structured diagnostics for the tween engine.
/// </summary>
/// <remarks>
/// Cold paths only — terminal faults. Lifecycle counters live in <see cref="TweenTelemetry"/>.
/// Follows the house pattern in <c>Axrone.Batching</c>.
/// </remarks>
[EventSource(Name = "Axrone-Tween")]
public sealed class TweenEventSource : EventSource
{
    /// <summary>Shared instance.</summary>
    public static readonly TweenEventSource Log = new();

    private TweenEventSource()
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
}
