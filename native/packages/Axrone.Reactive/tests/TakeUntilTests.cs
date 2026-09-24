namespace Axrone.Reactive.Tests;

public class TakeUntilTests
{
    private sealed class Recorder : IObserver<int>
    {
        private readonly object _gate = new();
        private readonly List<int> _values = new();
        private Exception? _error;
        private bool _completed;

        public void OnNext(int value)
        {
            lock (_gate)
            {
                _values.Add(value);
            }
        }

        public void OnError(Exception error)
        {
            lock (_gate)
            {
                _error = error;
            }
        }

        public void OnCompleted()
        {
            lock (_gate)
            {
                _completed = true;
            }
        }

        public int[] Values()
        {
            lock (_gate)
            {
                return _values.ToArray();
            }
        }

        public Exception? Error()
        {
            lock (_gate)
            {
                return _error;
            }
        }

        public bool Completed()
        {
            lock (_gate)
            {
                return _completed;
            }
        }
    }

    [Fact]
    public void OtherFires_CompletesAndStops()
    {
        using var source = new Subject<int>();
        using var trigger = new Subject<string>();
        var recorder = new Recorder();
        using var sub = source.TakeUntil(trigger).Subscribe(recorder);

        source.OnNext(1);
        trigger.OnNext("stop");
        source.OnNext(2);

        recorder.Values().Should().Equal(1);
        recorder.Completed().Should().BeTrue();
        source.SubscriberCount.Should().Be(0);
        trigger.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void OtherCompletesSilently_SourceContinues()
    {
        using var source = new Subject<int>();
        using var trigger = new Subject<string>();
        var recorder = new Recorder();
        using var sub = source.TakeUntil(trigger).Subscribe(recorder);

        trigger.OnCompleted();
        source.OnNext(1);

        recorder.Values().Should().Equal(1);
        recorder.Completed().Should().BeFalse();
        trigger.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void OtherErrors_Terminates()
    {
        using var source = new Subject<int>();
        using var trigger = new Subject<string>();
        var recorder = new Recorder();
        using var sub = source.TakeUntil(trigger).Subscribe(recorder);

        var fault = new InvalidOperationException("trigger fault");
        trigger.OnError(fault);

        recorder.Error().Should().BeSameAs(fault);
        source.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_UnsubscribesBoth()
    {
        using var source = new Subject<int>();
        using var trigger = new Subject<string>();
        var recorder = new Recorder();
        var subscription = source.TakeUntil(trigger).Subscribe(recorder);

        source.SubscriberCount.Should().Be(1);
        trigger.SubscriberCount.Should().Be(1);
        subscription.Dispose();

        source.SubscriberCount.Should().Be(0);
        trigger.SubscriberCount.Should().Be(0);
    }
}
