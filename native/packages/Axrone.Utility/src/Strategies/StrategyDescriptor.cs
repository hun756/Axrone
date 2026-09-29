namespace Axrone.Utility.Strategies;

using Axrone.Utility.Descriptors;

/// <summary>
/// Registered strategy node: declared identity, invocation, and optional state.
/// Lifecycle identity is derived from the shared descriptor library: the
/// coordinator issues a generational <see cref="Handle"/> at registration, so
/// slot reuse is ABA-safe and liveness is table-authoritative. A standalone
/// descriptor carries <see cref="DescriptorHandle{TDescriptor}.Invalid"/> until registered.
/// </summary>
public sealed class StrategyDescriptor<TContext, TInput, TOutput>
{
    /// <summary>Author-declared strategy identity.</summary>
    public StrategyId Id { get; }

    /// <summary>
    /// Generational lifecycle identity issued by the coordinator's descriptor
    /// table. Invalid until registered.
    /// </summary>
    public DescriptorHandle<StrategyNode> Handle { get; internal set; }

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
