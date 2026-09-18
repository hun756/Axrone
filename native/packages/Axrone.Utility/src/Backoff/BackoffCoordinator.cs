using System.Runtime.ExceptionServices;

namespace Axrone.Utility.Backoff;

[StructLayout(LayoutKind.Explicit, Size = 256)]
internal struct AlignedLockFreeBackoffState
{
    [FieldOffset(0)]
    public ulong TotalExecutedSteps;

    [FieldOffset(8)]
    public ulong SpinPhaseSteps;

    [FieldOffset(16)]
    public ulong YieldPhaseSteps;

    [FieldOffset(24)]
    public ulong SleepPhaseSteps;

    [FieldOffset(32)]
    public ulong AsyncPhaseSteps;

    [FieldOffset(64)]
    public long ActiveWorkerContention;

    [FieldOffset(128)]
    public volatile int LifecycleStatus;

    [FieldOffset(136)]
    public ExceptionDispatchInfo? FaultDispatch;
}

public sealed class BackoffCoordinator<TPolicy> :
    ISynchronousBackoff,
    IAsynchronousBackoff,
    IBatchBackoffProcessor,
    IBackoffTelemetryEndpoint,
    IBackoffLifecycle
    where TPolicy : struct, IBackoffPolicy<TPolicy>
{
    private const int StatusActive = 0;
    private const int StatusDraining = 1;
    private const int StatusTerminated = 2;
    private const int StatusFaulted = 3;

    private readonly BackoffConfiguration _configuration;
    private readonly BackoffInstrumentation _instrumentation;
    private readonly Lock _barrierLock = new();
    private readonly TaskCompletionSource _drainCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private AlignedLockFreeBackoffState _state;

    [ThreadStatic]
    private static ulong t_threadRngSeed;

    [ThreadStatic]
    private static ResettableAsyncDelayNode? t_asyncDelayNode;

    [ThreadStatic]
    private static HybridAsyncDelayNode? t_hybridDelayNode;

    public BackoffCoordinator(in BackoffConfiguration configuration, string componentName = "BackoffCoordinator")
    {
        _configuration = configuration;
        _instrumentation = new BackoffInstrumentation(componentName);
        _state = default;
    }

    public BackoffCoordinator(in BackoffConfiguration configuration, IBackoffTelemetrySink customSink, string componentName = "BackoffCoordinator")
    {
        _configuration = configuration;
        _instrumentation = new BackoffInstrumentation(componentName, customSink);
        _state = default;
    }

    public BackoffConfiguration Configuration => _configuration;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Step()
    {
        EnsureOperable();

        ref ulong rng = ref GetThreadRng();
        _instrumentation.IncrementContention();
        Interlocked.Increment(ref _state.ActiveWorkerContention);

        try
        {
            uint step = (uint)Interlocked.Increment(ref _state.TotalExecutedSteps);
            if (step > _configuration.MaxRetryLimit)
            {
                ThrowHelper.ThrowInvalidOperationException("Exceeded maximum configured retry limit.");
            }

            BackoffKind kind = TPolicy.ClassifyKind(step, in _configuration);
            BackoffDuration duration = TPolicy.ComputeDuration(step, in _configuration, ref rng);

            ExecuteSynchronousPause(kind, duration, step);
            _instrumentation.RecordExecution(kind, duration.Nanoseconds / (double)BackoffDuration.NanosecondsPerMillisecond, duration.Nanoseconds);
            BackoffDiagnosticsEventSource.Log.StepFired(step, (int)kind, duration.Nanoseconds);
        }
        finally
        {
            Interlocked.Decrement(ref _state.ActiveWorkerContention);
            _instrumentation.DecrementContention();
            EvaluateDrainCompletion();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BackoffStepResult ComputeStep()
    {
        EnsureOperable();

        ref ulong rng = ref GetThreadRng();
        uint nextStep = (uint)Volatile.Read(ref _state.TotalExecutedSteps) + 1U;

        if (nextStep > _configuration.MaxRetryLimit)
        {
            ThrowHelper.ThrowInvalidOperationException("Exceeded maximum configured retry limit.");
        }

        BackoffKind kind = TPolicy.ClassifyKind(nextStep, in _configuration);
        BackoffDuration duration = TPolicy.ComputeDuration(nextStep, in _configuration, ref rng);

        return new BackoffStepResult(nextStep, kind, duration);
    }

    public bool WaitFor(Func<bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        EnsureOperable();

        long frequency = Stopwatch.Frequency;
        long startTimestamp = Stopwatch.GetTimestamp();
        long deadlineTimestamp = startTimestamp + (long)(timeout.TotalSeconds * frequency);

        while (!predicate())
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (Stopwatch.GetTimestamp() >= deadlineTimestamp) return false;
            if (!TryStep(cancellationToken)) return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryStep(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested || _state.LifecycleStatus != StatusActive)
        {
            return false;
        }

        Step();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Interlocked.Exchange(ref _state.TotalExecutedSteps, 0UL);
        Interlocked.Exchange(ref _state.SpinPhaseSteps, 0UL);
        Interlocked.Exchange(ref _state.YieldPhaseSteps, 0UL);
        Interlocked.Exchange(ref _state.SleepPhaseSteps, 0UL);
        Interlocked.Exchange(ref _state.AsyncPhaseSteps, 0UL);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async ValueTask<BackoffDuration> StepAsync(CancellationToken cancellationToken = default)
    {
        EnsureOperable();
        cancellationToken.ThrowIfCancellationRequested();

        ref ulong rng = ref GetThreadRng();
        _instrumentation.IncrementContention();
        Interlocked.Increment(ref _state.ActiveWorkerContention);

        try
        {
            uint step = (uint)Interlocked.Increment(ref _state.TotalExecutedSteps);
            if (step > _configuration.MaxRetryLimit)
            {
                ThrowHelper.ThrowInvalidOperationException("Exceeded maximum configured retry limit.");
            }

            Interlocked.Increment(ref _state.AsyncPhaseSteps);
            BackoffDuration duration = TPolicy.ComputeDuration(step, in _configuration, ref rng);

            if (duration.Nanoseconds > 0L)
            {
                if (duration.Nanoseconds <= 5_000_000L)
                {
                    HybridAsyncDelayNode hybridNode = t_hybridDelayNode ??= new HybridAsyncDelayNode();
                    await hybridNode.StartDelay(duration, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ResettableAsyncDelayNode node = t_asyncDelayNode ??= new ResettableAsyncDelayNode();
                    await node.StartDelay(duration, cancellationToken).ConfigureAwait(false);
                }
            }

            _instrumentation.RecordExecution(BackoffKind.AsyncDelay, duration.Nanoseconds / (double)BackoffDuration.NanosecondsPerMillisecond, duration.Nanoseconds);
            BackoffDiagnosticsEventSource.Log.StepFired(step, (int)BackoffKind.AsyncDelay, duration.Nanoseconds);

            return duration;
        }
        finally
        {
            Interlocked.Decrement(ref _state.ActiveWorkerContention);
            _instrumentation.DecrementContention();
            EvaluateDrainCompletion();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ComputeBatch(ReadOnlySpan<uint> steps, Span<BackoffDuration> outputs, in BackoffConfiguration config)
    {
        ref ulong rng = ref GetThreadRng();
        VectorizedBatchEngine.ComputeBatch(steps, outputs, in config, ref rng);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteSynchronousPause(BackoffKind kind, BackoffDuration duration, uint step)
    {
        switch (kind)
        {
            case BackoffKind.Spin:
                Interlocked.Increment(ref _state.SpinPhaseSteps);
                uint pauseCycles = 1U << (int)Math.Min(step, 10U);
                MicroPause.Execute(pauseCycles);
                break;

            case BackoffKind.Yield:
                Interlocked.Increment(ref _state.YieldPhaseSteps);
                Thread.Yield();
                break;

            case BackoffKind.Sleep:
            case BackoffKind.AsyncDelay:
                Interlocked.Increment(ref _state.SleepPhaseSteps);
                long ms = duration.Nanoseconds / BackoffDuration.NanosecondsPerMillisecond;
                if (ms == 0L && duration.Nanoseconds > 0L)
                {
                    ms = 1L;
                }
                Thread.Sleep((int)Math.Min(ms, int.MaxValue));
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref ulong GetThreadRng()
    {
        if (t_threadRngSeed == 0UL)
        {
            ulong seed = (ulong)Environment.TickCount64 ^ (ulong)Environment.CurrentManagedThreadId;
            t_threadRngSeed = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
        }
        return ref t_threadRngSeed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureOperable()
    {
        if (_state.LifecycleStatus != StatusActive)
        {
            ThrowAbnormalLifecycleState();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowAbnormalLifecycleState()
    {
        if (_state.LifecycleStatus == StatusFaulted)
        {
            _state.FaultDispatch?.Throw();
            ThrowHelper.ThrowInvalidOperationException("Backoff Coordinator faulted.");
        }

        if (_state.LifecycleStatus >= StatusDraining)
        {
            ThrowHelper.ThrowInvalidOperationException("Backoff Coordinator is draining or terminated.");
        }
    }

    public bool IsHealthy => _state.LifecycleStatus == StatusActive;

    public BackoffHealthStatus QueryHealthStatus()
    {
        int status = _state.LifecycleStatus;
        return status switch
        {
            StatusActive => BackoffHealthStatus.Healthy,
            StatusDraining => BackoffHealthStatus.Degraded,
            _ => BackoffHealthStatus.Unhealthy
        };
    }

    public BackoffMetricsSnapshot TakeSnapshot()
    {
        int status = _state.LifecycleStatus;
        long activeOps = Volatile.Read(ref _state.ActiveWorkerContention);
        bool isDrained = (status == StatusTerminated) || (status == StatusFaulted && activeOps == 0L);

        return new BackoffMetricsSnapshot(
            TotalExecutedSteps: Volatile.Read(ref _state.TotalExecutedSteps),
            SpinPhaseSteps: Volatile.Read(ref _state.SpinPhaseSteps),
            YieldPhaseSteps: Volatile.Read(ref _state.YieldPhaseSteps),
            SleepPhaseSteps: Volatile.Read(ref _state.SleepPhaseSteps),
            AsyncPhaseSteps: Volatile.Read(ref _state.AsyncPhaseSteps),
            ActiveWorkerContention: activeOps,
            IsFaulted: status == StatusFaulted,
            IsDrained: isDrained);
    }

    public void Complete(Exception? fault = null)
    {
        lock (_barrierLock)
        {
            if (_state.LifecycleStatus >= StatusDraining) return;

            int oldState = _state.LifecycleStatus;
            if (fault != null)
            {
                _state.FaultDispatch = ExceptionDispatchInfo.Capture(fault);
                _state.LifecycleStatus = StatusFaulted;
                BackoffDiagnosticsEventSource.Log.FatalFaultReported(fault.Message);
            }
            else
            {
                _state.LifecycleStatus = StatusDraining;
            }

            BackoffDiagnosticsEventSource.Log.StateTransition(oldState, _state.LifecycleStatus);
        }

        EvaluateDrainCompletion();
    }

    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        Complete();

        if (TakeSnapshot().IsDrained) return;

        using var reg = cancellationToken.UnsafeRegister(static state =>
        {
            var tcs = (TaskCompletionSource)state!;
            tcs.TrySetCanceled();
        }, _drainCompletion);

        await _drainCompletion.Task.ConfigureAwait(false);
    }

    private void EvaluateDrainCompletion()
    {
        if (Volatile.Read(ref _state.ActiveWorkerContention) == 0L && _state.LifecycleStatus is StatusDraining or StatusFaulted)
        {
            lock (_barrierLock)
            {
                if (_state.LifecycleStatus is StatusDraining or StatusFaulted)
                {
                    if (_state.LifecycleStatus != StatusFaulted)
                    {
                        _state.LifecycleStatus = StatusTerminated;
                    }
                    _drainCompletion.TrySetResult();
                }
            }
        }
    }

    public void Dispose()
    {
        Complete();
        _instrumentation.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await DrainAsync().ConfigureAwait(false);
        Dispose();
    }
}
