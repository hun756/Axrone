namespace Axrone.Patterns;

/// <summary>
/// Stateless read-only visit: computes a result from a node without mutating it.
/// Bound statically so the traversal call devirtualizes with no closure.
/// </summary>
public interface IVisitorPolicy<TNode, TContext, TResult>
    where TNode : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Visits <paramref name="node"/>.</summary>
    static abstract TResult Visit(in TNode node, ref TContext context);
}

/// <summary>Stateless mutating visit: may rewrite the node in place.</summary>
public interface IMutatingVisitorPolicy<TNode, TContext, TResult>
    where TNode : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Visits and possibly mutates <paramref name="node"/>.</summary>
    static abstract TResult Visit(ref TNode node, ref TContext context);
}

/// <summary>Stateless visit over a pair, used by pair-enumeration passes.</summary>
public interface IDualVisitorPolicy<TLeft, TRight, TContext, TResult>
    where TLeft : allows ref struct
    where TRight : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Visits the pair.</summary>
    static abstract TResult Visit(in TLeft left, in TRight right, ref TContext context);
}

/// <summary>
/// Try-pattern visit: reports whether it handled the node and only then
/// produces a result. Pairs with <c>FallbackPolicy</c>.
/// </summary>
public interface IFallbackVisitorPolicy<TNode, TContext, TResult>
    where TNode : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Attempts the visit; <see langword="false"/> means "not mine".</summary>
    static abstract bool TryVisit(in TNode node, ref TContext context, out TResult result);
}

/// <summary>
/// Two-stage traversal gate: <c>ShouldTraverse</c> decides whether the node is
/// visited at all, <c>ShouldDescend</c> whether its children are walked.
/// </summary>
public interface ITraversalFilter<TNode, TContext>
    where TNode : allows ref struct
    where TContext : allows ref struct
{
    /// <summary>Whether the node itself is visited.</summary>
    static abstract bool ShouldTraverse(in TNode node, ref TContext context);

    /// <summary>Whether the walk continues into the children.</summary>
    static abstract bool ShouldDescend(in TNode node, ref TContext context);
}

/// <summary>Filter that visits everything and descends everywhere.</summary>
public readonly struct DefaultTraversalFilter<TNode, TContext> : ITraversalFilter<TNode, TContext>
    where TNode : allows ref struct
    where TContext : allows ref struct
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldTraverse(in TNode node, ref TContext context) => true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ShouldDescend(in TNode node, ref TContext context) => true;
}

/// <summary>Stateful read-only visit, for visitors that carry instance state.</summary>
public interface IVisitor<TNode, TContext, TResult>
    where TNode : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Visits <paramref name="node"/>.</summary>
    TResult Visit(in TNode node, ref TContext context);
}

/// <summary>Stateful mutating visit.</summary>
public interface IMutatingVisitor<TNode, TContext, TResult>
    where TNode : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Visits and possibly mutates <paramref name="node"/>.</summary>
    TResult Visit(ref TNode node, ref TContext context);
}

/// <summary>Stateful visit over a pair.</summary>
public interface IDualVisitor<TLeft, TRight, TContext, TResult>
    where TLeft : allows ref struct
    where TRight : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Visits the pair.</summary>
    TResult Visit(in TLeft left, in TRight right, ref TContext context);
}

/// <summary>Double-dispatch target: routes itself into a hierarchical visitor.</summary>
public interface IVisitable<TContext, TResult>
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Accepts <paramref name="visitor"/>.</summary>
    TResult Accept<TVisitor>(ref TVisitor visitor, ref TContext context)
        where TVisitor : allows ref struct;
}

/// <summary>Element side of visitor/element double dispatch.</summary>
public interface IElement<TVisitor, TContext, TResult>
    where TVisitor : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Accepts <paramref name="visitor"/>.</summary>
    TResult Accept(ref TVisitor visitor, ref TContext context);
}

/// <summary>Exposes the children of a hierarchy node as a handle span.</summary>
/// <typeparam name="TNode">The node type of the hierarchy.</typeparam>
public interface INodeHierarchy<TNode>
{
    /// <summary>Returns the child handles of <paramref name="node"/>.</summary>
    static abstract ReadOnlySpan<NodeHandle> GetChildren(in TNode node);
}

/// <summary>Object-hierarchy visitor with a default arm for unhandled elements.</summary>
public interface IHierarchicalVisitor<TBase, TContext, TResult>
    where TBase : class
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Handles any element without a dedicated arm.</summary>
    TResult VisitDefault(TBase element, ref TContext context);
}

