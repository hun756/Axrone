namespace Axrone.Reactive.Tests;

using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;

public class TelemetryTests
{
    private sealed class Recorder : IObserver<int>
    {
        public void OnNext(int value)
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }

    private sealed class CaptureListener : EventListener
    {
        public readonly List<string> Events = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Axrone-Reactive")
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

    [Fact]
    public void Meter_CountsSubscriptions()
    {
        long subscribed = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "Axrone.Reactive")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "reactive.subscribed")
            {
                Interlocked.Add(ref subscribed, measurement);
            }
        });
        listener.Start();

        using var subject = new Subject<int>();
        using var first = subject.Subscribe(new Recorder());
        using var second = subject.Subscribe(new Recorder());

        SpinWait.SpinUntil(() => Interlocked.Read(ref subscribed) == 2, TimeSpan.FromSeconds(15)).Should().BeTrue();
    }

    [Fact]
    public void EventSource_ReportsFault()
    {
        using var capture = new CaptureListener();
        using var subject = new Subject<int>();
        var recorder = new Recorder();
        using var sub = subject.Subscribe(recorder);

        subject.OnError(new InvalidOperationException("telemetry fault"));

        SpinWait.SpinUntil(() =>
        {
            lock (capture.Events)
            {
                return capture.Events.Count > 0;
            }
        }, TimeSpan.FromSeconds(15)).Should().BeTrue();

        lock (capture.Events)
        {
            capture.Events.Should().ContainSingle(e => e.Contains("FaultDelivered") && e.Contains("telemetry fault"));
        }
    }
}
