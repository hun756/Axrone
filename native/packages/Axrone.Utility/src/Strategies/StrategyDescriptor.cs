namespace Axrone.Utility.Strategies;

/// <summary>Registered strategy node: identity, invoker, and optional state.</summary>
public sealed class StrategyDescriptor<TContext, TInput, TOutput>
{
    /// <summary>Strategy identity.</summary>
    public StrategyId Id { get; }

    /// <summary>Invocation delegate (cached static lambda for static strategies).</summary>
    public StrategyInvoker<TContext, TInput, TOutput> Invoker { get; }

    /// <summary>Optional instance state for dynamic strategies.</summary>
    public object? State { get; }

    /// <summary>Creates a descriptor; empty ids are rejected.</summary>
    public StrategyDescriptor(
        StrategyId id,
        StrategyInvoker<TContext, TInput, TOutput> invoker,
        object? state = null)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        if (id == StrategyId.Empty)
        {
            ThrowHelper.ThrowArgumentException(nameof(id), "Strategy ID cannot be zero or uninitialized.");
        }

        Id = id;
        Invoker = invoker;
        State = state;
    }
}
