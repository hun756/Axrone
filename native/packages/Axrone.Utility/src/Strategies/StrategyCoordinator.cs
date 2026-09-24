namespace Axrone.Utility.Strategies;

using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Axrone.Utility.Alignment;
using Axrone.Utility.Backoff;

/// <summary>
/// Lock-free strategy execution with atomic hot-swapping. Registration and swaps
/// serialize on a cold-path gate; the execution hot path takes no locks.
/// Backoff reuses the shared <see cref="ISpinBackoff"/> family — no second dialect.
/// </summary>
public sealed class DynamicStrategyCoordinator<TContext, TInput, TOutput, TBackoff, TMetrics> : IDisposable, IAsyncDisposable
    where TBackoff : struct, ISpinBackoff
    where TMetrics : struct, IStrategyMetricsSink
{
    private const int StateUninitialized = 0;
    private const int StateActive = 1;
    private const int StateDraining = 2;
    private const int StateTerminated = 3;
    private const int StateFaulted = 4;

    private AlignedAtomicCounter64 _lifecycleState;
    private AlignedAtomicCounter64 _inFlightCount;

    private volatile StrategyDescriptor<TContext, TInput, TOutput>? _activeDescriptor;

    private readonly Lock _lifecycleLock = new();
    private ExceptionDispatchInfo? _terminalFault;
    private TaskCompletionSource<bool>? _drainCompletion;
    private int _isDisposed;

    private StrategyDescriptor<TContext, TInput, TOutput>?[] _registry = new StrategyDescriptor<TContext, TInput, TOutput>?[8];
    private uint _registryCount;

    private TMetrics _metrics;

    /// <summary>Currently active strategy.</summary>
    public StrategyId ActiveStrategyId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => field;
        private set
        {
            if (value == StrategyId.Empty)
            {
                ThrowHelper.ThrowArgumentException(nameof(value), "Strategy ID cannot be zero or uninitialized.");
            }

            field = value;
        }
    }

    /// <summary>In-flight executions.</summary>
    public long InFlightCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inFlightCount.Value;
    }

    /// <summary>Creates a coordinator. The id starts empty until the first registration.</summary>
    public DynamicStrategyCoordinator(TMetrics metrics = default)
    {
        _metrics = metrics;
        _lifecycleState.Reset();
        _inFlightCount.Reset();
    }

    /// <summary>Registers a static strategy type with a cached invoker.</summary>
    public void Register<TStrategy>()
        where TStrategy : struct, IStaticStrategy<TContext, TInput, TOutput>
    {
        lock (_lifecycleLock)
        {
            EnsureAssignable();

            var descriptor = new StrategyDescriptor<TContext, TInput, TOutput>(
                TStrategy.Id,
                static (ref TContext ctx, in TInput inp, out TOutput outp) =>
                    TStrategy.Execute(ref ctx, in inp, out outp));

            RegisterCore(descriptor);
        }
    }

    /// <summary>Registers an instance strategy.</summary>
    public void Register(IStrategy<TContext, TInput, TOutput> strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        lock (_lifecycleLock)
        {
            EnsureAssignable();

            var descriptor = new StrategyDescriptor<TContext, TInput, TOutput>(
                strategy.Id,
                strategy.Execute,
                strategy);

            RegisterCore(descriptor);
        }
    }

    /// <summary>Registers a prebuilt descriptor.</summary>
    public void Register(StrategyDescriptor<TContext, TInput, TOutput> descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        lock (_lifecycleLock)
        {
            EnsureAssignable();
            RegisterCore(descriptor);
        }
    }

    /// <summary>Registers a batch without heap array allocation.</summary>
    public void RegisterBatch(params ReadOnlySpan<StrategyDescriptor<TContext, TInput, TOutput>> descriptors)
    {
        lock (_lifecycleLock)
        {
            EnsureAssignable();
            nuint length = (nuint)descriptors.Length;
            for (nuint i = 0; i < length; i++)
            {
                RegisterCore(descriptors[(int)i]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RegisterCore(StrategyDescriptor<TContext, TInput, TOutput> descriptor)
    {
        StrategyDescriptor<TContext, TInput, TOutput>?[] registry = _registry;
        uint count = _registryCount;

        if (count == (uint)registry.Length)
        {
            var expanded = new StrategyDescriptor<TContext, TInput, TOutput>?[registry.Length << 1];
            Array.Copy(registry, expanded, registry.Length);
            _registry = expanded;
            registry = expanded;
        }

        registry[count] = descriptor;
        _registryCount = count + 1;

        if (_activeDescriptor is null)
        {
            _activeDescriptor = descriptor;
            ActiveStrategyId = descriptor.Id;
            _lifecycleState.Reset();
            _lifecycleState.Add(StateActive);
        }
    }

    /// <summary>
    /// Atomically swaps the active strategy. Serialized with registration so the
    /// registry/count pair is always observed consistently (no torn reads).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TrySwap(StrategyId id)
    {
        if (id == StrategyId.Empty)
        {
            return false;
        }

        lock (_lifecycleLock)
        {
            StrategyDescriptor<TContext, TInput, TOutput>?[] registry = _registry;
            nuint count = _registryCount;

            for (nuint i = 0; i < count; i++)
            {
                StrategyDescriptor<TContext, TInput, TOutput>? candidate = registry[i];
                if (candidate is not null && candidate.Id == id)
                {
                    StrategyDescriptor<TContext, TInput, TOutput>? previous = Interlocked.Exchange(ref _activeDescriptor, candidate);
                    ActiveStrategyId = id;
                    _metrics.OnSwapped(previous?.Id ?? StrategyId.Empty, id);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Lock-free hot-path execution. Faults isolate per call: the terminal fault is
    /// captured, waiters are released, and the original exception propagates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public StrategyResult Execute(ref TContext context, in TInput input, out TOutput output)
    {
        if (_lifecycleState.Value != StateActive)
        {
            return ExecuteColdInactive(ref context, in input, out output);
        }

        _inFlightCount.Increment();

        if (_lifecycleState.Value != StateActive)
        {
            return ExecuteColdDraining(ref context, in input, out output);
        }

        StrategyDescriptor<TContext, TInput, TOutput>? descriptor = _activeDescriptor;
        if (descriptor is null)
        {
            return ExecuteColdMissingStrategy(ref context, in input, out output);
        }

        try
        {
            StrategyResult result = descriptor.Invoker(ref context, in input, out output);
            _metrics.OnExecuted(descriptor.Id, result);
            return result;
        }
        catch (Exception ex)
        {
            _metrics.OnFaulted(descriptor.Id, ex);
            HandleTerminalFault(ex);
            throw;
        }
        finally
        {
            if (_inFlightCount.Decrement() == 0 && _lifecycleState.Value >= StateDraining)
            {
                SignalDrainCompleted();
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private StrategyResult ExecuteColdInactive(ref TContext context, in TInput input, out TOutput output)
    {
        output = default!;
        _terminalFault?.Throw();
        ThrowHelper.ThrowInvalidOperationException($"Strategy coordinator is inactive. Internal state: {_lifecycleState.Value}.");
        return StrategyResult.Faulted(500);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private StrategyResult ExecuteColdDraining(ref TContext context, in TInput input, out TOutput output)
    {
        if (_inFlightCount.Decrement() == 0 && _lifecycleState.Value >= StateDraining)
        {
            SignalDrainCompleted();
        }

        output = default!;
        _terminalFault?.Throw();
        ThrowHelper.ThrowInvalidOperationException("Strategy coordinator is currently draining in-flight work.");
        return StrategyResult.Faulted(503);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static StrategyResult ExecuteColdMissingStrategy(ref TContext context, in TInput input, out TOutput output)
    {
        output = default!;
        ThrowHelper.ThrowInvalidOperationException("No operational strategy is bound to the dispatcher.");
        return StrategyResult.Faulted(404);
    }

    /// <summary>Transitions to draining (or faulted with an error).</summary>
    public void Complete(Exception? error = null)
    {
        lock (_lifecycleLock)
        {
            if (error is not null)
            {
                _terminalFault = ExceptionDispatchInfo.Capture(error);
                _lifecycleState.Reset();
                _lifecycleState.Add(StateFaulted);
                _drainCompletion?.TrySetException(error);
                return;
            }

            long state = _lifecycleState.Value;
            if (state == StateActive || state == StateUninitialized)
            {
                _lifecycleState.Reset();
                _lifecycleState.Add(StateDraining);
                if (_inFlightCount.Value == 0)
                {
                    _lifecycleState.Reset();
                    _lifecycleState.Add(StateTerminated);
                    _drainCompletion?.TrySetResult(true);
                }
            }
        }
    }

    /// <summary>Awaits in-flight executions to drain with cancellation support.</summary>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TaskCompletionSource<bool> completion;
        lock (_lifecycleLock)
        {
            long state = _lifecycleState.Value;
            if (state == StateTerminated)
            {
                return;
            }

            if (state == StateFaulted)
            {
                _terminalFault?.Throw();
                return;
            }

            if (state == StateActive || state == StateUninitialized)
            {
                _lifecycleState.Reset();
                _lifecycleState.Add(StateDraining);
            }

            if (_inFlightCount.Value == 0)
            {
                _lifecycleState.Reset();
                _lifecycleState.Add(StateTerminated);
                return;
            }

            _drainCompletion ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            completion = _drainCompletion;
        }

        if (cancellationToken.CanBeCanceled)
        {
            await using (cancellationToken.UnsafeRegister(static s =>
            {
                var target = (TaskCompletionSource<bool>)s!;
                target.TrySetCanceled();
            }, completion).ConfigureAwait(false))
            {
                await completion.Task.ConfigureAwait(false);
            }
        }
        else
        {
            await completion.Task.ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        Complete();

        TBackoff.Initialize(out int spin);
        while (_lifecycleState.Value == StateDraining && _inFlightCount.Value > 0)
        {
            TBackoff.Advance(ref spin);
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        Complete();
        try
        {
            await DrainAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Terminal fault state preserved for inspection.
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void HandleTerminalFault(Exception ex)
    {
        _terminalFault = ExceptionDispatchInfo.Capture(ex);
        _lifecycleState.Reset();
        _lifecycleState.Add(StateFaulted);

        lock (_lifecycleLock)
        {
            _drainCompletion?.TrySetException(ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SignalDrainCompleted()
    {
        lock (_lifecycleLock)
        {
            if (_lifecycleState.Value == StateDraining)
            {
                _lifecycleState.Reset();
                _lifecycleState.Add(StateTerminated);
                _drainCompletion?.TrySetResult(true);
            }
        }
    }

    private void EnsureAssignable()
    {
        if (_lifecycleState.Value >= StateDraining)
        {
            ThrowHelper.ThrowInvalidOperationException($"Strategy mutation prohibited during state: {_lifecycleState.Value}.");
        }
    }
}
