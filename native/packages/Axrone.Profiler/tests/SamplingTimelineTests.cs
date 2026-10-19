namespace Axrone.Profiler.Tests;

public sealed class SamplingTimelineTests : IDisposable
{
    private readonly List<IDisposable> _owned = new();

    public void Dispose()
    {
        foreach (IDisposable owned in _owned)
        {
            owned.Dispose();
        }

        _owned.Clear();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Sampler_Fires_Ticks_And_Tracks_State()
    {
        using var ticks = new ManualResetEventSlim(false);
        int count = 0;
        var sampler = new ContinuousSampler(
            onTick: _ =>
            {
                if (Interlocked.Increment(ref count) >= 2)
                {
                    ticks.Set();
                }
            },
            intervalMs: 5.0,
            minIntervalMs: 1.0,
            maxIntervalMs: 50.0);
        _owned.Add(sampler);

        sampler.Start();
        ticks.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
        sampler.Stop();

        sampler.TickCount.Should().BeGreaterThanOrEqualTo(2);
        sampler.GetState().IsRunning.Should().BeFalse();
        sampler.GetState().IsAdaptive.Should().BeTrue();
    }

    [Fact]
    public void Sampler_Adapts_Interval_To_Tick_Cost()
    {
        using var ticks = new ManualResetEventSlim(false);
        int count = 0;
        var sampler = new ContinuousSampler(
            onTick: _ =>
            {
                Thread.Sleep(2);
                if (Interlocked.Increment(ref count) >= 8)
                {
                    ticks.Set();
                }
            },
            intervalMs: 10.0,
            minIntervalMs: 10.0,
            maxIntervalMs: 100.0,
            targetFrameBudgetMs: 1.0);
        _owned.Add(sampler);

        sampler.Start();
        ticks.Wait(TimeSpan.FromSeconds(15)).Should().BeTrue();
        sampler.Stop();

        sampler.IntervalMs.Should().BeGreaterThan(10.0);
        sampler.IntervalMs.Should().BeLessThanOrEqualTo(100.0);
    }

    [Fact]
    public void Sampler_Rejects_Bad_Configuration()
    {
        Action badInterval = () => _ = new ContinuousSampler(_ => { }, intervalMs: 500.0, minIntervalMs: 1.0, maxIntervalMs: 100.0);
        badInterval.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Timeline_Records_And_Caps_Events()
    {
        using var recorder = new TimelineRecorder(maxEvents: 4, heartbeatMs: 10_000);
        recorder.Start();
        recorder.RecordEvent(TimelineCategory.Memory, "heap:1");
        recorder.RecordEvent(TimelineCategory.Gc, "gc:2");
        recorder.Tick();
        recorder.RecordEvent(TimelineCategory.Cpu, "frame");
        recorder.RecordEvent(TimelineCategory.Allocation, "alloc");
        recorder.RecordEvent(TimelineCategory.Memory, "heap:6");

        IReadOnlyList<TimelineRecordEvent> events = recorder.GetEvents();
        events.Should().HaveCount(4);
        events.Should().OnlyContain(e => e.TimestampMs > 0L);
        events[^1].Message.Should().Be("heap:6");

        recorder.Clear();
        recorder.Count.Should().Be(0);
        recorder.Stop();
        recorder.IsActive.Should().BeFalse();

        Action stopped = () => recorder.RecordEvent(TimelineCategory.Cpu, "late");
        stopped.Should().Throw<InvalidOperationException>();
    }
}
