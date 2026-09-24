namespace Axrone.Reactive.Tests;

public class ScanTests
{
    private sealed class Recorder : IObserver<int>
    {
        public readonly List<int> Values = new();
        public bool Completed;

        public void OnNext(int value) => Values.Add(value);
        public void OnError(Exception error) => throw new NotSupportedException();
        public void OnCompleted() => Completed = true;
    }

    [Fact]
    public void Scan_EmitsRunningAccumulation()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder();
        using var sub = subject.Scan(0, (total, next) => total + next).Subscribe(recorder);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(3);

        recorder.Values.Should().Equal(1, 3, 6);
    }

    [Fact]
    public void Scan_SeedIsNotEmitted()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder();
        using var sub = subject.Scan(100, (total, next) => total + next).Subscribe(recorder);

        subject.OnNext(1);

        recorder.Values.Should().Equal(101);
    }

    [Fact]
    public void Scan_ComposesAfterWhere()
    {
        using var subject = new Subject<int>();
        var recorder = new Recorder();
        using var sub = subject.Where(x => x % 2 == 0).Scan(0, (total, next) => total + next).Subscribe(recorder);

        subject.OnNext(1);
        subject.OnNext(2);
        subject.OnNext(4);

        recorder.Values.Should().Equal(2, 6);
        recorder.Completed.Should().BeFalse();
    }
}
