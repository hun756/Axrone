namespace Axrone.Geometry;

using Axrone.Utility.Backoff;
using Axrone.Utility.Backoff.SpinPolicies;

/// <summary>Builder state before any choice is made.</summary>
public readonly struct QuadUnconfigured;

/// <summary>Builder state after the root bounds are chosen.</summary>
public readonly struct QuadBoundsConfigured;

/// <summary>Builder state after the tuning is supplied.</summary>
public readonly struct QuadFullyConfigured;

/// <summary>
/// Compile-time typestate builder for <see cref="QuadTree{T, TBackoff, TMetrics}"/>.
/// The stage parameter only permits <c>Build</c> once root bounds (and
/// optionally tuning) are configured, so a boundless tree cannot be constructed.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
/// <typeparam name="TBackoff">Spin policy to build the tree with.</typeparam>
/// <typeparam name="TMetrics">Instrumentation sink to build the tree with.</typeparam>
/// <typeparam name="TStage">Configuration stage marker.</typeparam>
public readonly struct QuadTreeBuilder<T, TBackoff, TMetrics, TStage>
    where T : unmanaged
    where TBackoff : struct, ISpinBackoff
    where TMetrics : struct, IQuadMetricsSink
{
    internal Aabb2D Bounds { get; }

    internal QuadTreeConfig Config { get; }

    internal QuadTreeBuilder(in Aabb2D bounds, in QuadTreeConfig config)
    {
        Bounds = bounds;
        Config = config;
    }

    /// <summary>Starts a builder with the default backoff, metrics and tuning.</summary>
    public static QuadTreeBuilder<T, AdaptiveSpinBackoff, NullQuadMetricsSink, QuadUnconfigured> Create()
    {
        return new(default, new QuadTreeConfig());
    }

    /// <summary>Chooses the root bounds, advancing to the bounds-configured stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuadTreeBuilder<T, TBackoff, TMetrics, QuadBoundsConfigured> WithBounds(in Aabb2D bounds) =>
        new(bounds, Config);

    /// <summary>Overrides the spin policy, keeping the current stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuadTreeBuilder<T, TNewBackoff, TMetrics, TStage> WithBackoff<TNewBackoff>()
        where TNewBackoff : struct, ISpinBackoff =>
        new(Bounds, Config);

    /// <summary>Overrides the metrics sink, keeping the current stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuadTreeBuilder<T, TBackoff, TNewMetrics, TStage> WithMetrics<TNewMetrics>()
        where TNewMetrics : struct, IQuadMetricsSink =>
        new(Bounds, Config);

    /// <summary>Supplies the tuning, advancing to the fully-configured stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QuadTreeBuilder<T, TBackoff, TMetrics, QuadFullyConfigured> WithConfig(in QuadTreeConfig config) =>
        new(Bounds, config);

    internal QuadTree<T, TBackoff, TMetrics> BuildCore()
    {
        return new QuadTree<T, TBackoff, TMetrics>(Bounds, Config);
    }
}

/// <summary>Build entry points enabled once the builder stage permits them.</summary>
public static class QuadTreeBuilderExtensions
{
    /// <summary>Builds the tree from a bounds-configured builder.</summary>
    public static QuadTree<T, TBackoff, TMetrics> Build<T, TBackoff, TMetrics>(
        this QuadTreeBuilder<T, TBackoff, TMetrics, QuadBoundsConfigured> builder)
        where T : unmanaged
        where TBackoff : struct, ISpinBackoff
        where TMetrics : struct, IQuadMetricsSink
    {
        return builder.BuildCore();
    }

    /// <summary>Builds the tree from a fully-configured builder.</summary>
    public static QuadTree<T, TBackoff, TMetrics> Build<T, TBackoff, TMetrics>(
        this QuadTreeBuilder<T, TBackoff, TMetrics, QuadFullyConfigured> builder)
        where T : unmanaged
        where TBackoff : struct, ISpinBackoff
        where TMetrics : struct, IQuadMetricsSink
    {
        return builder.BuildCore();
    }
}
