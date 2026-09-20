namespace Axrone.Event.Tests;

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;

public class TelemetryTests
{
    private sealed class CaptureListener : EventListener
    {
        public readonly List<(string Name, string? Type, string? Message)> Events = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Axrone-Event")
            {
                EnableEvents(eventSource, EventLevel.Verbose);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            string? type = null;
            string? message = null;
            if (eventData.Payload is { Count: >= 1 })
            {
                type = eventData.Payload[0]?.ToString();
            }
            if (eventData.Payload is { Count: >= 2 })
            {
                message = eventData.Payload[1]?.ToString();
            }

            lock (Events)
            {
                Events.Add((eventData.EventName ?? string.Empty, type, message));
            }
        }
    }

    [Fact]
    public void Meter_CountsPublishes()
    {
        var options = new RouterOptionsBuilder().WithCapacity(64).WithTelemetry("Axrone.Event.TelemetryTest").Build();
        using var router = new EventRouter<int>(options);

        long published = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "Axrone.Event.TelemetryTest")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "events.published")
            {
                Interlocked.Add(ref published, measurement);
            }
        });
        listener.Start();

        var count = 0;
        using var sub = router.Subscribe((_, _) => Interlocked.Increment(ref count));
        for (int i = 0; i < 5; i++)
        {
            router.Publish(i);
        }

        SpinWait.SpinUntil(() => Interlocked.Read(ref published) == 5, TimeSpan.FromSeconds(15)).Should().BeTrue();
        SpinWait.SpinUntil(() => Volatile.Read(ref count) == 5, TimeSpan.FromSeconds(15)).Should().BeTrue();
    }

    [Fact]
    public void EventSource_ReportsFault()
    {
        using var capture = new CaptureListener();
        using var router = new EventRouter<int>(16);

        router.Complete(new InvalidOperationException("telemetry fault"));

        SpinWait.SpinUntil(() =>
        {
            lock (capture.Events)
            {
                return capture.Events.Count > 0;
            }
        }, TimeSpan.FromSeconds(15)).Should().BeTrue();

        lock (capture.Events)
        {
            capture.Events.Should().Contain(e => e.Type == "InvalidOperationException" && e.Message == "telemetry fault");
        }
    }
}
