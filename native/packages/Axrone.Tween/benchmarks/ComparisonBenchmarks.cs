using Axrone.Tween;
using BenchmarkDotNet.Attributes;

namespace Axrone.Tween.Benchmarks;

/// <summary>
/// Head-to-head costs: callback-free ticks versus ticks with user callbacks, and
/// built-in curve dispatch versus custom-delegate curves.
/// </summary>
/// <remarks>
/// Infinite loops keep the live set stable no matter how many iterations the harness
/// pilots; finite tweens would decay the workload to an empty ring. Setup is excluded.
/// </remarks>
[MemoryDiagnoser]
public class ComparisonBenchmarks : IDisposable
{
    [Params(64, 512)]
    public int ActiveTweens { get; set; }

    private TweenEngine _engine = new(1024);
    private TweenEngine _callbackEngine = new(1024);
    private DurationNs _step = DurationNs.FromMilliseconds(16.666f);
    private float[] _sink = new float[1024];
    private Func<float, float> _punch = t => t;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new TweenEngine(1024);
        _callbackEngine = new TweenEngine(1024);
        _step = DurationNs.FromMilliseconds(16.666f);
        _sink = new float[1024];
        _punch = TweenPresets.Punch(1.0f, 1.0f).CustomEasing!;
        for (int i = 0; i < ActiveTweens; i++)
        {
            int slot = i;
            _engine.Play(new TweenBuilder().From(0f).To(1f).DurationSeconds(100f).Mode(PlaybackMode.Loop).Loops(-1).Build());
            _callbackEngine.Play(
                new TweenBuilder().From(0f).To(1f).DurationSeconds(100f).Mode(PlaybackMode.Loop).Loops(-1)
                    .OnUpdate(v => _sink[slot] = v)
                    .Build());
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _engine.Dispose();
        _callbackEngine.Dispose();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public void TickCallbackFree()
    {
        _engine.Update(_step);
    }

    [Benchmark]
    public void TickWithFloatCallbacks()
    {
        _callbackEngine.Update(_step);
    }

    [Benchmark]
    public float EaseLinear()
    {
        float acc = _sink[0];
        for (int i = 0; i < 1000; i++)
        {
            acc += EasingEvaluator.Evaluate(EasingKind.Linear, (i % 101) / 100.0f);
        }

        return acc;
    }

    [Benchmark]
    public float EaseOutElastic()
    {
        float acc = _sink[0];
        for (int i = 0; i < 1000; i++)
        {
            acc += EasingEvaluator.Evaluate(EasingKind.EaseOutElastic, (i % 101) / 100.0f);
        }

        return acc;
    }

    [Benchmark]
    public float EaseCustomPunch()
    {
        float acc = _sink[0];
        for (int i = 0; i < 1000; i++)
        {
            acc += _punch((i % 101) / 100.0f);
        }

        return acc;
    }
}
