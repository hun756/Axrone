namespace Axrone.Geometry;

using System.Buffers;

/// <summary>
/// Query half of the generational spatial tree: box and sphere overlap scans,
/// simple-slab ray casts, self-pair enumeration and closest-hit reduction. All
/// traversals are allocation-free on the hot path (stack-sized explicit stacks,
/// pooled growth only for the pair walk) and report through caller-provided
/// monomorphic sinks.
/// </summary>
public partial class DynamicSpatialTree<T, TExpansionPolicy, TSAHPolicy>
    where TExpansionPolicy : struct, IFatExpansionPolicy
    where TSAHPolicy : struct, ISurfaceAreaHeuristicPolicy
{
    private struct ClosestSink : ISpatialRaySink<T>
    {
        public SpatialHandle BestHandle;
        public Distance BestDistance;
        public Aabb3D BestBounds;
        public T BestPayload;
        public bool HasHit;

        public bool OnHit(SpatialHandle handle, in Aabb3D bounds, ref readonly T payload, Distance distance)
        {
            if (!HasHit || distance < BestDistance)
            {
                BestHandle = handle;
                BestDistance = distance;
                BestBounds = bounds;
                BestPayload = payload;
                HasHit = true;
            }

            return true;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int Query<TSink>(in Aabb3D box, ref TSink sink)
        where TSink : ISpatialQuerySink<T>, allows ref struct
    {
        using (_gate.EnterScope())
        {
            EnsureNotDisposed();
            if (_state.Root.IsNull)
            {
                return 0;
            }

            Span<NodeIndex> stack = stackalloc NodeIndex[256];
            int stackTop = 0;
            stack[stackTop++] = _state.Root;

            int hitCount = 0;

            while (stackTop > 0)
            {
                NodeIndex nodeId = stack[--stackTop];
                ref readonly DynamicTreeNode<T> node = ref GetNodeRef(nodeId);

                if (!node.FatBox.Overlaps(in box))
                {
                    continue;
                }

                if (node.IsLeaf)
                {
                    Aabb3D tightBox = node.TightBox;
                    if (tightBox.Overlaps(in box))
                    {
                        T payload = node.Payload;
                        hitCount++;
                        if (!sink.OnHit(SpatialHandle.Create(nodeId, node.Generation), in tightBox, ref payload))
                        {
                            return hitCount;
                        }
                    }
                }
                else
                {
                    stack[stackTop++] = node.Left;
                    stack[stackTop++] = node.Right;
                }
            }

            return hitCount;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int QuerySphere<TSink>(in BoundingSphere3 sphere, ref TSink sink)
        where TSink : ISpatialQuerySink<T>, allows ref struct
    {
        using (_gate.EnterScope())
        {
            EnsureNotDisposed();
            if (_state.Root.IsNull)
            {
                return 0;
            }

            Span<NodeIndex> stack = stackalloc NodeIndex[256];
            int stackTop = 0;
            stack[stackTop++] = _state.Root;

            int hitCount = 0;

            while (stackTop > 0)
            {
                NodeIndex nodeId = stack[--stackTop];
                ref readonly DynamicTreeNode<T> node = ref GetNodeRef(nodeId);

                if (!node.FatBox.Intersects(in sphere))
                {
                    continue;
                }

                if (node.IsLeaf)
                {
                    Aabb3D tightBox = node.TightBox;
                    if (tightBox.Intersects(in sphere))
                    {
                        T payload = node.Payload;
                        hitCount++;
                        if (!sink.OnHit(SpatialHandle.Create(nodeId, node.Generation), in tightBox, ref payload))
                        {
                            return hitCount;
                        }
                    }
                }
                else
                {
                    stack[stackTop++] = node.Left;
                    stack[stackTop++] = node.Right;
                }
            }

            return hitCount;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int Raycast<TSink>(in Ray3D ray, Distance maxDistance, ref TSink sink)
        where TSink : ISpatialRaySink<T>, allows ref struct
    {
        using (_gate.EnterScope())
        {
            EnsureNotDisposed();
            if (_state.Root.IsNull)
            {
                return 0;
            }

            float limit = maxDistance.Value;
            Span<NodeIndex> stack = stackalloc NodeIndex[256];
            int stackTop = 0;
            stack[stackTop++] = _state.Root;

            int hitCount = 0;

            while (stackTop > 0)
            {
                NodeIndex nodeId = stack[--stackTop];
                ref readonly DynamicTreeNode<T> node = ref GetNodeRef(nodeId);

                if (!node.FatBox.Intersects(in ray, limit, out _))
                {
                    continue;
                }

                if (node.IsLeaf)
                {
                    Aabb3D tightBox = node.TightBox;
                    if (tightBox.Intersects(in ray, limit, out float distance))
                    {
                        T payload = node.Payload;
                        hitCount++;
                        if (!sink.OnHit(SpatialHandle.Create(nodeId, node.Generation), in tightBox, ref payload, new Distance(distance)))
                        {
                            return hitCount;
                        }
                    }
                }
                else
                {
                    bool hitLeft = GetNodeRef(node.Left).FatBox.Intersects(in ray, limit, out float distLeft);
                    bool hitRight = GetNodeRef(node.Right).FatBox.Intersects(in ray, limit, out float distRight);

                    if (hitLeft && hitRight)
                    {
                        if (distLeft < distRight)
                        {
                            stack[stackTop++] = node.Right;
                            stack[stackTop++] = node.Left;
                        }
                        else
                        {
                            stack[stackTop++] = node.Left;
                            stack[stackTop++] = node.Right;
                        }
                    }
                    else if (hitLeft)
                    {
                        stack[stackTop++] = node.Left;
                    }
                    else if (hitRight)
                    {
                        stack[stackTop++] = node.Right;
                    }
                }
            }

            return hitCount;
        }
    }

    /// <summary>
    /// Casts <paramref name="ray"/> with no distance limit; excess candidates are visited but only the nearest wins.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Raycast<TSink>(in Ray3D ray, ref TSink sink)
        where TSink : ISpatialRaySink<T>, allows ref struct
    {
        return Raycast(in ray, Distance.PositiveInfinity, ref sink);
    }

    /// <summary>Returns the nearest item the ray reaches within <paramref name="maxDistance"/>, or a miss.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public RaycastResult<T> RaycastClosest(in Ray3D ray, Distance maxDistance)
    {
        var sink = new ClosestSink { BestDistance = Distance.PositiveInfinity, HasHit = false };
        Raycast(in ray, maxDistance, ref sink);

        return sink.HasHit
            ? new RaycastResult<T>(sink.BestHandle, sink.BestDistance, in sink.BestBounds, sink.BestPayload)
            : RaycastResult<T>.Miss;
    }

    /// <summary>Returns the nearest item the ray reaches, or a miss.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RaycastResult<T> RaycastClosest(in Ray3D ray) => RaycastClosest(in ray, Distance.PositiveInfinity);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void QueryOverlaps<TSink>(ref TSink sink)
        where TSink : ISpatialPairSink<T>, allows ref struct
    {
        using (_gate.EnterScope())
        {
            EnsureNotDisposed();
            if (_state.Root.IsNull || GetNodeRef(_state.Root).IsLeaf)
            {
                return;
            }

            (NodeIndex A, NodeIndex B)[]? rentedPairStack = null;
            NodeIndex[]? rentedSelfStack = null;

            Span<(NodeIndex A, NodeIndex B)> pairStack = stackalloc (NodeIndex, NodeIndex)[512];
            int pairTop = 0;

            Span<NodeIndex> selfStack = stackalloc NodeIndex[256];
            int selfTop = 0;
            selfStack[selfTop++] = _state.Root;

            try
            {
                while (selfTop > 0)
                {
                    NodeIndex nodeId = selfStack[--selfTop];
                    ref readonly DynamicTreeNode<T> node = ref GetNodeRef(nodeId);

                    if (!node.IsLeaf)
                    {
                        if (pairTop + 1 > pairStack.Length)
                        {
                            GrowPairStack(ref pairStack, ref rentedPairStack);
                        }

                        pairStack[pairTop++] = (node.Left, node.Right);

                        if (selfTop + 2 > selfStack.Length)
                        {
                            GrowNodeStack(ref selfStack, ref rentedSelfStack);
                        }

                        selfStack[selfTop++] = node.Left;
                        selfStack[selfTop++] = node.Right;
                    }
                }

                while (pairTop > 0)
                {
                    (NodeIndex nodeIdA, NodeIndex nodeIdB) = pairStack[--pairTop];
                    ref readonly DynamicTreeNode<T> nodeA = ref GetNodeRef(nodeIdA);
                    ref readonly DynamicTreeNode<T> nodeB = ref GetNodeRef(nodeIdB);

                    Aabb3D fatA = nodeA.FatBox;
                    Aabb3D fatB = nodeB.FatBox;
                    if (!fatA.Overlaps(in fatB))
                    {
                        continue;
                    }

                    if (nodeA.IsLeaf && nodeB.IsLeaf)
                    {
                        Aabb3D tightA = nodeA.TightBox;
                        Aabb3D tightB = nodeB.TightBox;
                        if (tightA.Overlaps(in tightB))
                        {
                            SpatialHandle handleA = SpatialHandle.Create(nodeIdA, nodeA.Generation);
                            SpatialHandle handleB = SpatialHandle.Create(nodeIdB, nodeB.Generation);
                            T payloadA = nodeA.Payload;
                            T payloadB = nodeB.Payload;

                            if (!sink.OnPair(handleA, ref payloadA, handleB, ref payloadB))
                            {
                                return;
                            }
                        }
                    }
                    else if (nodeA.IsLeaf)
                    {
                        if (pairTop + 2 > pairStack.Length)
                        {
                            GrowPairStack(ref pairStack, ref rentedPairStack);
                        }

                        pairStack[pairTop++] = (nodeIdA, nodeB.Left);
                        pairStack[pairTop++] = (nodeIdA, nodeB.Right);
                    }
                    else if (nodeB.IsLeaf)
                    {
                        if (pairTop + 2 > pairStack.Length)
                        {
                            GrowPairStack(ref pairStack, ref rentedPairStack);
                        }

                        pairStack[pairTop++] = (nodeA.Left, nodeIdB);
                        pairStack[pairTop++] = (nodeA.Right, nodeIdB);
                    }
                    else
                    {
                        if (pairTop + 2 > pairStack.Length)
                        {
                            GrowPairStack(ref pairStack, ref rentedPairStack);
                        }

                        if (HalfArea(nodeA.FatBox) > HalfArea(nodeB.FatBox))
                        {
                            pairStack[pairTop++] = (nodeA.Left, nodeIdB);
                            pairStack[pairTop++] = (nodeA.Right, nodeIdB);
                        }
                        else
                        {
                            pairStack[pairTop++] = (nodeIdA, nodeB.Left);
                            pairStack[pairTop++] = (nodeIdA, nodeB.Right);
                        }
                    }
                }
            }
            finally
            {
                if (rentedPairStack is not null)
                {
                    ArrayPool<(NodeIndex, NodeIndex)>.Shared.Return(rentedPairStack);
                }

                if (rentedSelfStack is not null)
                {
                    ArrayPool<NodeIndex>.Shared.Return(rentedSelfStack);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowNodeStack(ref Span<NodeIndex> stack, ref NodeIndex[]? rented)
    {
        int newSize = stack.Length * 2;
        NodeIndex[] next = ArrayPool<NodeIndex>.Shared.Rent(newSize);
        stack.CopyTo(next);
        if (rented is not null)
        {
            ArrayPool<NodeIndex>.Shared.Return(rented);
        }

        rented = next;
        stack = next.AsSpan(0, newSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowPairStack(ref Span<(NodeIndex A, NodeIndex B)> stack, ref (NodeIndex A, NodeIndex B)[]? rented)
    {
        int newSize = stack.Length * 2;
        (NodeIndex A, NodeIndex B)[] next = ArrayPool<(NodeIndex A, NodeIndex B)>.Shared.Rent(newSize);
        stack.CopyTo(next);
        if (rented is not null)
        {
            ArrayPool<(NodeIndex A, NodeIndex B)>.Shared.Return(rented);
        }

        rented = next;
        stack = next.AsSpan(0, newSize);
    }

    /// <summary>Enumerates the live leaves; a snapshot view, not a locked traversal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpatialTreeEnumerator<T, TExpansionPolicy, TSAHPolicy> GetEnumerator() => new(this);
}

/// <summary>
/// By-value enumerator over the live leaves of a generational spatial tree.
/// Namespace-level (rather than nested) per the nested-type rules; construct
/// it through the tree's <c>GetEnumerator</c>.
/// </summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
/// <typeparam name="TExpansionPolicy">Fattening policy of the enumerated tree.</typeparam>
/// <typeparam name="TSAHPolicy">Insertion cost model of the enumerated tree.</typeparam>
public ref struct SpatialTreeEnumerator<T, TExpansionPolicy, TSAHPolicy>
    where TExpansionPolicy : struct, IFatExpansionPolicy
    where TSAHPolicy : struct, ISurfaceAreaHeuristicPolicy
{
    private readonly ReadOnlySpan<DynamicTreeNode<T>> _nodes;
    private int _index;
    private readonly int _capacity;

    /// <summary>Creates an enumerator; prefer <c>GetEnumerator</c> on the tree.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SpatialTreeEnumerator(DynamicSpatialTree<T, TExpansionPolicy, TSAHPolicy> tree)
    {
        _nodes = tree._nodes.AsSpan();
        _index = -1;
        _capacity = tree._state.Capacity;
    }

    /// <summary>Advances to the next live leaf, if any.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        while (++_index < _capacity)
        {
            if (_nodes[_index].IsLeaf && !_nodes[_index].IsFree)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The node at the current position.</summary>
    public readonly ref readonly DynamicTreeNode<T> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _nodes[_index];
    }

    /// <summary>Returns this enumerator for <see langword="foreach"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpatialTreeEnumerator<T, TExpansionPolicy, TSAHPolicy> GetEnumerator() => this;
}
