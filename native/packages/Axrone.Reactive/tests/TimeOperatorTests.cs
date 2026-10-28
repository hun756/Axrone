namespace Axrone.Reactive.Tests;

public class TimeOperatorTests
{
    [Fact]
    public void DebounceEmitsOnlyTheLastValueAfterTheQuietPeriod()
    {
        var time = new ManualTimeProvider();
        using var source = new Subject<int>();
        var observer = new RecordingObserver<int>();
        using var subscription = source
            .Debounce(TimeSpan.FromMilliseconds(10), time)
            .Subscribe(observer);

        source.OnNext(1);
        source.OnNext(2);
        time.Advance(9);
        observer.Values.Should().BeEmpty();

        time.Advance(1);
        observer.Values.Should().Equal(2);

        source.OnNext(3);
        time.Advance(10);
        observer.Values.Should().Equal(2, 3);
    }

    [Fact]
    public void DebounceCompletionDropsThePendingValue()
    {
        var time = new ManualTimeProvider();
        using var source = new Subject<int>();
        var observer = new RecordingObserver<int>();
        using var subscription = source
            .Debounce(TimeSpan.FromMilliseconds(10), time)
            .Subscribe(observer);

        source.OnNext(1);
        source.OnCompleted();
        time.Advance(20);

        observer.Values.Should().BeEmpty();
        observer.Completed.Should().BeTrue();
    }

    [Fact]
    public void DebounceDisposeCancelsThePendingTimer()
    {
        var time = new ManualTimeProvider();
        using var source = new Subject<int>();
        var observer = new RecordingObserver<int>();
        IDisposable subscription = source
            .Debounce(TimeSpan.FromMilliseconds(10), time)
            .Subscribe(observer);

        source.OnNext(1);
        subscription.Dispose();
        time.Advance(20);

        observer.Values.Should().BeEmpty();
        source.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void ZeroDebounceIsTheIdentityStream()
    {
        using var source = new Subject<int>();
        source.Debounce(TimeSpan.Zero).Should().BeSameAs(source);
    }

    [Fact]
    public void ThrottleEmitsLeadingEdgeAndDropsInsideTheWindow()
    {
        var time = new ManualTimeProvider();
        using var source = new Subject<int>();
        var observer = new RecordingObserver<int>();
        using var subscription = source
            .Throttle(TimeSpan.FromMilliseconds(10), time)
            .Subscribe(observer);

        source.OnNext(1);
        source.OnNext(2);
        observer.Values.Should().Equal(1);

        time.Advance(5);
        source.OnNext(3);
        observer.Values.Should().Equal(1);

        time.Advance(5);
        source.OnNext(4);
        observer.Values.Should().Equal(1, 4);

        source.OnCompleted();
        observer.Completed.Should().BeTrue();
    }

    [Fact]
    public void ThrottleForwardsErrorWithoutFurtherValues()
    {
        var time = new ManualTimeProvider();
        using var source = new Subject<int>();
        var observer = new RecordingObserver<int>();
        using var subscription = source
            .Throttle(TimeSpan.FromMilliseconds(10), time)
            .Subscribe(observer);

        source.OnNext(1);
        var fault = new InvalidOperationException("throttle fault");
        source.OnError(fault);
        source.OnNext(2);

        observer.Values.Should().Equal(1);
        observer.Error.Should().BeSameAs(fault);
    }

    [Fact]
    public void ZeroThrottleIsTheIdentityStream()
    {
        using var source = new Subject<int>();
        source.Throttle(TimeSpan.Zero).Should().BeSameAs(source);
    }

    [Fact]
    public void DebounceNotifyDoesNotAllocate()
    {
        var time = new ManualTimeProvider();
        using var source = new Subject<int>();
        var observer = new CountingObserver<int>();
        using var subscription = source
            .Debounce(TimeSpan.FromMilliseconds(10), time)
            .Subscribe(observer);

        for (int index = 0; index < 200; index++)
        {
            source.OnNext(index);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            source.OnNext(index);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
    }

    [Fact]
    public void ThrottleNotifyDoesNotAllocate()
    {
        var time = new ManualTimeProvider();
        using var source = new Subject<int>();
        var observer = new CountingObserver<int>();
        using var subscription = source
            .Throttle(TimeSpan.FromMilliseconds(10), time)
            .Subscribe(observer);

        for (int index = 0; index < 200; index++)
        {
            source.OnNext(index);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            source.OnNext(index);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
    }
}
