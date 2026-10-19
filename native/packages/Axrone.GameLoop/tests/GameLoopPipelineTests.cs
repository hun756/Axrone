namespace Axrone.GameLoop.Tests;

public sealed class GameLoopPipelineTests : IDisposable
{
    private static GameLoopKernel<int, HighResolutionStopwatchClock, ModuloSpiralMitigation, FailFastErrorPolicy<int>> NewKernel() =>
        new(FixedStep.SixtyFps, GameTime.FromMilliseconds(250.0), 8U, TimeScale.Identity);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Typestate_Walks_Start_Pause_Resume_Stop()
    {
        using var kernel = NewKernel();
        var pipeline = new GameLoopPipeline<int, HighResolutionStopwatchClock, ModuloSpiralMitigation, FailFastErrorPolicy<int>, StageIdle>(kernel);

        var running = pipeline.Start(GameTime.FromMilliseconds(0.0));
        running.Status.Should().Be(GameLoopStatus.Running);

        var paused = running.Pause();
        paused.Status.Should().Be(GameLoopStatus.Paused);

        var resumed = paused.Resume(GameTime.FromMilliseconds(0.0));
        resumed.Status.Should().Be(GameLoopStatus.Running);

        var stopped = resumed.Stop();
        stopped.Status.Should().Be(GameLoopStatus.Stopped);

        var restarted = stopped.Restart(GameTime.FromMilliseconds(0.0));
        restarted.Status.Should().Be(GameLoopStatus.Running);

        var disposed = restarted.Dispose();
        disposed.Status.Should().Be(GameLoopStatus.Disposed);
    }

    [Fact]
    public void Advance_ThroughPipeline_Works()
    {
        using var kernel = NewKernel();
        var pipeline = new GameLoopPipeline<int, HighResolutionStopwatchClock, ModuloSpiralMitigation, FailFastErrorPolicy<int>, StageIdle>(kernel);
        var running = pipeline.Start(GameTime.FromMilliseconds(0.0));

        int state = 0;
        FrameExecutionOutcome outcome = running.Advance(ref state, GameTime.FromMilliseconds(33.334));
        outcome.Kind.Should().Be(FrameOutcomeKind.Executed);
        outcome.FixedStepsExecuted.Should().Be(2U);
    }

    [Fact]
    public void Builder_Validates_AndBuilds()
    {
        var pipeline = GameLoopFactory.CreateBuilder<int>()
            .WithFixedStep(FixedStep.SixtyFps)
            .WithMaxDelta(GameTime.FromMilliseconds(100.0))
            .WithMaxSubSteps(4U)
            .WithTimeScale(TimeScale.Identity)
            .WithCapacity(8)
            .BuildPipeline();

        pipeline.Status.Should().Be(GameLoopStatus.Idle);
        pipeline.Kernel.MaxSubSteps.Should().Be(4U);
        pipeline.Kernel.Dispose();

        Action bad = () => GameLoopFactory.CreateBuilder<int>().WithMaxSubSteps(0U);
        bad.Should().Throw<GameLoopConfigurationException>();
    }

    [Fact]
    public void Snapshot_RoundTrips_Frame()
    {
        using var kernel = NewKernel();
        int state = 7;
        kernel.TransitionToRunning(GameTime.FromMilliseconds(0.0));
        kernel.Advance(ref state, GameTime.FromMilliseconds(33.334));

        GameLoopSnapshot<int> snapshot = kernel.CaptureSnapshot(state);
        snapshot.Frame.Should().Be(new FrameIndex(1UL));
        snapshot.State.Should().Be(7);
        snapshot.Status.Should().Be(GameLoopStatus.Running);

        kernel.Advance(ref state, GameTime.FromMilliseconds(50.0));
        kernel.RestoreSnapshot(in snapshot, out int restored);
        restored.Should().Be(7);
        kernel.Frame.Should().Be(new FrameIndex(1UL));
    }

    [Fact]
    public async Task ThreadHost_Starts_AndDrains()
    {
        using var kernel = NewKernel();
        var host = new GameLoopThreadHost<int, HighResolutionStopwatchClock, ModuloSpiralMitigation, FailFastErrorPolicy<int>>(kernel, 0, 1000.0);
        host.Start();
        host.MutateState((ref int s) => s = 41);
        host.State.Should().Be(41);
        await host.DisposeAsync();
    }
}
