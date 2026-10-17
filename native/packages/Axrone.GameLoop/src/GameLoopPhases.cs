namespace Axrone.GameLoop;

/// <summary>
/// BeforeUpdate phase context: full frame state, no alpha yet.
/// </summary>
public readonly ref struct BeforeUpdateContext<TState> where TState : struct
{
    /// <summary>Mutable frame state.</summary>
    public readonly ref TState State;

    /// <summary>Frame index.</summary>
    public readonly FrameIndex Frame;

    /// <summary>Clock time.</summary>
    public readonly GameTime Time;

    /// <summary>Accumulated scaled time.</summary>
    public readonly GameTime Elapsed;

    /// <summary>Scaled delta.</summary>
    public readonly GameTime Delta;

    /// <summary>Unscaled delta.</summary>
    public readonly GameTime UnscaledDelta;

    /// <summary>Fixed-step backlog.</summary>
    public readonly GameTime Accumulator;

    /// <summary>Fixed step.</summary>
    public readonly FixedStep FixedDelta;

    /// <summary>Time multiplier.</summary>
    public readonly TimeScale TimeScale;

    /// <summary>Creates a context.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BeforeUpdateContext(
        ref TState state,
        FrameIndex frame,
        GameTime time,
        GameTime elapsed,
        GameTime delta,
        GameTime unscaledDelta,
        GameTime accumulator,
        FixedStep fixedDelta,
        TimeScale timeScale)
    {
        State = ref state;
        Frame = frame;
        Time = time;
        Elapsed = elapsed;
        Delta = delta;
        UnscaledDelta = unscaledDelta;
        Accumulator = accumulator;
        FixedDelta = fixedDelta;
        TimeScale = timeScale;
    }
}

/// <summary>
/// FixedUpdate phase context: step counters included.
/// </summary>
public readonly ref struct FixedUpdateContext<TState> where TState : struct
{
    /// <summary>Mutable frame state.</summary>
    public readonly ref TState State;

    /// <summary>Frame index.</summary>
    public readonly FrameIndex Frame;

    /// <summary>Clock time.</summary>
    public readonly GameTime Time;

    /// <summary>Accumulated scaled time.</summary>
    public readonly GameTime Elapsed;

    /// <summary>Scaled step.</summary>
    public readonly FixedStep StepDelta;

    /// <summary>Unscaled step.</summary>
    public readonly GameTime UnscaledStepDelta;

    /// <summary>Fixed-step backlog.</summary>
    public readonly GameTime Accumulator;

    /// <summary>Fixed step.</summary>
    public readonly FixedStep FixedDelta;

    /// <summary>Time multiplier.</summary>
    public readonly TimeScale TimeScale;

    /// <summary>Current step in this frame, one-based.</summary>
    public readonly uint CurrentStep;

    /// <summary>Steps running this frame.</summary>
    public readonly uint MaxSteps;

    /// <summary>Creates a context.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedUpdateContext(
        ref TState state,
        FrameIndex frame,
        GameTime time,
        GameTime elapsed,
        FixedStep stepDelta,
        GameTime unscaledStepDelta,
        GameTime accumulator,
        FixedStep fixedDelta,
        TimeScale timeScale,
        uint currentStep,
        uint maxSteps)
    {
        State = ref state;
        Frame = frame;
        Time = time;
        Elapsed = elapsed;
        StepDelta = stepDelta;
        UnscaledStepDelta = unscaledStepDelta;
        Accumulator = accumulator;
        FixedDelta = fixedDelta;
        TimeScale = timeScale;
        CurrentStep = currentStep;
        MaxSteps = maxSteps;
    }
}

