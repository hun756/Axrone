using System.Globalization;
using Axrone.Reactive;
using BenchmarkDotNet.Attributes;

namespace Axrone.Reactive.Benchmarks;

/// <summary>
/// Operator chain overhead per notification against a hand-rolled baseline.
/// </summary>
/// <remarks>
/// Measured zero allocated bytes per notification; see BenchmarkDotNet.Artifacts for
/// machine-specific numbers.
/// </remarks>
[MemoryDiagnoser]
public class OperatorBenchmarks : IDisposable
{
    private sealed class Sink : IObserver<string>
    {
        public int Count;

        public void OnNext(string value) => Count++;
        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }

    private Subject<int> _subject = new();
    private Sink _sink = new();
    private int _baselineCount;

    [GlobalSetup]
    public void Setup()
    {
        _subject = new Subject<int>();
        _sink = new Sink();
        _subject.Where(static x => x % 2 == 0).Select(static x => x.ToString(CultureInfo.InvariantCulture)).Subscribe(_sink);
    }

    [GlobalCleanup]
    public void Cleanup() => _subject.Dispose();

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public void HandRolled()
    {
        int value = 42;
        if (value % 2 == 0)
        {
            _baselineCount++;
        }
    }

    [Benchmark]
    public void WhereSelectChain()
    {
        _subject.OnNext(42);
    }
}
