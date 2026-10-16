namespace Axrone.Physics3D;

using System.Threading;

/// <summary>
/// Standard high-precision IEEE 754 convergence policy.
/// </summary>
public readonly struct StandardGjkPolicy : IGjkConvergencePolicy<StandardGjkPolicy>
{
    /// <inheritdoc/>
    public static DistanceSquared OverlapTolerance => DistanceSquared.From(1e-12f);

    /// <inheritdoc/>
    public static DistanceSquared DuplicateTolerance => DistanceSquared.From(1e-12f);

    /// <inheritdoc/>
    public static Scalar ImprovementTolerance => Scalar.From(1e-9f);

    /// <inheritdoc/>
    public static DistanceSquared ZeroDirectionEpsilon => DistanceSquared.From(1e-12f);

    /// <inheritdoc/>
    public static IterationLimit DefaultIterationLimit => IterationLimit.From(24);
}

/// <summary>
/// High-precision convergence policy for sensitive geometric validation.
/// </summary>
public readonly struct HighPrecisionGjkPolicy : IGjkConvergencePolicy<HighPrecisionGjkPolicy>
{
    /// <inheritdoc/>
    public static DistanceSquared OverlapTolerance => DistanceSquared.From(1e-16f);

    /// <inheritdoc/>
    public static DistanceSquared DuplicateTolerance => DistanceSquared.From(1e-16f);

    /// <inheritdoc/>
    public static Scalar ImprovementTolerance => Scalar.From(1e-12f);

    /// <inheritdoc/>
    public static DistanceSquared ZeroDirectionEpsilon => DistanceSquared.From(1e-16f);

    /// <inheritdoc/>
    public static IterationLimit DefaultIterationLimit => IterationLimit.From(64);
}

/// <summary>
/// Fast, early-out convergence policy for real-time physics and coarse raycasts.
/// </summary>
public readonly struct FastGjkPolicy : IGjkConvergencePolicy<FastGjkPolicy>
{
    /// <inheritdoc/>
    public static DistanceSquared OverlapTolerance => DistanceSquared.From(1e-6f);

    /// <inheritdoc/>
    public static DistanceSquared DuplicateTolerance => DistanceSquared.From(1e-6f);

    /// <inheritdoc/>
    public static Scalar ImprovementTolerance => Scalar.From(1e-4f);

    /// <inheritdoc/>
    public static DistanceSquared ZeroDirectionEpsilon => DistanceSquared.From(1e-6f);

    /// <inheritdoc/>
    public static IterationLimit DefaultIterationLimit => IterationLimit.From(12);
}

/// <summary>
/// Zero-overhead no-op observer. All call sites are completely elided by RyuJIT.
/// </summary>
public readonly struct NullGjkObserver : IGjkObserver<NullGjkObserver>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void OnIteration(IterationCount iteration, DistanceSquared closestDistanceSq, in Vec3 searchDirection)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void OnTerminated(GjkStatus status, IterationCount iterations, Distance finalDistance)
    {
    }
}

/// <summary>
/// Lock-free, cache-line-isolated atomic telemetry state.
/// Physically separates mutated fields across distinct 64-byte cache lines to eliminate false sharing.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 256)]
public struct GjkTelemetryState
{
    [FieldOffset(0)]
    private long _totalQueries;

    [FieldOffset(64)]
    private long _totalIterations;

    [FieldOffset(128)]
    private long _totalOverlaps;

    [FieldOffset(192)]
    private long _totalDisjoint;

    /// <summary>Recorded queries.</summary>
    public long TotalQueries => Volatile.Read(ref _totalQueries);

    /// <summary>Recorded iterations.</summary>
    public long TotalIterations => Volatile.Read(ref _totalIterations);

    /// <summary>Recorded overlaps.</summary>
    public long TotalOverlaps => Volatile.Read(ref _totalOverlaps);

    /// <summary>Recorded disjoint outcomes.</summary>
    public long TotalDisjoint => Volatile.Read(ref _totalDisjoint);

    /// <summary>Records one query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void RecordQuery() => Interlocked.Increment(ref _totalQueries);

    /// <summary>Records one iteration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void RecordIteration() => Interlocked.Increment(ref _totalIterations);

    /// <summary>Records one overlap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void RecordOverlap() => Interlocked.Increment(ref _totalOverlaps);

    /// <summary>Records one disjoint outcome.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void RecordDisjoint() => Interlocked.Increment(ref _totalDisjoint);
}

/// <summary>
/// Thread-safe diagnostic observer tracking hardware performance counters without false sharing.
/// </summary>
public readonly struct TelemetryGjkObserver : IGjkObserver<TelemetryGjkObserver>
{
    private static GjkTelemetryState s_state;

    /// <summary>Shared telemetry sink; mutations land through the reference.</summary>
    public static ref GjkTelemetryState TelemetryState => ref s_state;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void OnIteration(IterationCount iteration, DistanceSquared closestDistanceSq, in Vec3 searchDirection)
    {
        s_state.RecordIteration();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void OnTerminated(GjkStatus status, IterationCount iterations, Distance finalDistance)
    {
        s_state.RecordQuery();
        if (status == GjkStatus.TouchingOrOverlapping)
        {
            s_state.RecordOverlap();
        }
        else if (status == GjkStatus.Disjoint)
        {
            s_state.RecordDisjoint();
        }
    }
}
