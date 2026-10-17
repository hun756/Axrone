namespace Axrone.GameLoop;

/// <summary>
/// Builder typestate marker: initial stage.
/// </summary>
public readonly struct StageIdle;

/// <summary>
/// Builder typestate marker: running stage.
/// </summary>
public readonly struct StageRunning;

/// <summary>
/// Builder typestate marker: paused stage.
/// </summary>
public readonly struct StagePaused;

/// <summary>
/// Builder typestate marker: stopped stage.
/// </summary>
public readonly struct StageStopped;

/// <summary>
/// Builder typestate marker: disposed stage.
/// </summary>
public readonly struct StageDisposed;

/// <summary>
/// Compile-time typestate pipeline over a kernel. Transitions consume the
/// current stage and produce the next one.
/// </summary>
public readonly struct GameLoopPipeline<TState, TClock, TSpiral, TError, TStage>
    where TState : struct
    where TClock : struct, IClockPolicy
    where TSpiral : struct, ISpiralMitigationPolicy
    where TError : struct, IErrorRecoveryPolicy<TState>
{
    private readonly GameLoopKernel<TState, TClock, TSpiral, TError> _kernel;

    /// <summary>Creates a pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal GameLoopPipeline(GameLoopKernel<TState, TClock, TSpiral, TError> kernel) => _kernel = kernel;

    /// <summary>Underlying kernel.</summary>
    public GameLoopKernel<TState, TClock, TSpiral, TError> Kernel => _kernel;

    /// <summary>Current status.</summary>
    public GameLoopStatus Status => _kernel.Status;

    /// <summary>Frame counter.</summary>
    public FrameIndex Frame => _kernel.Frame;

    /// <summary>Elapsed scaled time.</summary>
    public GameTime Elapsed => _kernel.Elapsed;

    /// <summary>Disposes the kernel.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GameLoopPipeline<TState, TClock, TSpiral, TError, StageDisposed> Dispose()
    {
        _kernel.Dispose();
        return new GameLoopPipeline<TState, TClock, TSpiral, TError, StageDisposed>(_kernel);
    }
}

/// <summary>
/// Stage transitions for the pipeline.
/// </summary>
public static class GameLoopPipelineExtensions
{
    /// <summary>Starts an idle pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameLoopPipeline<TState, TClock, TSpiral, TError, StageRunning> Start<TState, TClock, TSpiral, TError>(
        this GameLoopPipeline<TState, TClock, TSpiral, TError, StageIdle> pipeline,
        GameTime? timestamp = null)
        where TState : struct
        where TClock : struct, IClockPolicy
        where TSpiral : struct, ISpiralMitigationPolicy
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        pipeline.Kernel.TransitionToRunning(timestamp);
        return new GameLoopPipeline<TState, TClock, TSpiral, TError, StageRunning>(pipeline.Kernel);
    }

    /// <summary>Resumes a paused pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameLoopPipeline<TState, TClock, TSpiral, TError, StageRunning> Resume<TState, TClock, TSpiral, TError>(
        this GameLoopPipeline<TState, TClock, TSpiral, TError, StagePaused> pipeline,
        GameTime? timestamp = null)
        where TState : struct
        where TClock : struct, IClockPolicy
        where TSpiral : struct, ISpiralMitigationPolicy
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        pipeline.Kernel.TransitionToRunning(timestamp);
        return new GameLoopPipeline<TState, TClock, TSpiral, TError, StageRunning>(pipeline.Kernel);
    }

    /// <summary>Restarts a stopped pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameLoopPipeline<TState, TClock, TSpiral, TError, StageRunning> Restart<TState, TClock, TSpiral, TError>(
        this GameLoopPipeline<TState, TClock, TSpiral, TError, StageStopped> pipeline,
        GameTime? timestamp = null)
        where TState : struct
        where TClock : struct, IClockPolicy
        where TSpiral : struct, ISpiralMitigationPolicy
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        pipeline.Kernel.TransitionToRunning(timestamp);
        return new GameLoopPipeline<TState, TClock, TSpiral, TError, StageRunning>(pipeline.Kernel);
    }

    /// <summary>Pauses a running pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameLoopPipeline<TState, TClock, TSpiral, TError, StagePaused> Pause<TState, TClock, TSpiral, TError>(
        this GameLoopPipeline<TState, TClock, TSpiral, TError, StageRunning> pipeline)
        where TState : struct
        where TClock : struct, IClockPolicy
        where TSpiral : struct, ISpiralMitigationPolicy
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        pipeline.Kernel.TransitionToPaused();
        return new GameLoopPipeline<TState, TClock, TSpiral, TError, StagePaused>(pipeline.Kernel);
    }

    /// <summary>Stops a running pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameLoopPipeline<TState, TClock, TSpiral, TError, StageStopped> Stop<TState, TClock, TSpiral, TError>(
        this GameLoopPipeline<TState, TClock, TSpiral, TError, StageRunning> pipeline)
        where TState : struct
        where TClock : struct, IClockPolicy
        where TSpiral : struct, ISpiralMitigationPolicy
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        pipeline.Kernel.TransitionToStopped();
        return new GameLoopPipeline<TState, TClock, TSpiral, TError, StageStopped>(pipeline.Kernel);
    }

    /// <summary>Advances a running pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static FrameExecutionOutcome Advance<TState, TClock, TSpiral, TError>(
        this GameLoopPipeline<TState, TClock, TSpiral, TError, StageRunning> pipeline,
        ref TState state,
        GameTime? timestamp = null)
        where TState : struct
        where TClock : struct, IClockPolicy
        where TSpiral : struct, ISpiralMitigationPolicy
        where TError : struct, IErrorRecoveryPolicy<TState> =>
        pipeline.Kernel.Advance(ref state, timestamp);
}

