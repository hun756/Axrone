namespace Axrone.Physics3D;

/// <summary>
/// Deterministic termination status of the GJK solver.
/// </summary>
public enum GjkStatus : byte
{
    /// <summary>Shapes are disjoint with positive separation.</summary>
    Disjoint = 0,

    /// <summary>Shapes touch or overlap.</summary>
    TouchingOrOverlapping = 1,

    /// <summary>Traversal exhausted its iteration budget while disjoint.</summary>
    MaxIterationsExceeded = 2,

    /// <summary>Reserved: degenerate centroid configuration.</summary>
    DegenerateCentroids = 3,
}

/// <summary>
/// Flat, zero-allocation discriminated struct union encoding the query result and witness points.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GjkResult(
    GjkStatus Status,
    Distance Distance,
    Vec3 WitnessPointA,
    Vec3 WitnessPointB,
    Vec3 SeparatingAxis,
    IterationCount Iterations)
{
    /// <summary>Whether the shapes are disjoint.</summary>
    public bool IsDisjoint => Status == GjkStatus.Disjoint;

    /// <summary>Whether the shapes touch or overlap.</summary>
    public bool IsOverlapping => Status == GjkStatus.TouchingOrOverlapping;

    /// <summary>
    /// Compiler-enforced exhaustive pattern matcher. Allocation-free when all
    /// callbacks are static lambdas or method groups; capturing closures box
    /// as usual.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TResult Match<TResult>(
        Func<Distance, Vec3, Vec3, Vec3, IterationCount, TResult> onDisjoint,
        Func<Vec3, Vec3, IterationCount, TResult> onOverlapping,
        Func<Distance, Vec3, Vec3, IterationCount, TResult> onMaxIterations,
        Func<IterationCount, TResult> onDegenerate)
    {
        ArgumentNullException.ThrowIfNull(onDisjoint);
        ArgumentNullException.ThrowIfNull(onOverlapping);
        ArgumentNullException.ThrowIfNull(onMaxIterations);
        ArgumentNullException.ThrowIfNull(onDegenerate);

        return Status switch
        {
            GjkStatus.Disjoint => onDisjoint(Distance, WitnessPointA, WitnessPointB, SeparatingAxis, Iterations),
            GjkStatus.TouchingOrOverlapping => onOverlapping(WitnessPointA, WitnessPointB, Iterations),
            GjkStatus.MaxIterationsExceeded => onMaxIterations(Distance, WitnessPointA, WitnessPointB, Iterations),
            GjkStatus.DegenerateCentroids => onDegenerate(Iterations),
            _ => ThrowHelper.ThrowUnreachable<TResult>(),
        };
    }
}
