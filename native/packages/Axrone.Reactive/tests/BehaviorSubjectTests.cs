namespace Axrone.Reactive.Tests;

public class BehaviorSubjectTests
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
    public void LateSubscriber_ReceivesCurrent()
    {
        using var subject = new BehaviorSubject<int>(10);
        subject.OnNext(20);

        var late = new Recorder();
        using var sub = subject.Subscribe(late);
        subject.OnNext(30);

        late.Values().Should().Equal(20, 30);
        subject.Value.Should().Be(30);
    }
}
