namespace Axrone.Physics2D;

/// <summary>
/// Static-abstract shape support mapping contract.
/// Monomorphized without virtual dispatch. Anti-constrained with allows ref struct for stack allocations.
/// </summary>
/// <typeparam name="TSelf">The shape type itself.</typeparam>
public interface ISupportShape2D<TSelf> where TSelf : allows ref struct
{
    /// <summary>Returns the area centroid of the shape.</summary>
    static abstract Vec2 ComputeCentroid(in TSelf shape);

    /// <summary>Returns the point of the shape furthest along <paramref name="direction"/>.</summary>
    static abstract Vec2 Support(in TSelf shape, in Vec2 direction);
}

/// <summary>
/// Compile-time policy traits governing mathematical convergence, numeric tolerances, and limits.
/// </summary>
/// <typeparam name="TPolicy">The policy type itself.</typeparam>
public interface IGjkConvergencePolicy<TPolicy> where TPolicy : struct, IGjkConvergencePolicy<TPolicy>
{
    /// <summary>Below this squared distance shapes count as touching.</summary>
    static abstract DistanceSquared OverlapTolerance { get; }

    /// <summary>Below this squared distance supports count as duplicates.</summary>
    static abstract DistanceSquared DuplicateTolerance { get; }

    /// <summary>Smallest distance improvement worth another iteration.</summary>
    static abstract Scalar ImprovementTolerance { get; }

    /// <summary>Below this squared length directions count as degenerate.</summary>
    static abstract DistanceSquared ZeroDirectionEpsilon { get; }

    /// <summary>Traversal budget unless overridden per query.</summary>
    static abstract IterationLimit DefaultIterationLimit { get; }
}

/// <summary>
/// Static-abstract zero-vtable observer hook. When bound to NullGjkObserver, JIT elides calls completely.
/// </summary>
/// <typeparam name="TObserver">The observer type itself.</typeparam>
public interface IGjkObserver<TObserver> where TObserver : struct, IGjkObserver<TObserver>
{
    /// <summary>Observes one traversal iteration.</summary>
    static abstract void OnIteration(IterationCount iteration, DistanceSquared closestDistanceSq, in Vec2 searchDirection);

    /// <summary>Observes traversal termination.</summary>
    static abstract void OnTerminated(GjkStatus status, IterationCount iterations, Distance finalDistance);
}
