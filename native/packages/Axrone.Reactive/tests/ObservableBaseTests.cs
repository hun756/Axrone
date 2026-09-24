namespace Axrone.Reactive.Tests;

public class ObservableBaseTests
{
    private sealed class Health : ObservableBase<int>
    {
        private int _hp = 100;

        public void Damage(int amount)
        {
            _hp -= amount;
            Publish(_hp);
        }

        public void Kill(Exception error) => Fault(error);

        public void Retire() => Complete();
    }

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
    public void Publish_DeliversInOrder()
    {
        using var health = new Health();
        var first = new Recorder();
        var second = new Recorder();
        using var subA = health.Subscribe(first);
        using var subB = health.Subscribe(second);

        health.Damage(10);
        health.Damage(20);

        first.Values().Should().Equal(90, 70);
        second.Values().Should().Equal(90, 70);
        health.SubscriberCount.Should().Be(2);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        using var health = new Health();
        var recorder = new Recorder();
        var subscription = health.Subscribe(recorder);

        health.Damage(10);
        subscription.Dispose();
        health.Damage(10);

        recorder.Values().Should().Equal(90);
        health.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void Complete_TerminatesAndReplays()
    {
        using var health = new Health();
        var recorder = new Recorder();
        using var sub = health.Subscribe(recorder);

        health.Retire();
        health.Damage(10);

        recorder.Values().Should().BeEmpty();
        recorder.Completed().Should().BeTrue();

        var late = new Recorder();
        using var lateSub = health.Subscribe(late);
        late.Completed().Should().BeTrue();
    }

    [Fact]
    public void Fault_DeliversAndReplaysError()
    {
        using var health = new Health();
        var recorder = new Recorder();
        using var sub = health.Subscribe(recorder);

        var fault = new InvalidOperationException("flatline");
        health.Kill(fault);

        recorder.Error().Should().BeSameAs(fault);

        var late = new Recorder();
        using var lateSub = health.Subscribe(late);
        late.Error().Should().BeSameAs(fault);
    }

    [Fact]
    public void Subscribe_Null_Throws()
    {
        using var health = new Health();

        var act = () => health.Subscribe(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ConcurrentPublish_DeliversAll()
    {
        using var health = new Health();
        var recorder = new Recorder();
        using var sub = health.Subscribe(recorder);

        const int Threads = 4;
        const int PerThread = 1000;
        var tasks = new Task[Threads];
        for (int t = 0; t < Threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < PerThread; i++)
                {
                    health.Damage(0);
                }
            });
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));
        recorder.Values().Should().HaveCount(Threads * PerThread);
    }
}
