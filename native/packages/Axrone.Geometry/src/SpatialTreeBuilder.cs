namespace Axrone.Geometry;

/// <summary>Builder state before any choice is made.</summary>
public readonly struct SpatialUnconfigured;

/// <summary>Builder state after the capacity is chosen.</summary>
public readonly struct SpatialCapacityConfigured;

/// <summary>Builder state after the margins are supplied.</summary>
public readonly struct SpatialFullyConfigured;

/// <summary>Entry point over the typestate spatial-tree builder.</summary>
public static class SpatialTreeBuilder
{
    /// <summary>Starts a builder with the default expansion and SAH policies.</summary>
    public static SpatialTreeBuilder<T, SpatialUnconfigured, DefaultExpansionPolicy, StandardSAHPolicy> Create<T>() =>
        new(new TreeCapacity(1024), 0.2f, 2.0f);
}

/// <summary>
/// Compile-time typestate builder for <see cref="DynamicSpatialTree{T, TExpansionPolicy, TSAHPolicy}"/>.
/// The stage parameter only permits <c>Build</c> once capacity and margins are
/// configured, so an unconfigured tree cannot be constructed.
/// </summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
/// <typeparam name="TPhase">Configuration stage marker.</typeparam>
/// <typeparam name="TExpansionPolicy">Fattening policy to build the tree with.</typeparam>
/// <typeparam name="TSAHPolicy">Insertion cost model to build the tree with.</typeparam>
public readonly struct SpatialTreeBuilder<T, TPhase, TExpansionPolicy, TSAHPolicy>
    where TExpansionPolicy : struct, IFatExpansionPolicy
    where TSAHPolicy : struct, ISurfaceAreaHeuristicPolicy
{
    /// <summary>Slots the node pool opens with.</summary>
    public TreeCapacity Capacity { get; }

    /// <summary>Per-side slack grown onto every stored box.</summary>
    public float FatMargin { get; }

    /// <summary>Scale applied to the margin for moving items.</summary>
    public float VelocityMultiplier { get; }

    internal SpatialTreeBuilder(TreeCapacity capacity, float fatMargin, float velocityMultiplier)
    {
        Capacity = capacity;
        FatMargin = fatMargin;
        VelocityMultiplier = velocityMultiplier;
    }

    /// <summary>Chooses the capacity, advancing to the capacity-configured stage.</summary>
    public SpatialTreeBuilder<T, SpatialCapacityConfigured, TExpansionPolicy, TSAHPolicy> WithCapacity(TreeCapacity capacity)
    {
        return new(capacity, FatMargin, VelocityMultiplier);
    }

    /// <summary>Overrides the expansion policy, keeping the current stage.</summary>
    public SpatialTreeBuilder<T, TPhase, TNewExpansion, TSAHPolicy> WithExpansionPolicy<TNewExpansion>()
        where TNewExpansion : struct, IFatExpansionPolicy
    {
        return new(Capacity, FatMargin, VelocityMultiplier);
    }

    /// <summary>Overrides the SAH policy, keeping the current stage.</summary>
    public SpatialTreeBuilder<T, TPhase, TExpansionPolicy, TNewSAH> WithSAHPolicy<TNewSAH>()
        where TNewSAH : struct, ISurfaceAreaHeuristicPolicy
    {
        return new(Capacity, FatMargin, VelocityMultiplier);
    }
}

/// <summary>Build entry points enabled once the builder stage permits them.</summary>
public static class SpatialTreeBuilderExtensions
{
    /// <summary>Supplies the margins, advancing to the fully-configured stage.</summary>
    public static SpatialTreeBuilder<T, SpatialFullyConfigured, TExpansionPolicy, TSAHPolicy> WithMargins<T, TExpansionPolicy, TSAHPolicy>(
        this SpatialTreeBuilder<T, SpatialCapacityConfigured, TExpansionPolicy, TSAHPolicy> builder,
        float fatMargin,
        float velocityMultiplier)
        where TExpansionPolicy : struct, IFatExpansionPolicy
        where TSAHPolicy : struct, ISurfaceAreaHeuristicPolicy
    {
        if (fatMargin < 0.0f || float.IsNaN(fatMargin))
        {
            ThrowHelper.ThrowInvalidFatMargin();
        }

        if (velocityMultiplier < 1.0f || float.IsNaN(velocityMultiplier))
        {
            ThrowHelper.ThrowInvalidVelocityMultiplier();
        }

        return new(builder.Capacity, fatMargin, velocityMultiplier);
    }

    /// <summary>Supplies the default margins, advancing to the fully-configured stage.</summary>
    public static SpatialTreeBuilder<T, SpatialFullyConfigured, TExpansionPolicy, TSAHPolicy> WithDefaultMargins<T, TExpansionPolicy, TSAHPolicy>(
        this SpatialTreeBuilder<T, SpatialCapacityConfigured, TExpansionPolicy, TSAHPolicy> builder)
        where TExpansionPolicy : struct, IFatExpansionPolicy
        where TSAHPolicy : struct, ISurfaceAreaHeuristicPolicy
    {
        return new(builder.Capacity, 0.2f, 2.0f);
    }

    /// <summary>Builds the tree from a fully-configured builder.</summary>
    public static DynamicSpatialTree<T, TExpansionPolicy, TSAHPolicy> Build<T, TExpansionPolicy, TSAHPolicy>(
        this SpatialTreeBuilder<T, SpatialFullyConfigured, TExpansionPolicy, TSAHPolicy> builder)
        where TExpansionPolicy : struct, IFatExpansionPolicy
        where TSAHPolicy : struct, ISurfaceAreaHeuristicPolicy
    {
        return new DynamicSpatialTree<T, TExpansionPolicy, TSAHPolicy>(builder.Capacity, builder.FatMargin, builder.VelocityMultiplier);
    }
}