/// <summary>
/// Fluent builder with validated properties.
/// </summary>
public sealed class GameLoopBuilder<TState, TClock, TSpiral, TError>
    where TState : struct
    where TClock : struct, IClockPolicy
    where TSpiral : struct, ISpiralMitigationPolicy
    where TError : struct, IErrorRecoveryPolicy<TState>
{
    /// <summary>Fixed step.</summary>
    public FixedStep FixedDelta
    {
        get;
        set
        {
            if (value.TotalMilliseconds <= 0.0)
            {
                GameLoopThrowHelper.ThrowInvalidFixedStep(value.TotalMilliseconds);
            }

            field = value;
        }
    } = FixedStep.SixtyFps;

    /// <summary>Delta clamp.</summary>
    public GameTime MaxDelta
    {
        get;
        set
        {
            if (value.TotalMilliseconds <= 0.0)
            {
                GameLoopThrowHelper.ThrowInvalidMaxDelta(value.TotalMilliseconds);
            }

            field = value;
        }
    } = GameTime.FromMilliseconds(250.0);

    /// <summary>Max steps per frame.</summary>
    public uint MaxSubSteps
    {
        get;
        set
        {
            if (value == 0)
            {
                GameLoopThrowHelper.ThrowInvalidMaxSubSteps(value);
            }

            field = value;
        }
    } = 8U;

    /// <summary>Time multiplier.</summary>
    public TimeScale TimeScale
    {
        get;
        set
        {
            if (value.Value < 0.0)
            {
                GameLoopThrowHelper.ThrowInvalidTimeScale(value.Value);
            }

            field = value;
        }
    } = TimeScale.Identity;

    /// <summary>System capacity.</summary>
    public int InitialCapacity
    {
        get;
        set
        {
            if (value <= 0)
            {
                GameLoopThrowHelper.ThrowInvalidCapacity(value);
            }

            field = value;
        }
    } = 16;

    /// <summary>Sets the fixed step.</summary>
    public GameLoopBuilder<TState, TClock, TSpiral, TError> WithFixedStep(FixedStep step)
    {
        FixedDelta = step;
        return this;
    }

    /// <summary>Sets the delta clamp.</summary>
    public GameLoopBuilder<TState, TClock, TSpiral, TError> WithMaxDelta(GameTime maxDelta)
    {
        MaxDelta = maxDelta;
        return this;
    }

    /// <summary>Sets max steps per frame.</summary>
    public GameLoopBuilder<TState, TClock, TSpiral, TError> WithMaxSubSteps(uint steps)
    {
        MaxSubSteps = steps;
        return this;
    }

    /// <summary>Sets the time multiplier.</summary>
    public GameLoopBuilder<TState, TClock, TSpiral, TError> WithTimeScale(TimeScale scale)
    {
        TimeScale = scale;
        return this;
    }

    /// <summary>Sets system capacity.</summary>
    public GameLoopBuilder<TState, TClock, TSpiral, TError> WithCapacity(int capacity)
    {
        InitialCapacity = capacity;
        return this;
    }

    /// <summary>Builds an idle pipeline.</summary>
    public GameLoopPipeline<TState, TClock, TSpiral, TError, StageIdle> BuildPipeline()
    {
        GameLoopKernel<TState, TClock, TSpiral, TError> kernel = new(
            FixedDelta,
            MaxDelta,
            MaxSubSteps,
            TimeScale,
            InitialCapacity);
        return new GameLoopPipeline<TState, TClock, TSpiral, TError, StageIdle>(kernel);
    }

    /// <summary>Builds a thread host.</summary>
    public GameLoopThreadHost<TState, TClock, TSpiral, TError> BuildHost(TState initialState, double targetFps = 60.0)
    {
        GameLoopKernel<TState, TClock, TSpiral, TError> kernel = new(
            FixedDelta,
            MaxDelta,
            MaxSubSteps,
            TimeScale,
            InitialCapacity);
        return new GameLoopThreadHost<TState, TClock, TSpiral, TError>(kernel, initialState, targetFps);
    }
}

/// <summary>
/// Entry point with curated defaults.
/// </summary>
public static class GameLoopFactory
{
    /// <summary>Creates a builder with default policies.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameLoopBuilder<TState, HighResolutionStopwatchClock, ModuloSpiralMitigation, FailFastErrorPolicy<TState>>
        CreateBuilder<TState>()
        where TState : struct =>
        new GameLoopBuilder<TState, HighResolutionStopwatchClock, ModuloSpiralMitigation, FailFastErrorPolicy<TState>>();

    /// <summary>Creates a builder with custom policies.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameLoopBuilder<TState, TClock, TSpiral, TError>
        CreateCustomBuilder<TState, TClock, TSpiral, TError>()
        where TState : struct
        where TClock : struct, IClockPolicy
        where TSpiral : struct, ISpiralMitigationPolicy
        where TError : struct, IErrorRecoveryPolicy<TState> =>
        new GameLoopBuilder<TState, TClock, TSpiral, TError>();
}
