using Axrone.Reactive;
using BenchmarkDotNet.Attributes;

namespace Axrone.Reactive.Benchmarks;

/// <summary>
/// Dispatch cost per notification against a bare delegate invocation baseline.
/// </summary>
/// <remarks>
/// The baseline invokes one <see cref="Action{T}"/> directly; the subject cases add a snapshot
/// read plus one virtual dispatch per subscriber. Subscribe-time setup is excluded.
/// Measured zero allocated bytes per notification with single-digit nanosecond dispatch;
/// see BenchmarkDotNet.Artifacts for machine-specific numbers.
/// </remarks>
[MemoryDiagnoser]
public class PublishBenchmarks : IDisposable
{
    private sealed class Sink : IObserver<int>
    {
        public int Total;

        public void OnNext(int value) => Total += value;
        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }

    private Subject<int> _subject = new();
    private Subject<int> _wide = new();
    private Sink _sink = new();
    private Sink _wideSink = new();
    private Action<int> _baseline = static _ => { };

    [GlobalSetup]
    public void Setup()
    {
        _subject = new Subject<int>();
        _sink = new Sink();
        _subject.Subscribe(_sink);

        _wide = new Subject<int>();
        _wideSink = new Sink();
        for (int i = 0; i < 8; i++)
        {
            _wide.Subscribe(_wideSink);
        }

        _baseline = static value => { };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _subject.Dispose();
        _wide.Dispose();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public void DelegateInvoke()
    {
        _baseline(1);
    }

    [Benchmark]
    public void SubjectPublishSingleSubscriber()
    {
        _subject.OnNext(1);
    }

    [Benchmark]
    public void SubjectPublishEightSubscribers()
    {
        _wide.OnNext(1);
    }
}
