using System.Diagnostics.Tracing;

namespace Axrone.Utility.Backoff;

[EventSource(Name = "Axrone-Utility-Backoff")]
public sealed class BackoffDiagnosticsEventSource : EventSource
{
    public static readonly BackoffDiagnosticsEventSource Log = new();

    private BackoffDiagnosticsEventSource() : base(EventSourceSettings.EtwSelfDescribingEventFormat)
    {
    }

    [Event(1, Level = EventLevel.Verbose)]
    public void StepFired(uint step, int kind, long durationNs)
    {
        if (IsEnabled())
        {
            WriteEvent(1, step, kind, durationNs);
        }
    }

    [Event(2, Level = EventLevel.Error)]
    public void FatalFaultReported(string faultMessage)
    {
        if (IsEnabled())
        {
            WriteEvent(2, faultMessage);
        }
    }

    [Event(3, Level = EventLevel.Informational)]
    public void StateTransition(int oldState, int newState)
    {
        if (IsEnabled())
        {
            WriteEvent(3, oldState, newState);
        }
    }
}
