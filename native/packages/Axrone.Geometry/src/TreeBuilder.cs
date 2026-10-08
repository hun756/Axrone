namespace Axrone.Geometry;

/// <summary>Builder state before any choice is made.</summary>
public readonly struct StageUnconfigured;

/// <summary>Builder state after the partition strategy is chosen.</summary>
public readonly struct StageStrategyConfigured;

/// <summary>Builder state after tuning options are supplied.</summary>
public readonly struct StageFullyConfigured;

/// <summary>
/// Compile-time typestate builder for <see cref="DynamicAabbTree{TUserData, TStrategy, TMetrics}"/>.
/// The stage parameter only permits <c>Build</c> once a strategy (and optionally
/// tuning) is configured, so an unconfigured tree cannot be constructed.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
/// <typeparam name="TStrategy">Insertion policy; a struct for dispatch without virtual calls.</typeparam>
/// <typeparam name="TMetrics">Traversal instrumentation sink; a struct for the same reason.</typeparam>
/// <typeparam name="TStage">Configuration stage marker.</typeparam>
public readonly struct DynamicAabbTreeBuilder<TUserData, TStrategy, TMetrics, TStage>
    where TStrategy : struct, ISpatialPartitionStrategy
    where TMetrics : struct, ISpatialMetricsSink
{
    private readonly TreeOptions _options;

    internal DynamicAabbTreeBuilder(TreeOptions options)
    {
        _options = options;
    }

    /// <summary>Starts a builder with the default strategy, metrics and tuning.</summary>
    public static DynamicAabbTreeBuilder<TUserData, SurfaceAreaHeuristicStrategy, NullSpatialMetricsSink, StageUnconfigured> Create()
    {
        return new(new TreeOptions());
    }

    /// <summary>Selects the partition strategy, advancing to the strategy-configured stage.</summary>
    /// <typeparam name="TNewStrategy">The insertion policy to build the tree with.</typeparam>
    public DynamicAabbTreeBuilder<TUserData, TNewStrategy, TMetrics, StageStrategyConfigured> WithStrategy<TNewStrategy>()
        where TNewStrategy : struct, ISpatialPartitionStrategy
    {
        return new(_options);
    }

    /// <summary>Selects the metrics sink, keeping the current stage.</summary>
    /// <typeparam name="TNewMetrics">The instrumentation sink to build the tree with.</typeparam>
    public DynamicAabbTreeBuilder<TUserData, TStrategy, TNewMetrics, TStage> WithMetrics<TNewMetrics>()
        where TNewMetrics : struct, ISpatialMetricsSink
    {
        return new(_options);
    }

    /// <summary>Supplies the tuning, advancing to the fully-configured stage.</summary>
    /// <param name="options">Fattening, velocity budget and initial capacity.</param>
    public DynamicAabbTreeBuilder<TUserData, TStrategy, TMetrics, StageFullyConfigured> WithOptions(TreeOptions options)
    {
        return new(options);
    }

    internal DynamicAabbTree<TUserData, TStrategy, TMetrics> BuildCore()
    {
        return new DynamicAabbTree<TUserData, TStrategy, TMetrics>(_options);
    }
}

/// <summary>Build entry points enabled once the builder stage permits them.</summary>
public static class DynamicAabbTreeBuilderExtensions
{
    /// <summary>Builds the tree from a strategy-configured builder.</summary>
    public static DynamicAabbTree<TUserData, TStrategy, TMetrics> Build<TUserData, TStrategy, TMetrics>(
        this DynamicAabbTreeBuilder<TUserData, TStrategy, TMetrics, StageStrategyConfigured> builder)
        where TStrategy : struct, ISpatialPartitionStrategy
        where TMetrics : struct, ISpatialMetricsSink
    {
        return builder.BuildCore();
    }

    /// <summary>Builds the tree from a fully-configured builder.</summary>
    public static DynamicAabbTree<TUserData, TStrategy, TMetrics> Build<TUserData, TStrategy, TMetrics>(
        this DynamicAabbTreeBuilder<TUserData, TStrategy, TMetrics, StageFullyConfigured> builder)
        where TStrategy : struct, ISpatialPartitionStrategy
        where TMetrics : struct, ISpatialMetricsSink
    {
        return builder.BuildCore();
    }
}
