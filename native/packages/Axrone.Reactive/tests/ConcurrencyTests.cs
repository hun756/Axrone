namespace Axrone.Reactive.Tests;

public class ConcurrencyTests
{
    private sealed class Counter : IObserver<int>
    {
        private int _count;

        public void OnNext(int value) => Interlocked.Increment(ref _count);
        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        public int Count => Volatile.Read(ref _count);
    }

    private sealed class SelfDisposer : IObserver<int>
    {
        private readonly Subject<int> _source;
        private IDisposable? _subscription;
        private int _received;

        public SelfDisposer(Subject<int> source) => _source = source;

        public void Attach() => _subscription = _source.Subscribe(this);

        public void OnNext(int value)
        {
            Interlocked.Increment(ref _received);
            _subscription?.Dispose();
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        public int Received => Volatile.Read(ref _received);
    }

    [Fact]
    public async Task PublishSubscribeChurn_StayConsistent()
    {
        using var subject = new Subject<int>();
        var stable = new Counter();
        using var stableSub = subject.Subscribe(stable);

        const int Publishers = 4;
        const int PerPublisher = 1000;
        var cts = new CancellationTokenSource();
        var churn = Task.Run(() =>
        {
            var dummy = new Counter();
            while (!cts.Token.IsCancellationRequested)
            {
                using var sub = subject.Subscribe(dummy);
            }
        });

        var publishers = new Task[Publishers];
        for (int p = 0; p < Publishers; p++)
        {
            publishers[p] = Task.Run(() =>
            {
                for (int i = 0; i < PerPublisher; i++)
                {
                    subject.OnNext(i);
                }
            });
        }

        await Task.WhenAll(publishers).WaitAsync(TimeSpan.FromSeconds(15));
        cts.Cancel();
        await churn.WaitAsync(TimeSpan.FromSeconds(15));

        stable.Count.Should().Be(Publishers * PerPublisher);
        subject.SubscriberCount.Should().Be(1);
    }

    [Fact]
    public void SelfDispose_DuringBroadcast_IsSafe()
    {
        using var subject = new Subject<int>();
        var selfDisposer = new SelfDisposer(subject);
        selfDisposer.Attach();

        var other = new Counter();
        using var otherSub = subject.Subscribe(other);

        subject.OnNext(1);
        subject.OnNext(2);

        selfDisposer.Received.Should().Be(1);
        other.Count.Should().Be(2);
        subject.SubscriberCount.Should().Be(1);
    }

    [Fact]
    public void DoubleDispose_IsIdempotent()
    {
        using var subject = new Subject<int>();
        var counter = new Counter();
        var subscription = subject.Subscribe(counter);

        subscription.Dispose();
        subscription.Dispose();
        subject.Dispose();
        subject.Dispose();

        subject.SubscriberCount.Should().Be(0);
    }
}
