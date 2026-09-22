namespace Axrone.Reactive.Tests;

public class CombineLatestTests
{
    private sealed class Recorder : IObserver<string>
    {
        private readonly object _gate = new();
        private readonly List<string> _values = new();
        private Exception? _error;
        private bool _completed;

        public void OnNext(string value)
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

        public string[] Values()
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
    public void CombinesAfterBothFired()
    {
        using var first = new Subject<int>();
        using var second = new Subject<string>();
        var recorder = new Recorder();
        using var sub = first.CombineLatest(second, (a, b) => $"{a}:{b}").Subscribe(recorder);

        first.OnNext(1);
        recorder.Values().Should().BeEmpty();

        second.OnNext("x");
        first.OnNext(2);

        recorder.Values().Should().Equal("1:x", "2:x");
    }

    [Fact]
    public void StaticConfig_KeepsCombiningWithLiveFeed()
    {
        using var live = new Subject<int>();
        var recorder = new Recorder();
        using var sub = Observable.Return("cfg").CombineLatest(live, (c, v) => $"{c}={v}").Subscribe(recorder);

        live.OnNext(1);
        live.OnNext(2);

        recorder.Values().Should().Equal("cfg=1", "cfg=2");
        recorder.Completed().Should().BeFalse();
    }

    [Fact]
    public void CompletesAfterBoth()
    {
        using var first = new Subject<int>();
        using var second = new Subject<int>();
        var recorder = new Recorder<int>();
        using var sub = first.CombineLatest(second, (a, b) => a + b).Subscribe(recorder);

        first.OnNext(1);
        first.OnCompleted();
        recorder.Completed.Should().BeFalse();

        second.OnNext(10);
        recorder.Values.Should().Equal(11);
        second.OnCompleted();
        recorder.Completed.Should().BeTrue();
    }

    private sealed class Recorder<T> : IObserver<T>
    {
        public readonly List<T> Values = new();
        public bool Completed;
        public void OnNext(T value) => Values.Add(value);
        public void OnError(Exception error) => throw new NotSupportedException();
        public void OnCompleted() => Completed = true;
    }

    [Fact]
    public void Error_Terminates()
    {
        using var first = new Subject<int>();
        using var second = new Subject<int>();
        var recorder = new Recorder();
        using var sub = first.CombineLatest(second, (a, b) => $"{a}:{b}").Subscribe(recorder);

        var fault = new InvalidOperationException("combine fault");
        second.OnError(fault);

        recorder.Error().Should().BeSameAs(fault);
    }
}
