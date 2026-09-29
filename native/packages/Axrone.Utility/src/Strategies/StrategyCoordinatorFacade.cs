namespace Axrone.Utility.Strategies;

using System.Threading.Tasks;
using Axrone.Utility.Backoff.SpinPolicies;
using Axrone.Utility.Descriptors;

/// <summary>Coordinator with standard adaptive backoff and null metrics.</summary>
public sealed class DynamicStrategyCoordinator<TContext, TInput, TOutput> : IDisposable, IAsyncDisposable
{
    private readonly DynamicStrategyCoordinator<TContext, TInput, TOutput, AdaptiveSpinBackoff, NullStrategyMetricsSink> _core;

    /// <summary>Currently active strategy.</summary>
    public StrategyId ActiveStrategyId => _core.ActiveStrategyId;

    /// <summary>In-flight executions.</summary>
    public long InFlightCount => _core.InFlightCount;

    /// <summary>Generational handle of the active strategy.</summary>
    public DescriptorHandle<StrategyNode> ActiveStrategyHandle => _core.ActiveStrategyHandle;

    /// <summary>Live registrations.</summary>
    public long RegisteredCount => _core.RegisteredCount;

    /// <summary>Creates a coordinator.</summary>
    public DynamicStrategyCoordinator(DescriptorTableOptions? descriptorOptions = null)
    {
        _core = new DynamicStrategyCoordinator<TContext, TInput, TOutput, AdaptiveSpinBackoff, NullStrategyMetricsSink>(descriptorOptions: descriptorOptions);
    }

    /// <summary>Registers a static strategy type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Register<TStrategy>()
        where TStrategy : struct, IStaticStrategy<TContext, TInput, TOutput> =>
        _core.Register<TStrategy>();

    /// <summary>Registers an instance strategy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Register(IStrategy<TContext, TInput, TOutput> strategy) =>
        _core.Register(strategy);

    /// <summary>Registers a prebuilt descriptor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Register(StrategyDescriptor<TContext, TInput, TOutput> descriptor) =>
        _core.Register(descriptor);

    /// <summary>Registers a batch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RegisterBatch(params ReadOnlySpan<StrategyDescriptor<TContext, TInput, TOutput>> descriptors) =>
        _core.RegisterBatch(descriptors);

    /// <summary>Swaps the active strategy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySwap(StrategyId id) =>
        _core.TrySwap(id);

    /// <summary>Swaps the active strategy by generational handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySwap(in DescriptorHandle<StrategyNode> handle) =>
        _core.TrySwap(in handle);

    /// <summary>Executes the active strategy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public StrategyResult Execute(ref TContext context, in TInput input, out TOutput output) =>
        _core.Execute(ref context, in input, out output);

    /// <summary>Transitions to draining or faulted.</summary>
    public void Complete(Exception? error = null) => _core.Complete(error);

    /// <summary>Drains in-flight executions.</summary>
    public ValueTask DrainAsync(CancellationToken cancellationToken = default) => _core.DrainAsync(cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => _core.Dispose();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _core.DisposeAsync();
}
