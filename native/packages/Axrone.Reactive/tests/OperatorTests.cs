namespace Axrone.Reactive.Tests;

public class OperatorTests
{
    private sealed class Recorder<T> : IObserver<T>
    {
        private readonly object _gate = new();
        private readonly List<T> _values = new();
        private Exception? _error;
        private bool _completed;

        public void OnNext(T value)
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

        public T[] Values()
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
    public void Where_FiltersValues()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder<int>();
        using var sub = subject.Where(x => x % 2 == 0).Subscribe(recorder);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);
        subject.OnNext(4);

        recorder.Values().Should().Equal(2, 4);
    }

    [Fact]
    public void Select_ProjectsValues()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder<string>();
        using var sub = subject.Select(x => $"n{x}").Subscribe(recorder);

        subject.OnNext(1);
        subject.OnNext(2);

        recorder.Values().Should().Equal("n1", "n2");
    }

    [Fact]
    public void Chain_ComposesWhereSelect()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder<string>();
        using var sub = subject.Where(x => x > 10).Select(x => $"big:{x}").Subscribe(recorder);

        subject.OnNext(5);
        subject.OnNext(50);

        recorder.Values().Should().Equal("big:50");
    }

    [Fact]
    public void Merge_CombinesBothStreams()
    {
        using var first = new Subject<int>();
        using var second = new Subject<int>();
        var recorder = new Recorder<int>();
        using var sub = first.Merge(second).Subscribe(recorder);

        first.OnNext(1);
        second.OnNext(2);
        first.OnNext(3);

        recorder.Values().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Merge_CompletesAfterBoth()
    {
        using var first = new Subject<int>();
        using var second = new Subject<int>();
        var recorder = new Recorder<int>();
        using var sub = first.Merge(second).Subscribe(recorder);

        first.OnCompleted();
        recorder.Completed().Should().BeFalse();
        second.OnCompleted();
        recorder.Completed().Should().BeTrue();
    }

    [Fact]
    public void Merge_ErrorTerminates()
    {
        using var first = new Subject<int>();
        using var second = new Subject<int>();
        var recorder = new Recorder<int>();
        using var sub = first.Merge(second).Subscribe(recorder);

        var fault = new InvalidOperationException("merge fault");
        first.OnError(fault);

        recorder.Error().Should().BeSameAs(fault);
        first.SubscriberCount.Should().Be(0);
        second.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_UnsubscribesUpstream()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder<int>();
        var subscription = subject.Where(_ => true).Subscribe(recorder);

        subject.SubscriberCount.Should().Be(1);
        subscription.Dispose();
        subject.SubscriberCount.Should().Be(0);

        subject.OnNext(1);
        recorder.Values().Should().BeEmpty();
    }
}