/// <summary>
/// Variable update phase context.
/// </summary>
public readonly ref struct UpdateContext<TState> where TState : struct
{
    /// <summary>Mutable frame state.</summary>
    public readonly ref TState State;

    /// <summary>Frame index.</summary>
    public readonly FrameIndex Frame;

    /// <summary>Clock time.</summary>
    public readonly GameTime Time;

    /// <summary>Accumulated scaled time.</summary>
    public readonly GameTime Elapsed;

    /// <summary>Scaled delta.</summary>
    public readonly GameTime Delta;

    /// <summary>Unscaled delta.</summary>
    public readonly GameTime UnscaledDelta;

    /// <summary>Fixed-step backlog.</summary>
    public readonly GameTime Accumulator;

    /// <summary>Fixed step.</summary>
    public readonly FixedStep FixedDelta;

    /// <summary>Time multiplier.</summary>
    public readonly TimeScale TimeScale;

    /// <summary>Creates a context.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UpdateContext(
        ref TState state,
        FrameIndex frame,
        GameTime time,
        GameTime elapsed,
        GameTime delta,
        GameTime unscaledDelta,
        GameTime accumulator,
        FixedStep fixedDelta,
        TimeScale timeScale)
    {
        State = ref state;
        Frame = frame;
        Time = time;
        Elapsed = elapsed;
        Delta = delta;
        UnscaledDelta = unscaledDelta;
        Accumulator = accumulator;
        FixedDelta = fixedDelta;
        TimeScale = timeScale;
    }
}

/// <summary>
/// Render phase context: carries the interpolation alpha.
/// </summary>
public readonly ref struct RenderContext<TState> where TState : struct
{
    /// <summary>Mutable frame state.</summary>
    public readonly ref TState State;

    /// <summary>Frame index.</summary>
    public readonly FrameIndex Frame;

    /// <summary>Clock time.</summary>
    public readonly GameTime Time;

    /// <summary>Accumulated scaled time.</summary>
    public readonly GameTime Elapsed;

    /// <summary>Scaled delta.</summary>
    public readonly GameTime Delta;

    /// <summary>Unscaled delta.</summary>
    public readonly GameTime UnscaledDelta;

    /// <summary>Fixed-step backlog.</summary>
    public readonly GameTime Accumulator;

    /// <summary>Fixed step.</summary>
    public readonly FixedStep FixedDelta;

    /// <summary>Time multiplier.</summary>
    public readonly TimeScale TimeScale;

    /// <summary>Interpolation factor.</summary>
    public readonly InterpolationAlpha Alpha;

    /// <summary>Creates a context.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RenderContext(
        ref TState state,
        FrameIndex frame,
        GameTime time,
        GameTime elapsed,
        GameTime delta,
        GameTime unscaledDelta,
        GameTime accumulator,
        FixedStep fixedDelta,
        TimeScale timeScale,
        InterpolationAlpha alpha)
    {
        State = ref state;
        Frame = frame;
        Time = time;
        Elapsed = elapsed;
        Delta = delta;
        UnscaledDelta = unscaledDelta;
        Accumulator = accumulator;
        FixedDelta = fixedDelta;
        TimeScale = timeScale;
        Alpha = alpha;
    }
}

/// <summary>
/// After-frame phase context: outcome included.
/// </summary>
public readonly ref struct AfterFrameContext<TState> where TState : struct
{
    /// <summary>Mutable frame state.</summary>
    public readonly ref TState State;

    /// <summary>Frame index.</summary>
    public readonly FrameIndex Frame;

    /// <summary>Clock time.</summary>
    public readonly GameTime Time;

    /// <summary>Accumulated scaled time.</summary>
    public readonly GameTime Elapsed;

    /// <summary>Scaled delta.</summary>
    public readonly GameTime Delta;

    /// <summary>Unscaled delta.</summary>
    public readonly GameTime UnscaledDelta;

    /// <summary>Fixed-step backlog.</summary>
    public readonly GameTime Accumulator;

    /// <summary>Fixed step.</summary>
    public readonly FixedStep FixedDelta;

    /// <summary>Time multiplier.</summary>
    public readonly TimeScale TimeScale;

    /// <summary>Interpolation factor.</summary>
    public readonly InterpolationAlpha Alpha;

    /// <summary>Fixed steps executed.</summary>
    public readonly uint FixedStepsExecuted;

    /// <summary>Dropped backlog.</summary>
    public readonly GameTime DroppedAccumulator;

    /// <summary>Creates a context.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AfterFrameContext(
        ref TState state,
        FrameIndex frame,
        GameTime time,
        GameTime elapsed,
        GameTime delta,
        GameTime unscaledDelta,
        GameTime accumulator,
        FixedStep fixedDelta,
        TimeScale timeScale,
        InterpolationAlpha alpha,
        uint fixedStepsExecuted,
        GameTime droppedAccumulator)
    {
        State = ref state;
        Frame = frame;
        Time = time;
        Elapsed = elapsed;
        Delta = delta;
        UnscaledDelta = unscaledDelta;
        Accumulator = accumulator;
        FixedDelta = fixedDelta;
        TimeScale = timeScale;
        Alpha = alpha;
        FixedStepsExecuted = fixedStepsExecuted;
        DroppedAccumulator = droppedAccumulator;
    }
}

