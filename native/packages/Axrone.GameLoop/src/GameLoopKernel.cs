namespace Axrone.GameLoop;

/// <summary>
/// Kernel storage: hot tick line, coordination line, read-mostly config line.
/// Sequential with padding (explicit layout is illegal inside generic types);
/// each group starts on its own cache line.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 192)]
internal struct KernelStorage
{
    public ulong FrameNumber;
    public double ElapsedMilliseconds;
    public double AccumulatorMilliseconds;
    public double LastTimestampMilliseconds;
    public double FixedStepMilliseconds;
    public double MaxDeltaMilliseconds;
    public double TimeScaleMultiplier;
    public uint MaxSubStepsAllowed;
    private readonly uint _pad0;

    public int AtomicStatus;
    public int StopRequested;
    public int PauseRequested;
    public int WorkerActive;
    public long CapturedAtEpochMs;
    private readonly long _padC0;
    private readonly long _padC1;
    private readonly long _padC2;
    private readonly long _padC3;
    private readonly long _padC4;

    public uint VersionCounter;
    public int AutoResetAccumulatorOnStop;
    private readonly long _padR0;
    private readonly long _padR1;
    private readonly long _padR2;
    private readonly long _padR3;
    private readonly long _padR4;
    private readonly long _padR5;
    private readonly long _padR6;
}

/// <summary>
/// Outcome of one advanced frame.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct FrameExecutionOutcome(
    FrameOutcomeKind Kind,
    FrameIndex Frame,
    GameTime Delta,
    uint FixedStepsExecuted,
    GameTime DroppedAccumulator)
{
    /// <summary>Creates an executed outcome.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrameExecutionOutcome Executed(FrameIndex frame, GameTime delta, uint fixedSteps) =>
        new(FrameOutcomeKind.Executed, frame, delta, fixedSteps, GameTime.Zero);

    /// <summary>Creates a mitigated outcome.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrameExecutionOutcome Mitigated(FrameIndex frame, GameTime delta, uint fixedSteps, GameTime dropped) =>
        new(FrameOutcomeKind.SpiralMitigated, frame, delta, fixedSteps, dropped);

    /// <summary>Creates a throttled outcome.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrameExecutionOutcome Throttled(FrameIndex frame) =>
        new(FrameOutcomeKind.ThrottledZeroDelta, frame, GameTime.Zero, 0U, GameTime.Zero);

    /// <summary>Creates a halted outcome.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrameExecutionOutcome Halted(FrameIndex frame) =>
        new(FrameOutcomeKind.Halted, frame, GameTime.Zero, 0U, GameTime.Zero);
}

