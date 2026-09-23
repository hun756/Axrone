namespace Axrone.Tween.Tests;

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;

public class TelemetryTests
{
    private sealed class CaptureListener : EventListener
    {
        public readonly List<string> Events = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Axrone-Tween")
            {
                EnableEvents(eventSource, EventLevel.Verbose);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            string? type = eventData.Payload is { Count: >= 1 } ? eventData.Payload[0]?.ToString() : null;
            string? message = eventData.Payload is { Count: >= 2 } ? eventData.Payload[1]?.ToString() : null;
            lock (Events)
            {
                Events.Add($"{eventData.EventName}|{type}|{message}");
            }
        }
    }

    private static TweenSpec Quick() =>
        new TweenBuilder().From(0f).To(1f).DurationSeconds(1f).Build();

    [Fact]
    public void Meter_CountsSchedules()
    {
        long submitted = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "Axrone.Tween.TelemetryTest")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "tween.submitted")
            {
                Interlocked.Add(ref submitted, measurement);
            }
        });
        listener.Start();

        using var engine = new TweenEngine(16, null, "Axrone.Tween.TelemetryTest");
        engine.Play(Quick());
        engine.Play(Quick());

        SpinWait.SpinUntil(() => Interlocked.Read(ref submitted) == 2, TimeSpan.FromSeconds(15)).Should().BeTrue();
    }

    [Fact]
    public void EventSource_ReportsFault()
    {
        using var capture = new CaptureListener();
        using var engine = new TweenEngine(16);

        engine.Complete(new InvalidOperationException("telemetry fault"));

        SpinWait.SpinUntil(() =>
        {
            lock (capture.Events)
            {
                return capture.Events.Count > 0;
            }
        }, TimeSpan.FromSeconds(15)).Should().BeTrue();

        lock (capture.Events)
        {
            capture.Events.Should().ContainSingle(e => e.Contains("FaultOccurred") && e.Contains("telemetry fault"));
        }
    }
}
