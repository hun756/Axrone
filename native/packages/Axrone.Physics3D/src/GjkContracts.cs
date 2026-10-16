namespace Axrone.Physics3D;

/// <summary>
/// Static-abstract shape support mapping contract.
/// Monomorphized without virtual dispatch. Anti-constrained with allows ref struct for stack allocations.
/// Unlike the 2D contract this also carries bounds: oriented wrappers
/// resolve generic bounds through their local frames.
/// </summary>
/// <typeparam name="TSelf">The shape type itself.</typeparam>
public interface ISupportShape3D<TSelf> where TSelf : allows ref struct
{
    /// <summary>Returns the volume centroid of the shape.</summary>
    static abstract Vec3 ComputeCentroid(in TSelf shape);

    /// <summary>Returns the point of the shape furthest along <paramref name="direction"/>.</summary>
    static abstract Vec3 Support(in TSelf shape, in Vec3 direction);

    /// <summary>Returns the tight bounds of the shape.</summary>
    static abstract Aabb3D ComputeBounds(in TSelf shape);
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
    static abstract void OnIteration(IterationCount iteration, DistanceSquared closestDistanceSq, in Vec3 searchDirection);

    /// <summary>Observes traversal termination.</summary>
    static abstract void OnTerminated(GjkStatus status, IterationCount iterations, Distance finalDistance);
}
