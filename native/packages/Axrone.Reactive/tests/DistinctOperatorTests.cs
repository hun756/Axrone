namespace Axrone.Reactive.Tests;

public class DistinctOperatorTests
{
    private sealed class Recorder : IObserver<int>
    {
        private readonly object _gate = new();
        private readonly List<int> _values = new();

        public void OnNext(int value)
        {
            lock (_gate)
            {
                _values.Add(value);
            }
        }

        public void OnError(Exception error) => throw new NotSupportedException();

        public void OnCompleted()
        {
        }

        public int[] Values()
        {
            lock (_gate)
            {
                return _values.ToArray();
            }
        }
    }

    [Fact]
    public void DistinctUntilChanged_SuppressesConsecutiveDuplicates()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder();
        using var sub = subject.DistinctUntilChanged().Subscribe(recorder);

        subject.OnNext(1);
        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(2);
        subject.OnNext(1);

        recorder.Values().Should().Equal(1, 2, 1);
    }

    [Fact]
    public void DistinctUntilChanged_PerSubscriptionState()
    {
        using var subject = new Subject<int>();
        var first = new Recorder();
        using var subA = subject.DistinctUntilChanged().Subscribe(first);

        subject.OnNext(7);

        var second = new Recorder();
        using var subB = subject.DistinctUntilChanged().Subscribe(second);
        subject.OnNext(7);

        first.Values().Should().Equal(7);
        second.Values().Should().Equal(7);
    }
}
