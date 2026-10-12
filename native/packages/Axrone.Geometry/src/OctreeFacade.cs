namespace Axrone.Geometry;

/// <summary>
/// Default-policy octree facade over <see cref="NativeOctree{T, TPartition, TMetrics}"/>.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
public sealed class NativeOctree<T> : IOctreeIndex<T> where T : unmanaged
{
    private readonly NativeOctree<T, DefaultPartitionPolicy, NullOctreeMetrics> _inner;

    /// <summary>Creates a tree over <paramref name="rootBounds"/> with the given capacities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeOctree(
        in Aabb3D rootBounds,
        NodeCapacity initialNodeCapacity = default,
        ItemCapacity initialItemCapacity = default)
    {
        _inner = new NativeOctree<T, DefaultPartitionPolicy, NullOctreeMetrics>(
            in rootBounds,
            initialNodeCapacity,
            initialItemCapacity);
    }

    /// <summary>Creates a tree over the default ±1024 domain.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeOctree()
        : this(new Aabb3D(new Vec3(-1024f), new Vec3(1024f)))
    {
    }

    /// <inheritdoc/>
    public uint Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Count;
    }

    /// <inheritdoc/>
    public Aabb3D RootBounds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.RootBounds;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OctreeHandle Insert(in Aabb3D bounds, in T value) => _inner.Insert(in bounds, in value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InsertResult TryInsert(in Aabb3D bounds, in T value) => _inner.TryInsert(in bounds, in value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(OctreeHandle handle) => _inner.Remove(handle);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Update(OctreeHandle handle, in Aabb3D newBounds, in T newValue) => _inner.Update(handle, in newBounds, in newValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _inner.Clear();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool QueryBox<TVisitor>(in Aabb3D queryBox, ref TVisitor visitor)
        where TVisitor : IOctreeVisitor<T>, allows ref struct =>
        _inner.QueryBox(in queryBox, ref visitor);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoxQueryEnumerator<T, DefaultPartitionPolicy, NullOctreeMetrics> EnumerateBox(in Aabb3D queryBox) =>
        _inner.EnumerateBox(in queryBox);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Raycast<TVisitor>(in Ray3D ray, Distance maxDistance, ref TVisitor visitor)
        where TVisitor : IRaycastVisitor<T>, allows ref struct =>
        _inner.Raycast(in ray, maxDistance, ref visitor);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool QuerySphere<TVisitor>(in BoundingSphere3 sphere, ref TVisitor visitor)
        where TVisitor : IOctreeVisitor<T>, allows ref struct =>
        _inner.QuerySphere(in sphere, ref visitor);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NearestNeighborResult<T> FindNearest(in Vec3 point, Distance maxDistance) =>
        _inner.FindNearest(in point, maxDistance);

    /// <inheritdoc/>
    public void Complete(Exception? error = null) => _inner.Complete(error);

    /// <inheritdoc/>
    public ValueTask DrainAsync(CancellationToken cancellationToken = default) => _inner.DrainAsync(cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => _inner.Dispose();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

/// <summary>Builder state before any choice is made.</summary>
public readonly struct PhaseUnconfigured;

/// <summary>Builder state after the root domain is chosen.</summary>
public readonly struct PhaseDomainConfigured;

/// <summary>Builder state after capacities are supplied.</summary>
public readonly struct PhaseFullyConfigured;

/// <summary>
/// Typestate builder for <see cref="NativeOctree{T, TPartition, TMetrics}"/>.
/// The stage parameter only permits <c>Build</c> once the root domain (and
/// optionally capacities) is configured, so a domainless tree cannot be
/// constructed.
/// </summary>
/// <typeparam name="TPhase">Configuration stage marker.</typeparam>
public readonly struct OctreeBuilder<TPhase>
{
    internal Aabb3D Bounds { get; }

    internal NodeCapacity NodeCap { get; }

    internal ItemCapacity ItemCap { get; }

    internal OctreeBuilder(in Aabb3D bounds, NodeCapacity nodeCapacity, ItemCapacity itemCapacity)
    {
        Bounds = bounds;
        NodeCap = nodeCapacity;
        ItemCap = itemCapacity;
    }
}

/// <summary>Entry point over the typestate octree builder.</summary>
public static class OctreeBuilder
{
    /// <summary>Starts a builder with default capacities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OctreeBuilder<PhaseUnconfigured> Create() => new(default, new NodeCapacity(1024), new ItemCapacity(2048));
}

/// <summary>Build entry points enabled once the builder stage permits them.</summary>
public static class OctreeBuilderExtensions
{
    /// <summary>Chooses the root domain, advancing to the domain-configured stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OctreeBuilder<PhaseDomainConfigured> WithDomain(
        this OctreeBuilder<PhaseUnconfigured> builder,
        in Aabb3D domain)
    {
        Vec3 min = domain.Min;
        Vec3 max = domain.Max;
        if (float.IsNaN(min.X) || float.IsNaN(min.Y) || float.IsNaN(min.Z) ||
            float.IsNaN(max.X) || float.IsNaN(max.Y) || float.IsNaN(max.Z) ||
            min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z)
        {
            ThrowHelper.ThrowInvalidBounds(in domain);
        }

        return new OctreeBuilder<PhaseDomainConfigured>(domain, builder.NodeCap, builder.ItemCap);
    }

    /// <summary>Supplies capacities, advancing to the fully-configured stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OctreeBuilder<PhaseFullyConfigured> WithCapacities(
        this OctreeBuilder<PhaseDomainConfigured> builder,
        NodeCapacity nodeCapacity,
        ItemCapacity itemCapacity)
    {
        if (nodeCapacity.Value < 8)
        {
            ThrowHelper.ThrowInvalidCapacity(nameof(nodeCapacity));
        }

        if (itemCapacity.Value < 16)
        {
            ThrowHelper.ThrowInvalidCapacity(nameof(itemCapacity));
        }

        return new OctreeBuilder<PhaseFullyConfigured>(builder.Bounds, nodeCapacity, itemCapacity);
    }

    /// <summary>Builds a default-policy tree from a domain-configured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeOctree<T, DefaultPartitionPolicy, NullOctreeMetrics> Build<T>(
        this OctreeBuilder<PhaseDomainConfigured> builder)
        where T : unmanaged
    {
        return new NativeOctree<T, DefaultPartitionPolicy, NullOctreeMetrics>(builder.Bounds, builder.NodeCap, builder.ItemCap);
    }

    /// <summary>Builds a custom-policy tree from a fully-configured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeOctree<T, TPartition, TMetrics> Build<T, TPartition, TMetrics>(
        this OctreeBuilder<PhaseFullyConfigured> builder)
        where T : unmanaged
        where TPartition : struct, IPartitionPolicy<TPartition>
        where TMetrics : struct, IOctreeMetrics<TMetrics>
    {
        return new NativeOctree<T, TPartition, TMetrics>(builder.Bounds, builder.NodeCap, builder.ItemCap);
    }

    /// <summary>Builds a default-policy tree from a fully-configured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeOctree<T, DefaultPartitionPolicy, NullOctreeMetrics> Build<T>(
        this OctreeBuilder<PhaseFullyConfigured> builder)
        where T : unmanaged
    {
        return new NativeOctree<T, DefaultPartitionPolicy, NullOctreeMetrics>(builder.Bounds, builder.NodeCap, builder.ItemCap);
    }
}
