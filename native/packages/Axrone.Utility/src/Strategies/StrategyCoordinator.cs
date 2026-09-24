namespace Axrone.Utility.Strategies;

using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Axrone.Utility.Alignment;
using Axrone.Utility.Backoff;
using Axrone.Utility.Descriptors;

/// <summary>
/// Lock-free strategy execution with atomic hot-swapping. Registration and swaps
/// serialize on a cold-path gate; the execution hot path takes no locks.
/// Backoff reuses the shared <see cref="ISpinBackoff"/> family — no second dialect.
/// Node identity is derived from the shared descriptor library: every registration
/// occupies a generational <see cref="DescriptorTable{TDescriptor}"/> slot, so the
/// coordinator owns no parallel id universe — handles are ABA-safe by construction
/// and liveness is table-authoritative. The hot path never touches table memory.
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

    private readonly DescriptorTable<StrategyNode> _nodes;
    private readonly StrategyDescriptor<TContext, TInput, TOutput>?[] _sidecar;

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

    /// <summary>Generational handle of the active strategy; invalid until the first registration.</summary>
    public DescriptorHandle<StrategyNode> ActiveStrategyHandle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _activeDescriptor?.Handle ?? default;
    }

    /// <summary>Live registrations (table-authoritative).</summary>
    public long RegisteredCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _nodes.ActiveCount;
    }

    /// <summary>
    /// Creates a coordinator. The id starts empty until the first registration.
    /// Registry capacity comes from the descriptor table options (default 64 slots).
    /// </summary>
    public DynamicStrategyCoordinator(TMetrics metrics = default, DescriptorTableOptions? descriptorOptions = null)
    {
        _metrics = metrics;
        _nodes = new DescriptorTable<StrategyNode>(descriptorOptions ?? new DescriptorTableOptions { Capacity = 64 });
        _sidecar = new StrategyDescriptor<TContext, TInput, TOutput>?[_nodes.Capacity];
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

            RegisterCore(descriptor, StrategyNodeKind.Static);
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

            RegisterCore(descriptor, StrategyNodeKind.Instance);
        }
    }

    /// <summary>Registers a prebuilt descriptor.</summary>
    public void Register(StrategyDescriptor<TContext, TInput, TOutput> descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        lock (_lifecycleLock)
        {
            EnsureAssignable();
            RegisterCore(descriptor, StrategyNodeKind.Instance);
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
                RegisterCore(descriptors[(int)i], StrategyNodeKind.Instance);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RegisterCore(StrategyDescriptor<TContext, TInput, TOutput> descriptor, StrategyNodeKind flags)
    {
        if (descriptor.Handle.IsValid)
        {
            ThrowHelper.ThrowInvalidOperationException("Strategy descriptor is already registered.");
        }

        var node = new StrategyNode(descriptor.Id.Value, flags);
        if (!_nodes.TryAllocate(in node, out DescriptorHandle<StrategyNode> handle))
        {
            ThrowHelper.ThrowInvalidOperationException($"Strategy registry exhausted (capacity {_nodes.Capacity}).");
        }

        _nodes.TrySetStatus(in handle, DescriptorStatus.Active);
        _sidecar[handle.SlotIndex] = descriptor;
        descriptor.Handle = handle;

        if (_activeDescriptor is null)
        {
            _activeDescriptor = descriptor;
            ActiveStrategyId = descriptor.Id;
            _lifecycleState.Reset();
            _lifecycleState.Add(StateActive);
        }
    }

    /// <summary>
    /// Atomically swaps the active strategy by declared id. Serialized with
    /// registration so slot state is always observed consistently (no torn reads).
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
            StrategyDescriptor<TContext, TInput, TOutput>?[] sidecar = _sidecar;
            nuint capacity = _nodes.Capacity;

            for (nuint i = 0; i < capacity; i++)
            {
                StrategyDescriptor<TContext, TInput, TOutput>? candidate = sidecar[i];
                if (candidate is not null && candidate.Id == id)
                {
                    SwapCore(candidate);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Atomically swaps the active strategy by generational handle. Stale handles
    /// (freed slots, recycled generations) fail closed — no scan, ABA-safe.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TrySwap(in DescriptorHandle<StrategyNode> handle)
    {
        if (!handle.IsValid)
        {
            return false;
        }

        lock (_lifecycleLock)
        {
            if (!_nodes.TryGet(in handle, out _))
            {
                return false;
            }

            StrategyDescriptor<TContext, TInput, TOutput>? candidate = _sidecar[handle.SlotIndex];
            if (candidate is null || candidate.Handle != handle)
            {
                return false;
            }

            SwapCore(candidate);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SwapCore(StrategyDescriptor<TContext, TInput, TOutput> candidate)
    {
        StrategyDescriptor<TContext, TInput, TOutput>? previous = Interlocked.Exchange(ref _activeDescriptor, candidate);
        ActiveStrategyId = candidate.Id;
        _metrics.OnSwapped(previous?.Id ?? StrategyId.Empty, candidate.Id);
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

        FreeAllDescriptors();
        _nodes.Dispose();
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
            FreeAllDescriptors();
            await _nodes.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Reclaims every table slot. Runs after the dispatch drain, so no execution
    /// can observe a freed slot; the hot path holds only managed references.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FreeAllDescriptors()
    {
        lock (_lifecycleLock)
        {
            StrategyDescriptor<TContext, TInput, TOutput>?[] sidecar = _sidecar;
            nuint capacity = _nodes.Capacity;

            for (nuint i = 0; i < capacity; i++)
            {
                StrategyDescriptor<TContext, TInput, TOutput>? descriptor = sidecar[i];
                if (descriptor is not null)
                {
                    DescriptorHandle<StrategyNode> handle = descriptor.Handle;
                    descriptor.Handle = default;
                    sidecar[i] = null;
                    if (handle.IsValid)
                    {
                        _nodes.TryFree(in handle);
                    }
                }
            }
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
