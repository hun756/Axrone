namespace Axrone.Utility.Strategies;

/// <summary>Universal strategy invocation signature.</summary>
public delegate StrategyResult StrategyInvoker<TContext, TInput, TOutput>(
    ref TContext context,
    in TInput input,
    out TOutput output);

/// <summary>Instance-based strategy for dynamic polymorphism.</summary>
public interface IStrategy<TContext, TInput, TOutput>
{
    /// <summary>Strategy identity.</summary>
    StrategyId Id { get; }

    /// <summary>Executes against a context.</summary>
    StrategyResult Execute(ref TContext context, in TInput input, out TOutput output);
}

/// <summary>
/// Static strategy contract: static abstract members force RyuJIT monomorphization,
/// eradicating vtables and indirect calls. Ref-struct contexts stay stack-only.
/// </summary>
public interface IStaticStrategy<TContext, TInput, TOutput>
    where TContext : allows ref struct
    where TInput : allows ref struct
{
    /// <summary>Strategy identity.</summary>
    static abstract StrategyId Id { get; }

    /// <summary>Executes against a context.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    static abstract StrategyResult Execute(ref TContext context, scoped in TInput input, out TOutput output);
}

/// <summary>Monomorphic invoker guaranteeing direct inlining at the call site.</summary>
public readonly struct StaticStrategyContext<TStrategy, TContext, TInput, TOutput>
    where TStrategy : struct, IStaticStrategy<TContext, TInput, TOutput>
    where TContext : allows ref struct
    where TInput : allows ref struct
{
    /// <summary>Executes the strategy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static StrategyResult Execute(ref TContext context, scoped in TInput input, out TOutput output) =>
        TStrategy.Execute(ref context, in input, out output);
}

/// <summary>Diagnostic sink for strategy lifecycle events.</summary>
public interface IStrategyMetricsSink
{
    /// <summary>Active strategy swapped.</summary>
    void OnSwapped(StrategyId oldId, StrategyId newId);

    /// <summary>Strategy executed.</summary>
    void OnExecuted(StrategyId id, StrategyResult result);

    /// <summary>Strategy faulted.</summary>
    void OnFaulted(StrategyId id, Exception error);
}

/// <summary>No-op sink eliminated by dead-code removal.</summary>
public readonly struct NullStrategyMetricsSink : IStrategyMetricsSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnSwapped(StrategyId oldId, StrategyId newId)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnExecuted(StrategyId id, StrategyResult result)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnFaulted(StrategyId id, Exception error)
    {
    }
}
