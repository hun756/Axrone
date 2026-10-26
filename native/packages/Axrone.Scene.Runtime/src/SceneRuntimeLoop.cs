namespace Axrone.Scene.Runtime;

using Axrone.GameLoop;

/// <summary>
/// Fixed-step loop host for a scene runtime. Owns the kernel, registers the
/// scene system and exposes explicit start/pause/stop transitions and ticking
/// so hosts can drive the loop from their own platform callback.
/// </summary>
public sealed class SceneRuntimeLoop : IDisposable
{
    private readonly GameLoopKernel<
        SceneRuntimeFrameState,
        HighResolutionStopwatchClock,
        ModuloSpiralMitigation,
        FailFastErrorPolicy<SceneRuntimeFrameState>> _kernel;

    private SceneRuntimeFrameState _state;
    private bool _disposed;

    /// <summary>Creates a loop over a runtime with an optional GL frame renderer.</summary>
    public SceneRuntimeLoop(
        SceneRuntime runtime,
        SceneGlFrameRenderer? renderer = null,
        SceneRuntimeLoopOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        options ??= new SceneRuntimeLoopOptions();

        _kernel = new GameLoopKernel<
            SceneRuntimeFrameState,
            HighResolutionStopwatchClock,
            ModuloSpiralMitigation,
            FailFastErrorPolicy<SceneRuntimeFrameState>>(
            options.FixedDelta,
            options.MaxDelta,
            options.MaxSubSteps,
            options.TimeScale,
            options.InitialCapacity);

        _kernel.Registry.Register(new SceneRuntimeSystem(runtime, renderer));
        _state = new SceneRuntimeFrameState(default, default, CameraAvailable: false);
    }

    /// <summary>Underlying game-loop kernel.</summary>
    public GameLoopKernel<
        SceneRuntimeFrameState,
        HighResolutionStopwatchClock,
        ModuloSpiralMitigation,
        FailFastErrorPolicy<SceneRuntimeFrameState>> Kernel => _kernel;

    /// <summary>Current frame state.</summary>
    public SceneRuntimeFrameState State => _state;

    /// <summary>Current loop status.</summary>
    public GameLoopStatus Status => _kernel.Status;

    /// <summary>Frames advanced so far.</summary>
    public FrameIndex Frame => _kernel.Frame;

    /// <summary>Starts the loop.</summary>
    public void Start(GameTime? timestamp = null)
    {
        ThrowIfDisposed();
        _kernel.TransitionToRunning(timestamp);
    }

    /// <summary>Pauses the loop.</summary>
    public void Pause()
    {
        ThrowIfDisposed();
        _kernel.TransitionToPaused();
    }

    /// <summary>Stops the loop.</summary>
    public void Stop()
    {
        ThrowIfDisposed();
        _kernel.TransitionToStopped();
    }

    /// <summary>Advances one frame.</summary>
    public FrameExecutionOutcome Tick(GameTime? timestamp = null)
    {
        ThrowIfDisposed();
        return _kernel.Advance(ref _state, timestamp);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _kernel.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new SceneDisposedException(nameof(SceneRuntimeLoop));
        }
    }
}