/// <summary>Base class for class-hierarchy elements carrying a type identity.</summary>
/// <typeparam name="TBase">The root of the element hierarchy.</typeparam>
public abstract class VisitableElement<TBase>
    where TBase : VisitableElement<TBase>
{
    /// <summary>The discriminating type identity.</summary>
    public abstract NodeTypeId TypeId { get; }

    /// <summary>Dispatches into the matching visitor arm.</summary>
    public abstract TResult Accept<TVisitor, TContext, TResult>(ref TVisitor visitor, ref TContext context)
        where TVisitor : IHierarchicalVisitor<TBase, TContext, TResult>, allows ref struct
        where TContext : allows ref struct
        where TResult : allows ref struct;
}

/// <summary>Runs two unit policies in sequence over the same node.</summary>
public readonly struct ChainedPolicy<TFirst, TSecond, TNode, TContext> : IVisitorPolicy<TNode, TContext, Unit>
    where TFirst : struct, IVisitorPolicy<TNode, TContext, Unit>
    where TSecond : struct, IVisitorPolicy<TNode, TContext, Unit>
    where TNode : allows ref struct
    where TContext : allows ref struct
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Unit Visit(in TNode node, ref TContext context)
    {
        TFirst.Visit(in node, ref context);
        TSecond.Visit(in node, ref context);
        return Unit.Value;
    }
}

/// <summary>Tries the primary policy, falling back to the secondary on a miss.</summary>
public readonly struct FallbackPolicy<TPrimary, TFallback, TNode, TContext, TResult> : IVisitorPolicy<TNode, TContext, TResult>
    where TPrimary : struct, IFallbackVisitorPolicy<TNode, TContext, TResult>
    where TFallback : struct, IVisitorPolicy<TNode, TContext, TResult>
    where TNode : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TResult Visit(in TNode node, ref TContext context)
    {
        if (TPrimary.TryVisit(in node, ref context, out TResult result))
        {
            return result;
        }
        return TFallback.Visit(in node, ref context);
    }
}

/// <summary>Runs two stateful visitors in sequence over the same node.</summary>
public readonly ref struct ChainedVisitor<TFirst, TSecond, TNode, TContext> : IVisitor<TNode, TContext, Unit>
    where TFirst : IVisitor<TNode, TContext, Unit>, allows ref struct
    where TSecond : IVisitor<TNode, TContext, Unit>, allows ref struct
    where TNode : allows ref struct
    where TContext : allows ref struct
{
    private readonly TFirst _first;
    private readonly TSecond _second;

    /// <summary>Chains <paramref name="first"/> and <paramref name="second"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChainedVisitor(TFirst first, TSecond second)
    {
        _first = first;
        _second = second;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Unit Visit(in TNode node, ref TContext context)
    {
        Unsafe.AsRef(in _first).Visit(in node, ref context);
        Unsafe.AsRef(in _second).Visit(in node, ref context);
        return Unit.Value;
    }
}

/// <summary>Stateful try/fallback pair reusing the try-pattern policy shape.</summary>
public readonly ref struct FallbackVisitor<TPrimary, TFallback, TNode, TContext, TResult> : IVisitor<TNode, TContext, TResult>
    where TPrimary : IFallbackVisitorPolicy<TNode, TContext, TResult>, allows ref struct
    where TFallback : IVisitor<TNode, TContext, TResult>, allows ref struct
    where TNode : allows ref struct
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    private readonly TPrimary _primary;
    private readonly TFallback _fallback;

    /// <summary>Pairs <paramref name="primary"/> with <paramref name="fallback"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FallbackVisitor(TPrimary primary, TFallback fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TResult Visit(in TNode node, ref TContext context)
    {
        if (TPrimary.TryVisit(in node, ref context, out TResult result))
        {
            return result;
        }
        return Unsafe.AsRef(in _fallback).Visit(in node, ref context);
    }
}

/// <summary>Folds every visited node into caller-owned state through a function pointer.</summary>
/// <remarks>The accumulator must be an unmanaged value that outlives the visitor
/// (typically a stack local); it is pinned by address, never boxed or copied.</remarks>
public readonly ref struct FoldVisitor<TAccumulator, TNode, TContext> : IVisitor<TNode, TContext, Unit>
    where TAccumulator : unmanaged
    where TNode : allows ref struct
    where TContext : allows ref struct
{
    private readonly unsafe delegate*<ref TAccumulator, in TNode, ref TContext, void> _foldAction;
    private readonly unsafe TAccumulator* _accumulator;

    /// <summary>Builds a fold over <paramref name="accumulator"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe FoldVisitor(
        ref TAccumulator accumulator,
        delegate*<ref TAccumulator, in TNode, ref TContext, void> foldAction)
    {
        _accumulator = (TAccumulator*)Unsafe.AsPointer(ref accumulator);
        _foldAction = foldAction;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe Unit Visit(in TNode node, ref TContext context)
    {
        _foldAction(ref *_accumulator, in node, ref context);
        return Unit.Value;
    }
}
