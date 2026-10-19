namespace Axrone.Profiler.Tests;

public sealed class ClockTests
{
    [Fact]
    public void Nominals_Convert_And_Compare()
    {
        var ns = new Nanoseconds(1_500_000_000L);
        Microseconds.FromNanoseconds(ns).Value.Should().Be(1_500_000L);
        Milliseconds.FromNanoseconds(ns).Value.Should().BeApproximately(1500.0, 1e-9);
        Seconds.FromNanoseconds(ns).Value.Should().BeApproximately(1.5, 1e-12);

        var duration = new Duration(ns);
        duration.Microseconds.Should().Be(1_500_000L);
        duration.Milliseconds.Should().BeApproximately(1500.0, 1e-9);
        duration.Seconds.Should().BeApproximately(1.5, 1e-12);

        (new Nanoseconds(1L) < new Nanoseconds(2L)).Should().BeTrue();
        (new Nanoseconds(2L) - new Nanoseconds(1L)).Should().Be(new Nanoseconds(1L));

        TimeConversion.Microseconds.Should().Be(1_000L);
        TimeConversion.Seconds.Should().Be(1_000_000_000L);
    }

    [Fact]
    public void MonotonicClock_Runs_And_Pauses()
    {
        var clock = new MonotonicClock();
        Action readStopped = () => clock.Now();
        readStopped.Should().Throw<ClockNotRunningException>().Where(e => e.Code == "CLOCK_NOT_RUNNING");

        clock.Start();
        Action doubleStart = () => clock.Start();
        doubleStart.Should().Throw<ClockAlreadyRunningException>();

        Thread.Sleep(5);
        clock.Now().Value.Should().BeGreaterThan(0L);
        clock.Tick();
        clock.Tick();
        clock.TickCount.Should().Be(2L);
        clock.Elapsed().TotalNanoseconds.Value.Should().BeGreaterThan(0L);

        clock.Pause();
        Nanoseconds frozen = clock.Now();
        Thread.Sleep(5);
        clock.Now().Should().Be(frozen);

        clock.Resume();
        Thread.Sleep(5);
        clock.Now().Value.Should().BeGreaterThan(frozen.Value);
        clock.Stop();
    }

    [Fact]
    public void WallClock_Detects_Skew()
    {
        var clock = new WallClock(maxSkewMs: 1000L);
        clock.Start();

        long skew = clock.DetectSkew(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        skew.Should().BeLessThanOrEqualTo(1000L);
        clock.ToUnixMs().Should().BeGreaterThan(0L);

        Action skewed = () => clock.DetectSkew(0L);
        skewed.Should().Throw<ClockSkewException>().Where(e => e.Code == "CLOCK_SKEW");
        clock.Stop();
    }

    [Fact]
    public void VirtualClock_Scales_And_Clamps()
    {
        var clock = new VirtualClock(maxTimeNs: 1000L);
        clock.Start();
        clock.SetTime(new Nanoseconds(100L));
        clock.Now().Should().Be(new Nanoseconds(100L));

        clock.SetTimeScale(2.0);
        clock.AdvanceBy(new Nanoseconds(100L));
        clock.Now().Should().Be(new Nanoseconds(300L));

        clock.AdvanceBy(new Nanoseconds(10_000L));
        clock.Now().Should().Be(new Nanoseconds(1000L));

        Action negative = () => clock.AdvanceBy(new Nanoseconds(-1L));
        negative.Should().Throw<ArgumentException>();
        clock.Stop();
    }

    [Fact]
    public void Stopwatch_Laps_And_Restarts()
    {
        var watch = new ProfilerStopwatch();
        Action readFresh = () => watch.Elapsed();
        readFresh.Should().Throw<ClockNotRunningException>();
        watch.Lap().Should().BeNull();

        watch.Start();
        watch.IsRunning.Should().BeTrue();
        Thread.Sleep(5);
        LapResult? lap = watch.Lap();
        lap.Should().NotBeNull();
        lap!.Value.LapNumber.Should().Be(1);
        lap.Value.LapDuration.TotalNanoseconds.Value.Should().BeGreaterThan(0L);
        watch.Elapsed().Should().NotBeNull();

        watch.Stop();
        watch.IsRunning.Should().BeFalse();

        watch.Restart();
        watch.IsRunning.Should().BeTrue();
        watch.Reset();
        watch.Stop();
    }

    [Fact]
    public void Clocks_Expose_Metadata_And_Metrics()
    {
        var mono = new MonotonicClock("main", ClockPrecision.High);
        mono.GetMetadata().Should().Be(new ClockMetadata("main", Stopwatch.Frequency, ClockPrecision.High));
        mono.GetMetrics().TickCount.Should().Be(0L);
        mono.GetMetrics().State.Should().Be(ClockState.Stopped);
        mono.Start();
        mono.Tick();
        mono.GetMetrics().TickCount.Should().Be(1L);
        mono.Stop();

        var wall = new WallClock();
        wall.Start();
        wall.GetMetadata().ClockId.Should().Be("wall");
        wall.GetMetadata().ResolutionHz.Should().Be(TimeConversion.Milliseconds);
        wall.GetMetrics().State.Should().Be(ClockState.Running);
        wall.Stop();

        var virt = new VirtualClock();
        virt.Start();
        virt.GetMetadata().Precision.Should().Be(ClockPrecision.High);
        virt.GetMetrics().State.Should().Be(ClockState.Running);
        virt.Stop();
    }

    [Fact]
    public async Task WaitForNextTick_Returns_After_Tick()
    {
        var clock = new MonotonicClock();
        clock.Start();
        try
        {
            ValueTask<Nanoseconds> wait = clock.WaitForNextTickAsync();
            clock.Tick();
            Nanoseconds stamped = await wait;
            stamped.Value.Should().BeGreaterThan(0L);
        }
        finally
        {
            clock.Stop();
        }
    }

    [Fact]
    public async Task WaitForNextTick_Honors_Cancellation()
    {
        var clock = new MonotonicClock();
        clock.Start();
        try
        {
            using var cts = new CancellationTokenSource(50);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clock.WaitForNextTickAsync(cts.Token).AsTask());
        }
        finally
        {
            clock.Stop();
        }
    }
}
