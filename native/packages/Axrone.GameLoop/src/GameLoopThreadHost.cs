namespace Axrone.GameLoop;

/// <summary>
/// Synchronous state mutation callback.
/// </summary>
/// <typeparam name="TState">Frame state type.</typeparam>
public delegate void StateMutator<TState>(ref TState state) where TState : struct;

/// <summary>
/// Dedicated-thread host: runs the kernel at a target frame rate with
/// sleep-plus-spin pacing and async drain.
/// </summary>
/// <typeparam name="TState">Frame state type.</typeparam>
/// <typeparam name="TClock">Clock policy.</typeparam>
/// <typeparam name="TSpiral">Spiral mitigation policy.</typeparam>
/// <typeparam name="TError">Error recovery policy.</typeparam>
public sealed class GameLoopThreadHost<TState, TClock, TSpiral, TError> : IAsyncDisposable
    where TState : struct
    where TClock : struct, IClockPolicy
    where TSpiral : struct, ISpiralMitigationPolicy
    where TError : struct, IErrorRecoveryPolicy<TState>
{
    private readonly GameLoopKernel<TState, TClock, TSpiral, TError> _kernel;
    private TState _state;
    private Thread? _workerThread;
    private readonly TaskCompletionSource _drainSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _isRunning;
    private readonly double _targetIntervalMs;

    /// <summary>Underlying kernel.</summary>
    public GameLoopKernel<TState, TClock, TSpiral, TError> Kernel => _kernel;

    /// <summary>Current state.</summary>
    public TState State => _state;

    /// <summary>Creates a host.</summary>
    public GameLoopThreadHost(
        GameLoopKernel<TState, TClock, TSpiral, TError> kernel,
        TState initialState,
        double targetFps = 60.0)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        _kernel = kernel;
        _state = initialState;
        _targetIntervalMs = targetFps > 0.0 ? 1000.0 / targetFps : 1000.0 / 60.0;
    }

    /// <summary>Starts the worker thread.</summary>
    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _kernel.TransitionToRunning();

        _workerThread = new Thread(ExecutionLoop)
        {
            Name = "Axrone.GameLoop.Worker",
            IsBackground = true,
            Priority = ThreadPriority.Highest,
        };
        _workerThread.Start();
    }

    /// <summary>Applies a synchronous state mutation.</summary>
    public void MutateState(StateMutator<TState> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        mutator(ref _state);
    }

    private void ExecutionLoop()
    {
        double lastFrameTime = TClock.Now().TotalMilliseconds;

        while (_isRunning && _kernel.Status != GameLoopStatus.Disposed && _kernel.Status != GameLoopStatus.Stopped)
        {
            if (_kernel.Status == GameLoopStatus.Running)
            {
                _kernel.Advance(ref _state);

                double now = TClock.Now().TotalMilliseconds;
                double frameElapsed = now - lastFrameTime;
                double delay = _targetIntervalMs - frameElapsed;

                if (delay > 1.0)
                {
                    int sleepMs = (int)(delay - 0.5);
                    if (sleepMs > 0)
                    {
                        Thread.Sleep(sleepMs);
                    }
                }

                while (TClock.Now().TotalMilliseconds - lastFrameTime < _targetIntervalMs)
                {
                    Thread.SpinWait(12);
                }

                lastFrameTime = TClock.Now().TotalMilliseconds;
            }
            else
            {
                Thread.Sleep(1);
            }
        }

        _drainSignal.TrySetResult();
    }

    /// <summary>Stops and drains the worker.</summary>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        _kernel.TransitionToStopped();
        _isRunning = false;

        await _drainSignal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        if (_workerThread != null && _workerThread.IsAlive)
        {
            _workerThread.Join(500);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DrainAsync(CancellationToken.None).ConfigureAwait(false);
        _kernel.Dispose();
    }
}
