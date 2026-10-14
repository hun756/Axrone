namespace Axrone.Geometry;

using System.Numerics;

/// <summary>
/// Query half of the native octree: octant-masked box scans, slab-pruned ray
/// casts, sphere scans and best-first nearest search. Reads run lock-free
/// behind retired (never freed until dispose) pools; every query holds an
/// in-flight count so dispose waits it out.
/// </summary>
public sealed partial class NativeOctree<T, TPartition, TMetrics>
    where T : unmanaged
    where TPartition : struct, IPartitionPolicy<TPartition>
    where TMetrics : struct, IOctreeMetrics<TMetrics>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe bool QueryBox<TVisitor>(in Aabb3D queryBox, ref TVisitor visitor)
        where TVisitor : IOctreeVisitor<T>, allows ref struct
    {
        if (!TryEnterOperation())
        {
            return false;
        }

        try
        {
            TraversalStack128 stack = default;
            int stackPtr = 0;
            stack[stackPtr++] = NodeIndex.FromIndex(0);

            OctreeNode* nodes = _nodes;
            OctreeItem<T>* items = _items;
            Vec3 queryMin = queryBox.Min;
            Vec3 queryMax = queryBox.Max;

            while (stackPtr > 0)
            {
                NodeIndex nodeIdx = stack[--stackPtr];
                ref OctreeNode node = ref nodes[nodeIdx.RawValue];

                if (!node.Bounds.Overlaps(in queryBox))
                {
                    continue;
                }

                SlotIndex itemIdx = node.FirstItemIndex;
                while (itemIdx.IsValid)
                {
                    ref OctreeItem<T> item = ref items[itemIdx.Value];
                    if (item.Bounds.Overlaps(in queryBox))
                    {
                        if (!visitor.OnVisit(new OctreeHandle(itemIdx, item.Generation), in item.Bounds, in item.Value))
                        {
                            return false;
                        }
                    }

                    itemIdx = item.NextItemIndex;
                }

                if (node.ChildBaseIndex.IsValid)
                {
                    uint candidateMask = OctantMask(queryMin, queryMax, node.Center) & node.ChildMask;

                    while (candidateMask != 0)
                    {
                        int octant = BitOperations.TrailingZeroCount(candidateMask);
                        candidateMask &= candidateMask - 1u;

                        if ((uint)stackPtr >= 128u)
                        {
                            ThrowHelper.ThrowStackDepthExceeded();
                        }

                        stack[stackPtr++] = NodeIndex.FromIndex(node.ChildBaseIndex.RawValue + octant);
                    }
                }
            }

            return true;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>Exposes the box scan as an allocation-free enumerator; the tree is held for the walk.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoxQueryEnumerator<T, TPartition, TMetrics> EnumerateBox(in Aabb3D queryBox)
    {
        ValidateActiveState();
        return new BoxQueryEnumerator<T, TPartition, TMetrics>(this, in queryBox);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe bool Raycast<TVisitor>(in Ray3D ray, Distance maxDistance, ref TVisitor visitor)
        where TVisitor : IRaycastVisitor<T>, allows ref struct
    {
        if (!TryEnterOperation())
        {
            return false;
        }

        try
        {
            float limit = maxDistance.Value;
            TraversalStack128 stack = default;
            int stackPtr = 0;
            stack[stackPtr++] = NodeIndex.FromIndex(0);

            OctreeNode* nodes = _nodes;
            OctreeItem<T>* items = _items;

            while (stackPtr > 0)
            {
                NodeIndex nodeIdx = stack[--stackPtr];
                ref OctreeNode node = ref nodes[nodeIdx.RawValue];

                if (!node.Bounds.Intersects(in ray, limit, out _))
                {
                    continue;
                }

                SlotIndex itemIdx = node.FirstItemIndex;
                while (itemIdx.IsValid)
                {
                    ref OctreeItem<T> item = ref items[itemIdx.Value];
                    if (item.Bounds.Intersects(in ray, limit, out float hitFloat))
                    {
                        if (!visitor.OnHit(new OctreeHandle(itemIdx, item.Generation), in item.Bounds, in item.Value, new Distance(hitFloat)))
                        {
                            return false;
                        }
                    }

                    itemIdx = item.NextItemIndex;
                }

                if (node.ChildBaseIndex.IsValid)
                {
                    uint mask = node.ChildMask;
                    while (mask != 0)
                    {
                        int octant = BitOperations.TrailingZeroCount(mask);
                        mask &= mask - 1u;

                        NodeIndex childIdx = NodeIndex.FromIndex(node.ChildBaseIndex.RawValue + octant);
                        if (nodes[childIdx.RawValue].Bounds.Intersects(in ray, limit, out _))
                        {
                            if ((uint)stackPtr >= 128u)
                            {
                                ThrowHelper.ThrowStackDepthExceeded();
                            }

                            stack[stackPtr++] = childIdx;
                        }
                    }
                }
            }

            return true;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe bool QuerySphere<TVisitor>(in BoundingSphere3 sphere, ref TVisitor visitor)
        where TVisitor : IOctreeVisitor<T>, allows ref struct
    {
        if (!TryEnterOperation())
        {
            return false;
        }

        try
        {
            TraversalStack128 stack = default;
            int stackPtr = 0;
            stack[stackPtr++] = NodeIndex.FromIndex(0);

            OctreeNode* nodes = _nodes;
            OctreeItem<T>* items = _items;

            while (stackPtr > 0)
            {
                NodeIndex nodeIdx = stack[--stackPtr];
                ref OctreeNode node = ref nodes[nodeIdx.RawValue];

                if (!node.Bounds.Intersects(in sphere))
                {
                    continue;
                }

                SlotIndex itemIdx = node.FirstItemIndex;
                while (itemIdx.IsValid)
                {
                    ref OctreeItem<T> item = ref items[itemIdx.Value];
                    if (item.Bounds.Intersects(in sphere))
                    {
                        if (!visitor.OnVisit(new OctreeHandle(itemIdx, item.Generation), in item.Bounds, in item.Value))
                        {
                            return false;
                        }
                    }

                    itemIdx = item.NextItemIndex;
                }

                if (node.ChildBaseIndex.IsValid)
                {
                    uint mask = node.ChildMask;
                    while (mask != 0)
                    {
                        int octant = BitOperations.TrailingZeroCount(mask);
                        mask &= mask - 1u;

                        NodeIndex childIdx = NodeIndex.FromIndex(node.ChildBaseIndex.RawValue + octant);
                        if (nodes[childIdx.RawValue].Bounds.Intersects(in sphere))
                        {
                            if ((uint)stackPtr >= 128u)
                            {
                                ThrowHelper.ThrowStackDepthExceeded();
                            }

                            stack[stackPtr++] = childIdx;
                        }
                    }
                }
            }

            return true;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe NearestNeighborResult<T> FindNearest(in Vec3 point, Distance maxDistance)
    {
        if (!TryEnterOperation())
        {
            return NearestNeighborResult<T>.NotFound();
        }

        try
        {
            TraversalStack128 stack = default;
            int stackPtr = 0;
            stack[stackPtr++] = NodeIndex.FromIndex(0);

            float bestDistSq = maxDistance.Value * maxDistance.Value;
            bool found = false;
            SpatialItem<T> bestItem = default;

            Span<(NodeIndex Index, float DistSq)> childSortBuffer = stackalloc (NodeIndex, float)[8];
            OctreeNode* nodes = _nodes;
            OctreeItem<T>* items = _items;

            while (stackPtr > 0)
            {
                NodeIndex nodeIdx = stack[--stackPtr];
                ref OctreeNode node = ref nodes[nodeIdx.RawValue];

                if (node.Bounds.DistanceSquared(in point) > bestDistSq)
                {
                    continue;
                }

                SlotIndex itemIdx = node.FirstItemIndex;
                while (itemIdx.IsValid)
                {
                    ref OctreeItem<T> item = ref items[itemIdx.Value];
                    float distSq = item.Bounds.DistanceSquared(in point);
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestItem = new SpatialItem<T>(new OctreeHandle(itemIdx, item.Generation), item.Bounds, item.Value);
                        found = true;
                    }

                    itemIdx = item.NextItemIndex;
                }

                if (node.ChildBaseIndex.IsValid)
                {
                    int validCount = 0;
                    uint mask = node.ChildMask;

                    while (mask != 0)
                    {
                        int octant = BitOperations.TrailingZeroCount(mask);
                        mask &= mask - 1u;

                        NodeIndex childIdx = NodeIndex.FromIndex(node.ChildBaseIndex.RawValue + octant);
                        float childDistSq = nodes[childIdx.RawValue].Bounds.DistanceSquared(in point);
                        if (childDistSq <= bestDistSq)
                        {
                            childSortBuffer[validCount++] = (childIdx, childDistSq);
                        }
                    }

                    for (int i = 1; i < validCount; i++)
                    {
                        (NodeIndex Index, float DistSq) key = childSortBuffer[i];
                        int j = i - 1;
                        while (j >= 0 && childSortBuffer[j].DistSq > key.DistSq)
                        {
                            childSortBuffer[j + 1] = childSortBuffer[j];
                            j--;
                        }

                        childSortBuffer[j + 1] = key;
                    }

                    for (int i = validCount - 1; i >= 0; i--)
                    {
                        if ((uint)stackPtr >= 128u)
                        {
                            ThrowHelper.ThrowStackDepthExceeded();
                        }

                        stack[stackPtr++] = childSortBuffer[i].Index;
                    }
                }
            }

            return found
                ? NearestNeighborResult<T>.Found(bestItem, bestDistSq)
                : NearestNeighborResult<T>.NotFound();
        }
        finally
        {
            ExitOperation();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static uint OctantMask(Vec3 queryMin, Vec3 queryMax, Vec3 center)
    {
        uint maskX = (queryMin.X <= center.X ? 0x55u : 0u) | (queryMax.X >= center.X ? 0xAAu : 0u);
        uint maskY = (queryMin.Y <= center.Y ? 0x33u : 0u) | (queryMax.Y >= center.Y ? 0xCCu : 0u);
        uint maskZ = (queryMin.Z <= center.Z ? 0x0Fu : 0u) | (queryMax.Z >= center.Z ? 0xF0u : 0u);
        return maskX & maskY & maskZ;
    }
}

/// <summary>
/// Allocation-free box-scan enumerator over a native octree. Unlike a raw
/// pointer walk, it holds an in-flight count for the enumeration lifetime, so
/// dispose waits it out instead of freeing the pools underneath it.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
/// <typeparam name="TPartition">Split/collapse policy of the enumerated tree.</typeparam>
/// <typeparam name="TMetrics">Instrumentation sink of the enumerated tree.</typeparam>
public ref struct BoxQueryEnumerator<T, TPartition, TMetrics>
    where T : unmanaged
    where TPartition : struct, IPartitionPolicy<TPartition>
    where TMetrics : struct, IOctreeMetrics<TMetrics>
{
    private readonly NativeOctree<T, TPartition, TMetrics> _tree;
    private readonly Aabb3D _queryBox;
    private TraversalStack128 _stack;
    private int _stackPtr;
    private SlotIndex _currentItemIdx;
    private SpatialItem<T> _current;
    private bool _held;

    /// <summary>Creates an enumerator; prefer <c>EnumerateBox</c> on the tree.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal BoxQueryEnumerator(NativeOctree<T, TPartition, TMetrics> tree, in Aabb3D queryBox)
    {
        _tree = tree;
        _queryBox = queryBox;
        _stack = default;
        _stackPtr = 0;
        _currentItemIdx = SlotIndex.Invalid;
        _current = default;
        _held = tree.TryEnterOperation();
        if (_held)
        {
            _stack[_stackPtr++] = NodeIndex.FromIndex(0);
        }
    }

    /// <summary>Returns this enumerator for <see langword="foreach"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoxQueryEnumerator<T, TPartition, TMetrics> GetEnumerator() => this;

    /// <summary>The item at the current enumerator position.</summary>
    public SpatialItem<T> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    /// <summary>Advances to the next overlapping item, if any.</summary>
    /// <returns><see langword="true"/> while a further item exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext()
    {
        if (!_held)
        {
            return false;
        }

        while (true)
        {
            if (_currentItemIdx.IsValid)
            {
                ref OctreeItem<T> item = ref _tree.ItemAt(_currentItemIdx.Value);
                SlotIndex activeSlot = _currentItemIdx;
                _currentItemIdx = item.NextItemIndex;

                if (item.Bounds.Overlaps(in _queryBox))
                {
                    _current = new SpatialItem<T>(
                        new OctreeHandle(activeSlot, item.Generation),
                        item.Bounds,
                        item.Value);
                    return true;
                }

                continue;
            }

            if (_stackPtr == 0)
            {
                return false;
            }

            NodeIndex nodeIdx = _stack[--_stackPtr];
            ref OctreeNode node = ref _tree.NodeAt((uint)nodeIdx.RawValue);

            if (!node.Bounds.Overlaps(in _queryBox))
            {
                continue;
            }

            if (node.ChildBaseIndex.IsValid)
            {
                uint candidateMask = NativeOctree<T, TPartition, TMetrics>.OctantMask(_queryBox.Min, _queryBox.Max, node.Center) & node.ChildMask;

                while (candidateMask != 0)
                {
                    int octant = BitOperations.TrailingZeroCount(candidateMask);
                    candidateMask &= candidateMask - 1u;

                    if ((uint)_stackPtr >= 128u)
                    {
                        ThrowHelper.ThrowStackDepthExceeded();
                    }

                    _stack[_stackPtr++] = NodeIndex.FromIndex(node.ChildBaseIndex.RawValue + octant);
                }
            }

            _currentItemIdx = node.FirstItemIndex;
        }
    }

    /// <summary>Releases the tree held for the enumeration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_held)
        {
            _held = false;
            _tree.ExitOperation();
        }
    }
}
