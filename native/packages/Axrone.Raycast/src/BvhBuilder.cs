namespace Axrone.Raycast;

/// <summary>Marker for the compile-time stage of a BVH build.</summary>
public interface IBuilderPhase
{
}

/// <summary>Builder state before any choice is made.</summary>
public readonly struct UnconfiguredPhase : IBuilderPhase
{
}

/// <summary>Builder state after tuning is supplied.</summary>
public readonly struct ConfiguredPhase : IBuilderPhase
{
}

/// <summary>
/// Compile-time typestate builder for <see cref="BvhPipeline{TPrimitive}"/>.
/// Tuning is optional: <c>Configure</c> moves to the buildable stage, and the
/// defaults (four primitives per leaf, sixteen SAH bins) apply when omitted.
/// </summary>
/// <typeparam name="TPrimitive">Primitive type stored in the hierarchy.</typeparam>
/// <typeparam name="TPhase">Configuration stage marker.</typeparam>
public readonly record struct BvhPipelineBuilder<TPrimitive, TPhase>
    where TPrimitive : unmanaged, IPrimitiveGeometry<TPrimitive>
    where TPhase : struct, IBuilderPhase
{
    /// <summary>Primitives grouped under one leaf before it splits.</summary>
    public int MaxLeafCapacity
    {
        get => field;
        init => field = value > 0 ? value : ThrowHelper.ThrowArgumentOutOfRange<int>(nameof(value));
    } = 4;

    /// <summary>Bins of the SAH split search.</summary>
    public int SahBinCount
    {
        get => field;
        init => field = value >= 4 ? value : ThrowHelper.ThrowArgumentOutOfRange<int>(nameof(value));
    } = 16;

    internal BvhPipelineBuilder(int maxLeafCapacity, int sahBinCount)
    {
        MaxLeafCapacity = maxLeafCapacity;
        SahBinCount = sahBinCount;
    }

    /// <summary>Starts a builder with default tuning.</summary>
    public static BvhPipelineBuilder<TPrimitive, UnconfiguredPhase> Create() => new(4, 16);

    /// <summary>Supplies tuning, advancing to the buildable stage.</summary>
    public BvhPipelineBuilder<TPrimitive, ConfiguredPhase> Configure(int maxLeafCapacity = 4, int sahBinCount = 16)
    {
        return new BvhPipelineBuilder<TPrimitive, ConfiguredPhase>(maxLeafCapacity, sahBinCount);
    }
}

/// <summary>Build entry point enabled once tuning is configured.</summary>
public static class BvhPipelineBuilderExtensions
{
    /// <summary>Builds the hierarchy over <paramref name="primitives"/>; empty input is rejected.</summary>
    public static BvhPipeline<TPrimitive> BuildHierarchy<TPrimitive>(
        this BvhPipelineBuilder<TPrimitive, ConfiguredPhase> builder,
        ReadOnlySpan<TPrimitive> primitives)
        where TPrimitive : unmanaged, IPrimitiveGeometry<TPrimitive>
    {
        if (primitives.IsEmpty)
        {
            ThrowHelper.ThrowArgumentEmpty(nameof(primitives));
        }

        return BvhPipeline<TPrimitive>.Construct(primitives, builder.MaxLeafCapacity, builder.SahBinCount);
    }
}
