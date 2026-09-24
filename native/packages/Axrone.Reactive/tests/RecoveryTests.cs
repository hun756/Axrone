namespace Axrone.Reactive.Tests;

public class RecoveryTests
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

    private sealed class ScriptedSource : IObservable<int>
    {
        private readonly int _failuresLeft;
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        public ScriptedSource(int failuresLeft) => _failuresLeft = failuresLeft;

        public IDisposable Subscribe(IObserver<int> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            Interlocked.Increment(ref _attempts);
            if (Volatile.Read(ref _attempts) <= _failuresLeft)
            {
                observer.OnError(new InvalidOperationException($"attempt {Volatile.Read(ref _attempts)}"));
            }
            else
            {
                observer.OnNext(42);
                observer.OnCompleted();
            }

            return new NoopSubscription();
        }

        private sealed class NoopSubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public void Catch_SwitchesToFallback()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder();
        using var sub = subject.Catch(_ => Observable.Return(99)).Subscribe(recorder);

        subject.OnNext(1);
        subject.OnError(new InvalidOperationException("boom"));
        subject.OnNext(2);

        recorder.Values().Should().Equal(1, 99);
        recorder.Completed().Should().BeTrue();
    }

    [Fact]
    public void Catch_FallbackError_Terminates()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder();
        var fallbackFault = new InvalidOperationException("fallback fault");
        using var sub = subject.Catch(_ => Observable.Throw<int>(fallbackFault)).Subscribe(recorder);

        subject.OnError(new InvalidOperationException("primary"));

        recorder.Values().Should().BeEmpty();
        recorder.Error().Should().BeSameAs(fallbackFault);
    }

    [Fact]
    public void Catch_ThrowingHandler_Terminates()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder();
        using var sub = subject.Catch<int>(_ => throw new InvalidOperationException("handler fault")).Subscribe(recorder);

        subject.OnError(new InvalidOperationException("primary"));

        recorder.Error().Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("handler fault");
    }

    [Fact]
    public void Retry_SucceedsWithinBudget()
    {
        var source = new ScriptedSource(failuresLeft: 2);
        var recorder = new Recorder();
        using var sub = source.Retry(2).Subscribe(recorder);

        recorder.Values().Should().Equal(42);
        recorder.Completed().Should().BeTrue();
        source.Attempts.Should().Be(3);
    }

    [Fact]
    public void Retry_Exhausted_ForwardsLastError()
    {
        var source = new ScriptedSource(failuresLeft: 5);
        var recorder = new Recorder();
        using var sub = source.Retry(2).Subscribe(recorder);

        recorder.Values().Should().BeEmpty();
        recorder.Error().Should().BeOfType<InvalidOperationException>();
        source.Attempts.Should().Be(3);
    }

    [Fact]
    public void Retry_NegativeCount_Throws()
    {
        using var subject = new Subject<int>();

        var act = () => subject.Retry(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