/// <summary>
/// Fixed-step simulation kernel: owns timing state, runs the five phases,
/// and coordinates lifecycle transitions and snapshots.
/// </summary>
/// <typeparam name="TState">Frame state type.</typeparam>
/// <typeparam name="TClock">Clock policy.</typeparam>
/// <typeparam name="TSpiral">Spiral mitigation policy.</typeparam>
/// <typeparam name="TError">Error recovery policy.</typeparam>
public sealed class GameLoopKernel<TState, TClock, TSpiral, TError> : IDisposable
    where TState : struct
    where TClock : struct, IClockPolicy
    where TSpiral : struct, ISpiralMitigationPolicy
    where TError : struct, IErrorRecoveryPolicy<TState>
{
    private KernelStorage _storage;
    private readonly SystemRegistry<TState> _registry;
    private readonly Lock _transitionGate = new();

    /// <summary>System registry.</summary>
    public SystemRegistry<TState> Registry => _registry;

    /// <summary>Current status.</summary>
    public GameLoopStatus Status
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (GameLoopStatus)Volatile.Read(ref _storage.AtomicStatus);
    }

    /// <summary>Frame counter.</summary>
    public FrameIndex Frame
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Volatile.Read(ref _storage.FrameNumber));
    }

    /// <summary>Elapsed scaled time.</summary>
    public GameTime Elapsed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GameTime.FromMilliseconds(Volatile.Read(ref _storage.ElapsedMilliseconds));
    }

    /// <summary>Fixed step.</summary>
    public FixedStep FixedStep
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Volatile.Read(ref _storage.FixedStepMilliseconds));
        set => Volatile.Write(ref _storage.FixedStepMilliseconds, value.TotalMilliseconds);
    }

    /// <summary>Delta clamp.</summary>
    public GameTime MaxDelta
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GameTime.FromMilliseconds(Volatile.Read(ref _storage.MaxDeltaMilliseconds));
        set => Volatile.Write(ref _storage.MaxDeltaMilliseconds, value.TotalMilliseconds);
    }

    /// <summary>Max steps per frame.</summary>
    public uint MaxSubSteps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _storage.MaxSubStepsAllowed);
        set
        {
            if (value == 0)
            {
                GameLoopThrowHelper.ThrowInvalidMaxSubSteps(value);
            }

            Volatile.Write(ref _storage.MaxSubStepsAllowed, value);
        }
    }

    /// <summary>Time multiplier.</summary>
    public TimeScale TimeScale
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Volatile.Read(ref _storage.TimeScaleMultiplier));
        set => Volatile.Write(ref _storage.TimeScaleMultiplier, value.Value);
    }

    /// <summary>Creates a kernel.</summary>
    public GameLoopKernel(
        FixedStep fixedStep,
        GameTime maxDelta,
        uint maxSubSteps,
        TimeScale timeScale,
        int systemCapacity = 16)
    {
        _storage = default;
        _storage.FixedStepMilliseconds = fixedStep.TotalMilliseconds;
        _storage.MaxDeltaMilliseconds = maxDelta.TotalMilliseconds;
        _storage.MaxSubStepsAllowed = maxSubSteps;
        _storage.TimeScaleMultiplier = timeScale.Value;
        _storage.LastTimestampMilliseconds = -1.0;
        _storage.AtomicStatus = (int)GameLoopStatus.Idle;
        _storage.VersionCounter = 1U;
        _storage.AutoResetAccumulatorOnStop = 1;

        _registry = new SystemRegistry<TState>(systemCapacity);
    }

    /// <summary>Advances one frame through all five phases.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public FrameExecutionOutcome Advance(ref TState state, GameTime? timestamp = null)
    {
        if (Status != GameLoopStatus.Running)
        {
            return FrameExecutionOutcome.Halted(Frame);
        }

        double nowMs = timestamp.HasValue ? timestamp.Value.TotalMilliseconds : TClock.Now().TotalMilliseconds;
        double lastMs = _storage.LastTimestampMilliseconds;

        if (lastMs < 0.0)
        {
            lastMs = nowMs;
        }

        double rawDeltaMs = Math.Max(0.0, nowMs - lastMs);
        _storage.LastTimestampMilliseconds = nowMs;

        if (rawDeltaMs <= 0.0)
        {
            return FrameExecutionOutcome.Throttled(Frame);
        }

        double clampedDeltaMs = Math.Min(rawDeltaMs, _storage.MaxDeltaMilliseconds);
        double timeScale = _storage.TimeScaleMultiplier;
        double scaledDeltaMs = clampedDeltaMs * timeScale;

        _storage.FrameNumber++;
        _storage.ElapsedMilliseconds += scaledDeltaMs;
        _storage.AccumulatorMilliseconds += scaledDeltaMs;

        FrameIndex frame = new(_storage.FrameNumber);
        GameTime nowTime = GameTime.FromMilliseconds(nowMs);
        GameTime elapsedTime = GameTime.FromMilliseconds(_storage.ElapsedMilliseconds);
        GameTime delta = GameTime.FromMilliseconds(scaledDeltaMs);
        GameTime unscaledDelta = GameTime.FromMilliseconds(clampedDeltaMs);
        FixedStep fixedDelta = new(_storage.FixedStepMilliseconds);
        TimeScale scale = new(timeScale);

        BeforeUpdateContext<TState> beforeCtx = new(
            ref state,
            frame,
            nowTime,
            elapsedTime,
            delta,
            unscaledDelta,
            GameTime.FromMilliseconds(_storage.AccumulatorMilliseconds),
            fixedDelta,
            scale);
        _registry.DispatchBeforeUpdate<TError>(in beforeCtx, ref state);

        TSpiral.Mitigate(
            ref _storage.AccumulatorMilliseconds,
            _storage.FixedStepMilliseconds,
            _storage.MaxSubStepsAllowed,
            out uint stepsToRun,
            out double droppedAccumulatorMs);

        FixedStep stepDelta = fixedDelta;
        GameTime unscaledStepDelta = timeScale > 0.0
            ? GameTime.FromMilliseconds(_storage.FixedStepMilliseconds / timeScale)
            : GameTime.FromMilliseconds(_storage.FixedStepMilliseconds);

        for (uint step = 1; step <= stepsToRun; step++)
        {
            FixedUpdateContext<TState> fixedCtx = new(
                ref state,
                frame,
                nowTime,
                elapsedTime,
                stepDelta,
                unscaledStepDelta,
                GameTime.FromMilliseconds(_storage.AccumulatorMilliseconds),
                fixedDelta,
                scale,
                step,
                stepsToRun);
            _registry.DispatchFixedUpdate<TError>(in fixedCtx, ref state);
        }

        UpdateContext<TState> updateCtx = new(
            ref state,
            frame,
            nowTime,
            elapsedTime,
            delta,
            unscaledDelta,
            GameTime.FromMilliseconds(_storage.AccumulatorMilliseconds),
            fixedDelta,
            scale);
        _registry.DispatchUpdate<TError>(in updateCtx, ref state);

        double alphaValue = _storage.FixedStepMilliseconds > 0.0
            ? Math.Clamp(_storage.AccumulatorMilliseconds / _storage.FixedStepMilliseconds, 0.0, 1.0)
            : 0.0;
        InterpolationAlpha alpha = new(alphaValue);

        RenderContext<TState> renderCtx = new(
            ref state,
            frame,
            nowTime,
            elapsedTime,
            delta,
            unscaledDelta,
            GameTime.FromMilliseconds(_storage.AccumulatorMilliseconds),
            fixedDelta,
            scale,
            alpha);
        _registry.DispatchRender<TError>(in renderCtx, ref state);

        AfterFrameContext<TState> afterCtx = new(
            ref state,
            frame,
            nowTime,
            elapsedTime,
            delta,
            unscaledDelta,
            GameTime.FromMilliseconds(_storage.AccumulatorMilliseconds),
            fixedDelta,
            scale,
            alpha,
            stepsToRun,
            GameTime.FromMilliseconds(droppedAccumulatorMs));
        _registry.DispatchAfterFrame<TError>(in afterCtx, ref state);

        return droppedAccumulatorMs > 0.0
            ? FrameExecutionOutcome.Mitigated(frame, delta, stepsToRun, GameTime.FromMilliseconds(droppedAccumulatorMs))
            : FrameExecutionOutcome.Executed(frame, delta, stepsToRun);
    }

    /// <summary>Transitions to running.</summary>
    public void TransitionToRunning(GameTime? timestamp = null)
    {
        lock (_transitionGate)
        {
            GameLoopStatus current = Status;
            if (current == GameLoopStatus.Disposed)
            {
                GameLoopThrowHelper.ThrowDisposed(nameof(GameLoopKernel<TState, TClock, TSpiral, TError>));
            }

            if (current == GameLoopStatus.Running)
            {
                return;
            }

            _storage.LastTimestampMilliseconds = timestamp.HasValue
                ? timestamp.Value.TotalMilliseconds
                : TClock.Now().TotalMilliseconds;

            Volatile.Write(ref _storage.AtomicStatus, (int)GameLoopStatus.Running);
            Volatile.Write(ref _storage.StopRequested, 0);
            Volatile.Write(ref _storage.PauseRequested, 0);
        }
    }

    /// <summary>Transitions to paused.</summary>
    public void TransitionToPaused()
    {
        lock (_transitionGate)
        {
            GameLoopStatus current = Status;
            if (current == GameLoopStatus.Disposed)
            {
                GameLoopThrowHelper.ThrowDisposed(nameof(GameLoopKernel<TState, TClock, TSpiral, TError>));
            }

            if (current != GameLoopStatus.Running)
            {
                return;
            }

            Volatile.Write(ref _storage.AtomicStatus, (int)GameLoopStatus.Paused);
            Volatile.Write(ref _storage.PauseRequested, 1);
        }
    }

    /// <summary>Transitions to stopped.</summary>
    public void TransitionToStopped()
    {
        lock (_transitionGate)
        {
            GameLoopStatus current = Status;
            if (current == GameLoopStatus.Disposed)
            {
                GameLoopThrowHelper.ThrowDisposed(nameof(GameLoopKernel<TState, TClock, TSpiral, TError>));
            }

            if (current is GameLoopStatus.Stopped or GameLoopStatus.Idle)
            {
                return;
            }

            Volatile.Write(ref _storage.AtomicStatus, (int)GameLoopStatus.Stopped);
            Volatile.Write(ref _storage.StopRequested, 1);

            if (_storage.AutoResetAccumulatorOnStop != 0)
            {
                Volatile.Write(ref _storage.AccumulatorMilliseconds, 0.0);
            }
        }
    }

    /// <summary>Transitions to disposed.</summary>
    public void TransitionToDisposed()
    {
        lock (_transitionGate)
        {
            if ((GameLoopStatus)Interlocked.Exchange(ref _storage.AtomicStatus, (int)GameLoopStatus.Disposed) == GameLoopStatus.Disposed)
            {
                return;
            }

            Volatile.Write(ref _storage.StopRequested, 1);
            _registry.Clear();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        TransitionToDisposed();
        _registry.Dispose();
    }
}
