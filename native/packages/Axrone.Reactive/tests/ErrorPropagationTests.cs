namespace Axrone.Reactive.Tests;

public class ErrorPropagationTests
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
    public void Where_ForwardsErrorAndCompletion()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder<int>();
        using var sub = subject.Where(_ => true).Subscribe(recorder);

        var fault = new InvalidOperationException("where fault");
        subject.OnError(fault);

        recorder.Error().Should().BeSameAs(fault);
    }

    [Fact]
    public void Select_ForwardsCompletion()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder<string>();
        using var sub = subject.Select(x => x.ToString()).Subscribe(recorder);

        subject.OnNext(1);
        subject.OnCompleted();

        recorder.Values().Should().Equal("1");
        recorder.Completed().Should().BeTrue();
    }

    [Fact]
    public void Merge_ErrorAfterPartialComplete_Terminates()
    {
        using var first = new Subject<int>();
        using var second = new Subject<int>();
        var recorder = new Recorder<int>();
        using var sub = first.Merge(second).Subscribe(recorder);

        first.OnNext(1);
        first.OnCompleted();

        var fault = new InvalidOperationException("late fault");
        second.OnError(fault);

        recorder.Values().Should().Equal(1);
        recorder.Error().Should().BeSameAs(fault);
        recorder.Completed().Should().BeFalse();
    }

    [Fact]
    public void Merge_ValuesStopAfterError()
    {
        using var first = new Subject<int>();
        using var second = new Subject<int>();
        var recorder = new Recorder<int>();
        using var sub = first.Merge(second).Subscribe(recorder);

        second.OnError(new InvalidOperationException("stop"));
        first.OnNext(99);
        second.OnNext(100);

        recorder.Values().Should().BeEmpty();
    }
}
