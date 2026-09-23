using Axrone.Tween;
using BenchmarkDotNet.Attributes;

namespace Axrone.Tween.Benchmarks;

/// <summary>
/// Tick cost over live tweens with callback-free specs (pure engine cost, no user code).
/// </summary>
/// <remarks>
/// Infinite loops keep the live set stable no matter how many iterations the harness pilots;
/// finite tweens would decay the workload to an empty ring. Setup is excluded. Measured
/// ~9ns per tween with zero allocated bytes; see BenchmarkDotNet.Artifacts for machine numbers.
/// </remarks>
[MemoryDiagnoser]
public class TickBenchmarks : IDisposable
{
    [Params(1, 64, 512)]
    public int ActiveTweens { get; set; }

    private TweenEngine _engine = new(1024);
    private DurationNs _step = DurationNs.FromMilliseconds(16.666f);

    [GlobalSetup]
    public void Setup()
    {
        _engine = new TweenEngine(1024);
        _step = DurationNs.FromMilliseconds(16.666f);
        for (int i = 0; i < ActiveTweens; i++)
        {
            // Infinite loops keep the live set stable no matter how many iterations
            // BenchmarkDotNet pilots run; finite tweens would decay the workload to empty.
            _engine.Play(new TweenBuilder().From(0f).To(1f).DurationSeconds(100f).Mode(PlaybackMode.Loop).Loops(-1).Build());
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _engine.Dispose();

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    [Benchmark]
    public void Tick()
    {
        _engine.Update(_step);
    }
}
