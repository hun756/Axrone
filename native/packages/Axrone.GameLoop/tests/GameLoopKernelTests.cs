namespace Axrone.GameLoop.Tests;

internal sealed class CountingSystem :
    IGameLoopSystem<int>,
    IBeforeUpdatePhase<int>,
    IFixedUpdatePhase<int>,
    IUpdatePhase<int>,
    IRenderPhase<int>,
    IAfterFramePhase<int>
{
    public SystemId Id { get; } = SystemId.FromString("counter");
    public int BeforeUpdates;
    public int FixedUpdates;
    public int Updates;
    public int Renders;
    public int AfterFrames;

    void IBeforeUpdatePhase<int>.BeforeUpdate(ref readonly BeforeUpdateContext<int> ctx) => BeforeUpdates++;
    void IFixedUpdatePhase<int>.FixedUpdate(ref readonly FixedUpdateContext<int> ctx) => FixedUpdates++;
    void IUpdatePhase<int>.Update(ref readonly UpdateContext<int> ctx) => Updates++;
    void IRenderPhase<int>.Render(ref readonly RenderContext<int> ctx) => Renders++;
    void IAfterFramePhase<int>.AfterFrame(ref readonly AfterFrameContext<int> ctx) => AfterFrames++;
}

public sealed class GameLoopKernelTests : IDisposable
{
    private readonly GameLoopKernel<int, HighResolutionStopwatchClock, ModuloSpiralMitigation, FailFastErrorPolicy<int>> _kernel;
    private readonly CountingSystem _counter = new();

    public GameLoopKernelTests()
    {
        _kernel = new GameLoopKernel<int, HighResolutionStopwatchClock, ModuloSpiralMitigation, FailFastErrorPolicy<int>>(
            FixedStep.SixtyFps,
            GameTime.FromMilliseconds(250.0),
            8U,
            TimeScale.Identity);
        _kernel.Registry.Register(_counter);
    }

    public void Dispose()
    {
        _kernel.Dispose();
        GC.SuppressFinalize(this);
    }

    private static GameTime At(double ms) => GameTime.FromMilliseconds(ms);

    [Fact]
    public void Advance_Runs_AllPhases_WithFixedSteps()
    {
        var state = 0;
        _kernel.TransitionToRunning(At(0.0));

        FrameExecutionOutcome outcome = _kernel.Advance(ref state, At(33.334));

        outcome.Kind.Should().Be(FrameOutcomeKind.Executed);
        outcome.Frame.Should().Be(new FrameIndex(1UL));
        outcome.FixedStepsExecuted.Should().Be(2U);
        _kernel.Frame.Should().Be(new FrameIndex(1UL));
        _counter.BeforeUpdates.Should().Be(1);
        _counter.FixedUpdates.Should().Be(2);
        _counter.Updates.Should().Be(1);
        _counter.Renders.Should().Be(1);
        _counter.AfterFrames.Should().Be(1);
    }

    [Fact]
    public void ZeroDelta_Throttles()
    {
        var state = 0;
        _kernel.TransitionToRunning(At(100.0));

        FrameExecutionOutcome outcome = _kernel.Advance(ref state, At(100.0));

        outcome.Kind.Should().Be(FrameOutcomeKind.ThrottledZeroDelta);
        _kernel.Frame.Should().Be(FrameIndex.Zero);
    }

    [Fact]
    public void Spiral_Mitigates_OverBudget()
    {
        var state = 0;
        _kernel.TransitionToRunning(At(0.0));

        FrameExecutionOutcome outcome = _kernel.Advance(ref state, At(1000.0));

        outcome.Kind.Should().Be(FrameOutcomeKind.SpiralMitigated);
        outcome.FixedStepsExecuted.Should().Be(8U);
        outcome.DroppedAccumulator.TotalMilliseconds.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void Lifecycle_Transitions_Work()
    {
        var state = 0;
        _kernel.Status.Should().Be(GameLoopStatus.Idle);

        _kernel.TransitionToRunning(At(0.0));
        _kernel.Status.Should().Be(GameLoopStatus.Running);

        _kernel.TransitionToPaused();
        _kernel.Status.Should().Be(GameLoopStatus.Paused);

        FrameExecutionOutcome halted = _kernel.Advance(ref state, At(16.0));
        halted.Kind.Should().Be(FrameOutcomeKind.Halted);

        _kernel.TransitionToStopped();
        _kernel.Status.Should().Be(GameLoopStatus.Stopped);

        _kernel.Dispose();
        _kernel.Status.Should().Be(GameLoopStatus.Disposed);
    }

    [Fact]
    public void Use_AfterDispose_Throws()
    {
        var kernel = new GameLoopKernel<int, HighResolutionStopwatchClock, ModuloSpiralMitigation, FailFastErrorPolicy<int>>(
            FixedStep.SixtyFps, GameTime.FromMilliseconds(250.0), 8U, TimeScale.Identity);
        kernel.Dispose();

        Action run = () => kernel.TransitionToRunning(At(0.0));
        run.Should().Throw<GameLoopDisposedException>();
    }
}
