namespace Axrone.Physics2D;

/// <summary>
/// Static factory entry point for typestate-enforced GJK query pipelines.
/// </summary>
public static class GjkQuery
{
    /// <summary>Starts an unconfigured query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static GjkInitialBuilder Create() => new();
}

/// <summary>
/// Stage 0 typestate builder: no shapes bound.
/// </summary>
public readonly ref struct GjkInitialBuilder
{
    /// <summary>Binds the first shape.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public GjkShapeABoundBuilder<TShapeA> WithShapeA<TShapeA>(TShapeA shapeA)
        where TShapeA : ISupportShape2D<TShapeA>, allows ref struct =>
        new(shapeA);
}

/// <summary>
/// Stage 1 typestate builder: first shape bound.
/// </summary>
/// <typeparam name="TShapeA">First shape type.</typeparam>
public readonly ref struct GjkShapeABoundBuilder<TShapeA>
    where TShapeA : ISupportShape2D<TShapeA>, allows ref struct
{
    private readonly TShapeA _shapeA;

    /// <summary>Binds the first shape.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal GjkShapeABoundBuilder(TShapeA shapeA) => _shapeA = shapeA;

    /// <summary>Binds the second shape, completing the query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public GjkConfiguredQuery<TShapeA, TShapeB, StandardGjkPolicy, NullGjkObserver> WithShapeB<TShapeB>(TShapeB shapeB)
        where TShapeB : ISupportShape2D<TShapeB>, allows ref struct =>
        new(_shapeA, shapeB);
}

/// <summary>
/// Stage 2 terminal typestate query: both shapes bound. Execute() is only exposed here at compile time.
/// Incorporates C# 14 field-backed validation for custom iteration capacities.
/// </summary>
/// <typeparam name="TShapeA">First shape type.</typeparam>
/// <typeparam name="TShapeB">Second shape type.</typeparam>
/// <typeparam name="TPolicy">Convergence policy.</typeparam>
/// <typeparam name="TObserver">Diagnostics observer.</typeparam>
public readonly ref struct GjkConfiguredQuery<TShapeA, TShapeB, TPolicy, TObserver>
    where TShapeA : ISupportShape2D<TShapeA>, allows ref struct
    where TShapeB : ISupportShape2D<TShapeB>, allows ref struct
    where TPolicy : struct, IGjkConvergencePolicy<TPolicy>
    where TObserver : struct, IGjkObserver<TObserver>
{
    private readonly TShapeA _shapeA;
    private readonly TShapeB _shapeB;

    /// <summary>Traversal budget override; validated non-zero on assignment.</summary>
    public IterationLimit IterationLimitOverride
    {
        get => field;
        init => field = value.Value == 0 ? ThrowHelper.ThrowInvalidIterationLimit<IterationLimit>() : value;
    } = TPolicy.DefaultIterationLimit;

    /// <summary>Binds both shapes with the policy default budget.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal GjkConfiguredQuery(TShapeA shapeA, TShapeB shapeB)
    {
        _shapeA = shapeA;
        _shapeB = shapeB;
    }

    /// <summary>Binds both shapes with an explicit budget.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal GjkConfiguredQuery(TShapeA shapeA, TShapeB shapeB, IterationLimit limit)
    {
        _shapeA = shapeA;
        _shapeB = shapeB;
        IterationLimitOverride = limit;
    }

    /// <summary>Swaps the convergence policy, keeping the budget.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public GjkConfiguredQuery<TShapeA, TShapeB, TNewPolicy, TObserver> WithPolicy<TNewPolicy>()
        where TNewPolicy : struct, IGjkConvergencePolicy<TNewPolicy> =>
        new(_shapeA, _shapeB, IterationLimitOverride);

    /// <summary>Swaps the observer, keeping the budget.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public GjkConfiguredQuery<TShapeA, TShapeB, TPolicy, TNewObserver> WithObserver<TNewObserver>()
        where TNewObserver : struct, IGjkObserver<TNewObserver> =>
        new(_shapeA, _shapeB, IterationLimitOverride);

    /// <summary>Overrides the traversal budget.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public GjkConfiguredQuery<TShapeA, TShapeB, TPolicy, TObserver> WithIterationLimit(IterationLimit limit) =>
        new(_shapeA, _shapeB, limit);

    /// <summary>Executes the configured query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public GjkResult Execute() =>
        GjkEngine2D.ComputeDistance<TShapeA, TShapeB, TPolicy, TObserver>(_shapeA, _shapeB, IterationLimitOverride);
}
