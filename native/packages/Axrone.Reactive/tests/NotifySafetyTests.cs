namespace Axrone.Reactive.Tests;

public class NotifySafetyTests
{
    [Fact]
    public void ObserverCanUnsubscribeItselfDuringNotify()
    {
        using var subject = new Subject<int>();
        var values = new List<int>();
        IDisposable? subscription = null;
        var observer = new DelegateObserver<int>(value =>
        {
            values.Add(value);
            subscription?.Dispose();
        });
        subscription = subject.Subscribe(observer);

        subject.OnNext(1);
        subject.OnNext(2);

        values.Should().Equal(1);
        subject.SubscriberCount.Should().Be(0);
        subscription.Dispose();
    }

    [Fact]
    public void ObserverCanSubscribeAnotherDuringNotify()
    {
        using var subject = new Subject<int>();
        var late = new RecordingObserver<int>();
        IDisposable? lateSubscription = null;
        var added = false;
        var first = new DelegateObserver<int>(_ =>
        {
            if (!added)
            {
                added = true;
                lateSubscription = subject.Subscribe(late);
            }
        });
        using var firstSubscription = subject.Subscribe(first);

        subject.OnNext(1);
        late.Values.Should().BeEmpty();

        subject.OnNext(2);
        late.Values.Should().Equal(2);
        lateSubscription!.Dispose();
    }

    [Fact]
    public void DisposingAnotherSubscriptionDuringNotifyKeepsTheCurrentSnapshot()
    {
        using var subject = new Subject<int>();
        var firstValues = new List<int>();
        var secondValues = new List<int>();
        IDisposable? secondSubscription = null;
        var first = new DelegateObserver<int>(value =>
        {
            firstValues.Add(value);
            secondSubscription?.Dispose();
        });
        var second = new DelegateObserver<int>(value => secondValues.Add(value));
        using var firstSubscription = subject.Subscribe(first);
        secondSubscription = subject.Subscribe(second);

        subject.OnNext(1);
        subject.OnNext(2);

        firstValues.Should().Equal(1, 2);
        secondValues.Should().Equal(1);
        secondSubscription.Dispose();
    }

    [Fact]
    public async Task ConcurrentSubscribeAndPublishStaysConsistent()
    {
        using var subject = new Subject<int>();
        var observers = new CountingObserver<int>[4];
        for (int index = 0; index < observers.Length; index++)
        {
            observers[index] = new CountingObserver<int>();
            subject.Subscribe(observers[index]);
        }

        var publisher = Task.Run(() =>
        {
            for (int index = 0; index < 5000; index++)
            {
                subject.OnNext(index);
            }
        });

        var churn = new Task[4];
        for (int worker = 0; worker < churn.Length; worker++)
        {
            int workerId = worker;
            churn[worker] = Task.Run(() =>
            {
                for (int index = 0; index < 1000; index++)
                {
                    var observer = new CountingObserver<int>();
                    IDisposable subscription = subject.Subscribe(
                        observer,
                        (workerId & 1) == 0 ? ObservationPriority.High : ObservationPriority.Low);
                    subscription.Dispose();
                }
            });
        }

        await Task.WhenAll(churn).WaitAsync(TimeSpan.FromSeconds(15));
        await publisher.WaitAsync(TimeSpan.FromSeconds(15));

        subject.SubscriberCount.Should().Be(4);
        subject.IsTerminated.Should().BeFalse();
    }

    private sealed class DelegateObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;

        public DelegateObserver(Action<T> onNext) => _onNext = onNext;

        public void OnNext(T value) => _onNext(value);

        public void OnError(Exception error) => throw error;

        public void OnCompleted()
        {
        }
    }
}