/// <summary>
/// System identity contract.
/// </summary>
public interface ISystemMetadata
{
    /// <summary>Identity.</summary>
    SystemId Id { get; }

    /// <summary>Priority; higher runs first.</summary>
    SystemPriority Priority => SystemPriority.Default;

    /// <summary>Whether the system runs.</summary>
    bool IsEnabled => true;

    /// <summary>Releases system resources.</summary>
    void Dispose()
    {
    }
}

/// <summary>BeforeUpdate phase contract.</summary>
public interface IBeforeUpdatePhase<TState> : ISystemMetadata where TState : struct
{
    /// <summary>Runs before fixed steps.</summary>
    void BeforeUpdate(ref readonly BeforeUpdateContext<TState> ctx);
}

/// <summary>FixedUpdate phase contract.</summary>
public interface IFixedUpdatePhase<TState> : ISystemMetadata where TState : struct
{
    /// <summary>Runs per fixed step.</summary>
    void FixedUpdate(ref readonly FixedUpdateContext<TState> ctx);
}

/// <summary>Update phase contract.</summary>
public interface IUpdatePhase<TState> : ISystemMetadata where TState : struct
{
    /// <summary>Runs per variable update.</summary>
    void Update(ref readonly UpdateContext<TState> ctx);
}

/// <summary>Render phase contract.</summary>
public interface IRenderPhase<TState> : ISystemMetadata where TState : struct
{
    /// <summary>Renders one frame.</summary>
    void Render(ref readonly RenderContext<TState> ctx);
}

/// <summary>AfterFrame phase contract.</summary>
public interface IAfterFramePhase<TState> : ISystemMetadata where TState : struct
{
    /// <summary>Runs after render.</summary>
    void AfterFrame(ref readonly AfterFrameContext<TState> ctx);
}

/// <summary>
/// Full system contract with no-op defaults per phase.
/// </summary>
public interface IGameLoopSystem<TState> :
    IBeforeUpdatePhase<TState>,
    IFixedUpdatePhase<TState>,
    IUpdatePhase<TState>,
    IRenderPhase<TState>,
    IAfterFramePhase<TState>
    where TState : struct
{
    /// <inheritdoc/>
    void IBeforeUpdatePhase<TState>.BeforeUpdate(ref readonly BeforeUpdateContext<TState> ctx)
    {
    }

    /// <inheritdoc/>
    void IFixedUpdatePhase<TState>.FixedUpdate(ref readonly FixedUpdateContext<TState> ctx)
    {
    }

    /// <inheritdoc/>
    void IUpdatePhase<TState>.Update(ref readonly UpdateContext<TState> ctx)
    {
    }

    /// <inheritdoc/>
    void IRenderPhase<TState>.Render(ref readonly RenderContext<TState> ctx)
    {
    }

    /// <inheritdoc/>
    void IAfterFramePhase<TState>.AfterFrame(ref readonly AfterFrameContext<TState> ctx)
    {
    }
}
