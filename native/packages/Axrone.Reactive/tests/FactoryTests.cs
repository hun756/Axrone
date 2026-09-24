namespace Axrone.Reactive.Tests;

public class FactoryTests
{
    private sealed class Recorder<T> : IObserver<T>
    {
        public readonly List<T> Values = new();
        public Exception? Error;
        public bool Completed;

        public void OnNext(T value) => Values.Add(value);
        public void OnError(Exception error) => Error = error;
        public void OnCompleted() => Completed = true;
    }

    [Fact]
    public void Return_EmitsOnceThenCompletes()
    {
        var recorder = new Recorder<int>();
        using var sub = Observable.Return(42).Subscribe(recorder);

        recorder.Values.Should().Equal(42);
        recorder.Completed.Should().BeTrue();
    }

    [Fact]
    public void Empty_CompletesWithoutValues()
    {
        var recorder = new Recorder<int>();
        using var sub = Observable.Empty<int>().Subscribe(recorder);

        recorder.Values.Should().BeEmpty();
        recorder.Completed.Should().BeTrue();
    }

    [Fact]
    public void Never_StaysSilent()
    {
        var recorder = new Recorder<int>();
        using var sub = Observable.Never<int>().Subscribe(recorder);

        recorder.Values.Should().BeEmpty();
        recorder.Completed.Should().BeFalse();
    }

    [Fact]
    public void Throw_TerminatesWithError()
    {
        var fault = new InvalidOperationException("factory fault");
        var recorder = new Recorder<int>();
        using var sub = Observable.Throw<int>(fault).Subscribe(recorder);

        recorder.Error.Should().BeSameAs(fault);
    }

    [Fact]
    public void Range_EmitsSequence()
    {
        var recorder = new Recorder<int>();
        using var sub = Observable.Range(5, 3).Subscribe(recorder);

        recorder.Values.Should().Equal(5, 6, 7);
        recorder.Completed.Should().BeTrue();
    }

    [Fact]
    public void Range_NegativeCount_Throws()
    {
        var act = () => Observable.Range(0, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Factories_ComposeWithOperators()
    {
        var recorder = new Recorder<string>();
        using var sub = Observable.Range(1, 5).Where(x => x % 2 == 1).Select(x => $"odd:{x}").Subscribe(recorder);

        recorder.Values.Should().Equal("odd:1", "odd:3", "odd:5");
        recorder.Completed.Should().BeTrue();
    }
}
