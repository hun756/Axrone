namespace Axrone.Audio.Tests;

public class AudioObservabilityTests
{
    private sealed class Recorder : IAudioEventListener
    {
        public readonly List<AudioRuntimeEvent> Events = new();

        public void OnAudioEvent(in AudioRuntimeEvent ev) => Events.Add(ev);
    }

    [Fact]
    public void Dispatch_Reaches_Subscribers_WithPayload()
    {
        var counters = new CacheAlignedDiagnosticsCounters();
        var runtime = new AudioObservabilityRuntime(counters);
        var recorder = new Recorder();
        runtime.Subscribe(recorder);

        runtime.Dispatch(AudioEventType.SourcePlayed, 7U, 9U, 0.5f);

        recorder.Events.Should().ContainSingle();
        recorder.Events[0].Type.Should().Be(AudioEventType.SourcePlayed);
        recorder.Events[0].PrimaryId.Should().Be(7U);
        recorder.Events[0].SecondaryId.Should().Be(9U);
        recorder.Events[0].Value.Should().Be(0.5f);
        counters.EmittedEventCount.Should().Be(1);
    }

    [Fact]
    public void Unsubscribe_Stops_Delivery()
    {
        var runtime = new AudioObservabilityRuntime(new CacheAlignedDiagnosticsCounters());
        var recorder = new Recorder();
        runtime.Subscribe(recorder);
        runtime.Unsubscribe(recorder);
        runtime.Dispatch(AudioEventType.BusUpserted, 1U);
        recorder.Events.Should().BeEmpty();
    }

    [Fact]
    public void Counters_Track_Mutations()
    {
        var counters = new CacheAlignedDiagnosticsCounters();
        counters.IncrementBusMutations();
        counters.IncrementListenerMutations();
        counters.IncrementSourceMutations();
        counters.IncrementCommands();
        counters.IncrementCompletions();
        counters.IncrementErrors();
        counters.IncrementSnapshots();
        counters.IncrementLifecycle();

        counters.BusMutationCount.Should().Be(1);
        counters.ListenerMutationCount.Should().Be(1);
        counters.SourceMutationCount.Should().Be(1);
        counters.PlaybackCommandCount.Should().Be(1);
        counters.PlaybackCompletionCount.Should().Be(1);
        counters.PlaybackErrorCount.Should().Be(1);
        counters.SnapshotOperationCount.Should().Be(1);
        counters.LifecycleTransitionCount.Should().Be(1);
    }
}
