namespace Axrone.Geometry;

/// <summary>
/// Query half of the dynamic AABB tree: overlap scans, ray casts, frustum culls,
/// pair enumeration and k-nearest search. All traversals are allocation-free
/// (stack-sized explicit stacks) and report through caller-provided spans or
/// monomorphic visitors, with traversal metrics pushed to
/// <typeparamref name="TMetrics"/>.
/// </summary>
public sealed partial class DynamicAabbTree<TUserData, TStrategy, TMetrics>
    where TStrategy : struct, ISpatialPartitionStrategy
    where TMetrics : struct, ISpatialMetricsSink
{
    /// <summary>
    /// Writes the identities overlapping <paramref name="box"/> into
    /// <paramref name="results"/>; excess hits are dropped once the span is full.
    /// </summary>
    /// <param name="box">World bounds to test against the stored fat boxes.</param>
    /// <param name="results">Destination span for the overlapping identities.</param>
    /// <returns>Number of identities written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int QueryOverlaps(in Aabb3D box, Span<SpatialItemId> results)
    {
        EnterOperation();
        try
        {
            if (_rootIndex.IsNull)
            {
                return 0;
            }

            Span<NodeIndex> stack = stackalloc NodeIndex[128];
            int stackPtr = 0;
            stack[stackPtr++] = _rootIndex;

            int count = 0;
            int maxResults = results.Length;

            while (stackPtr > 0)
            {
                NodeIndex nodeIndex = stack[--stackPtr];
                if (nodeIndex.IsNull)
                {
                    continue;
                }

                ref Node node = ref GetNodeRef(nodeIndex);
                TMetrics.OnNodeVisited();

                if (node.Box.Overlaps(in box))
                {
                    TMetrics.OnIntersectionTested(true);
                    if (node.IsLeaf())
                    {
                        if ((uint)count < (uint)maxResults)
                        {
                            results[count++] = node.ItemId;
                        }
                        else
                        {
                            return count;
                        }
                    }
                    else
                    {
                        stack[stackPtr++] = node.LeftChild;
                        stack[stackPtr++] = node.RightChild;
                    }
                }
                else
                {
                    TMetrics.OnIntersectionTested(false);
                }
            }

            return count;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Reports every item overlapping <paramref name="box"/> to a visitor;
    /// a <see langword="false"/> verdict stops the traversal early.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int QueryOverlaps<TVisitor, TContext>(in Aabb3D box, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, ISpatialVisitor<TUserData, TContext>
        where TContext : allows ref struct
    {
        EnterOperation();
        try
        {
            if (_rootIndex.IsNull)
            {
                return 0;
            }

            Span<NodeIndex> stack = stackalloc NodeIndex[128];
            int stackPtr = 0;
            stack[stackPtr++] = _rootIndex;

            int hitCount = 0;

            while (stackPtr > 0)
            {
                NodeIndex nodeIndex = stack[--stackPtr];
                if (nodeIndex.IsNull)
                {
                    continue;
                }

                ref Node node = ref GetNodeRef(nodeIndex);
                TMetrics.OnNodeVisited();

                if (node.Box.Overlaps(in box))
                {
                    TMetrics.OnIntersectionTested(true);
                    if (node.IsLeaf())
                    {
                        hitCount++;
                        TUserData data = _userData[(uint)node.ItemId.Value]!;
                        if (!visitor.OnOverlap(node.ItemId, data, in node.Box, ref context))
                        {
                            break;
                        }
                    }
                    else
                    {
                        stack[stackPtr++] = node.LeftChild;
                        stack[stackPtr++] = node.RightChild;
                    }
                }
                else
                {
                    TMetrics.OnIntersectionTested(false);
                }
            }

            return hitCount;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Exposes the overlap scan as an allocation-free enumerable over caller-owned
    /// stack storage; the tree is held for the enumeration lifetime.
    /// </summary>
    /// <param name="queryBox">World bounds to test against the stored fat boxes.</param>
    /// <param name="stackBuffer">Caller-owned traversal storage.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OverlapEnumerable<TUserData, TStrategy, TMetrics> EnumerateOverlaps(in Aabb3D queryBox, Span<NodeIndex> stackBuffer)
    {
        return new OverlapEnumerable<TUserData, TStrategy, TMetrics>(this, in queryBox, stackBuffer);
    }

    /// <summary>
    /// Casts <paramref name="ray"/> against the tree and writes one hit per
    /// intersected leaf; excess hits are dropped once the span is full.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int RayCast(in Ray3D ray, Span<RayHit> results, float maxDistance = float.PositiveInfinity)
    {
        EnterOperation();
        try
        {
            if (_rootIndex.IsNull)
            {
                return 0;
            }

            Span<NodeIndex> stack = stackalloc NodeIndex[128];
            int stackPtr = 0;
            stack[stackPtr++] = _rootIndex;

            int count = 0;
            int maxResults = results.Length;

            while (stackPtr > 0)
            {
                NodeIndex nodeIndex = stack[--stackPtr];
                if (nodeIndex.IsNull)
                {
                    continue;
                }

                ref Node node = ref GetNodeRef(nodeIndex);
                TMetrics.OnNodeVisited();

                if (node.Box.Intersects(in ray, maxDistance, out float dist))
                {
                    TMetrics.OnIntersectionTested(true);
                    if (node.IsLeaf())
                    {
                        if ((uint)count < (uint)maxResults)
                        {
                            results[count++] = new RayHit(node.ItemId, dist);
                        }
                        else
                        {
                            return count;
                        }
                    }
                    else
                    {
                        stack[stackPtr++] = node.LeftChild;
                        stack[stackPtr++] = node.RightChild;
                    }
                }
                else
                {
                    TMetrics.OnIntersectionTested(false);
                }
            }

            return count;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Casts <paramref name="ray"/> against the tree, letting the visitor shrink
    /// <paramref name="maxDistance"/> to prune farther subtrees as hits arrive.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void RayCast<TVisitor, TContext>(in Ray3D ray, float maxDistance, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, IRayHitVisitor<TUserData, TContext>
        where TContext : allows ref struct
    {
        EnterOperation();
        try
        {
            if (_rootIndex.IsNull)
            {
                return;
            }

            Span<NodeIndex> stack = stackalloc NodeIndex[128];
            int stackPtr = 0;
            stack[stackPtr++] = _rootIndex;
            float currentMaxDist = maxDistance;

            while (stackPtr > 0)
            {
                NodeIndex nodeIndex = stack[--stackPtr];
                if (nodeIndex.IsNull)
                {
                    continue;
                }

                ref Node node = ref GetNodeRef(nodeIndex);

                if (!node.Box.Intersects(in ray, currentMaxDist, out _))
                {
                    continue;
                }

                if (node.IsLeaf())
                {
                    TUserData data = _userData[(uint)node.ItemId.Value]!;
                    if (!visitor.OnHit(node.ItemId, data, in ray, ref currentMaxDist, ref context))
                    {
                        return;
                    }
                }
                else
                {
                    stack[stackPtr++] = node.LeftChild;
                    stack[stackPtr++] = node.RightChild;
                }
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Reports every item inside <paramref name="frustum"/>; fully contained
    /// subtrees are drained without per-box plane tests.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void FrustumCull<TVisitor, TContext>(in Frustum3 frustum, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, IFrustumVisitor<TUserData, TContext>
        where TContext : allows ref struct
    {
        EnterOperation();
        try
        {
            if (_rootIndex.IsNull)
            {
                return;
            }

            Span<NodeIndex> stack = stackalloc NodeIndex[128];
            int stackPtr = 0;
            stack[stackPtr++] = _rootIndex;

            while (stackPtr > 0)
            {
                NodeIndex nodeIndex = stack[--stackPtr];
                if (nodeIndex.IsNull)
                {
                    continue;
                }

                ref Node node = ref GetNodeRef(nodeIndex);
                ContainmentType containment = frustum.Contains(in node.Box);

                if (containment == ContainmentType.Disjoint)
                {
                    continue;
                }

                if (containment == ContainmentType.Contains)
                {
                    PushAllLeaves(nodeIndex, ref visitor, ref context);
                    continue;
                }

                if (node.IsLeaf())
                {
                    TUserData data = _userData[(uint)node.ItemId.Value]!;
                    if (!visitor.OnVisible(node.ItemId, data, in node.Box, ref context))
                    {
                        return;
                    }
                }
                else
                {
                    stack[stackPtr++] = node.LeftChild;
                    stack[stackPtr++] = node.RightChild;
                }
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    private void PushAllLeaves<TVisitor, TContext>(NodeIndex rootNode, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, IFrustumVisitor<TUserData, TContext>
        where TContext : allows ref struct
    {
        Span<NodeIndex> stack = stackalloc NodeIndex[64];
        int stackPtr = 0;
        stack[stackPtr++] = rootNode;

        while (stackPtr > 0)
        {
            NodeIndex current = stack[--stackPtr];
            ref Node node = ref GetNodeRef(current);

            if (node.IsLeaf())
            {
                TUserData data = _userData[(uint)node.ItemId.Value]!;
                if (!visitor.OnVisible(node.ItemId, data, in node.Box, ref context))
                {
                    return;
                }
            }
            else
            {
                stack[stackPtr++] = node.LeftChild;
                stack[stackPtr++] = node.RightChild;
            }
        }
    }

    /// <summary>
    /// Reports each pair of overlapping items once, descending the larger
    /// subtree first so the traversal stays balanced.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void FindPairs<TPairVisitor, TContext>(ref TPairVisitor visitor, ref TContext context)
        where TPairVisitor : struct, IPairVisitor<TUserData, TContext>
        where TContext : allows ref struct
    {
        EnterOperation();
        try
        {
            if (_rootIndex.IsNull || GetNodeRef(_rootIndex).IsLeaf())
            {
                return;
            }

            Span<NodePair> stack = stackalloc NodePair[256];
            int stackCount = 0;
            stack[stackCount++] = new NodePair(_rootIndex, _rootIndex);

            while (stackCount > 0)
            {
                NodePair pair = stack[--stackCount];

                if (pair.A == pair.B)
                {
                    ref Node self = ref GetNodeRef(pair.A);
                    if (!self.IsLeaf())
                    {
                        stack[stackCount++] = new NodePair(self.LeftChild, self.LeftChild);
                        stack[stackCount++] = new NodePair(self.RightChild, self.RightChild);
                        stack[stackCount++] = new NodePair(self.LeftChild, self.RightChild);
                    }
                    continue;
                }

                ref Node nodeA = ref GetNodeRef(pair.A);
                ref Node nodeB = ref GetNodeRef(pair.B);

                if (!nodeA.Box.Overlaps(in nodeB.Box))
                {
                    continue;
                }

                if (nodeA.IsLeaf() && nodeB.IsLeaf())
                {
                    TUserData dataA = _userData[(uint)nodeA.ItemId.Value]!;
                    TUserData dataB = _userData[(uint)nodeB.ItemId.Value]!;
                    if (!visitor.OnPair(nodeA.ItemId, dataA, nodeB.ItemId, dataB, ref context))
                    {
                        return;
                    }
                }
                else if (nodeA.IsLeaf())
                {
                    stack[stackCount++] = new NodePair(pair.A, nodeB.LeftChild);
                    stack[stackCount++] = new NodePair(pair.A, nodeB.RightChild);
                }
                else if (nodeB.IsLeaf())
                {
                    stack[stackCount++] = new NodePair(nodeA.LeftChild, pair.B);
                    stack[stackCount++] = new NodePair(nodeA.RightChild, pair.B);
                }
                else
                {
                    if (nodeA.Box.SurfaceArea > nodeB.Box.SurfaceArea)
                    {
                        stack[stackCount++] = new NodePair(nodeA.LeftChild, pair.B);
                        stack[stackCount++] = new NodePair(nodeA.RightChild, pair.B);
                    }
                    else
                    {
                        stack[stackCount++] = new NodePair(pair.A, nodeB.LeftChild);
                        stack[stackCount++] = new NodePair(pair.A, nodeB.RightChild);
                    }
                }
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Writes up to <paramref name="results"/>.Length nearest items to
    /// <paramref name="point"/> in ascending squared-distance order.
    /// </summary>
    /// <param name="point">The query point.</param>
    /// <param name="results">Destination span; a short span yields the nearest subset.</param>
    /// <param name="maxDistance">Neighbors past this distance are ignored.</param>
    /// <returns>Number of neighbors written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int QueryKNearest(Vec3 point, Span<KnnResult<TUserData>> results, float maxDistance = float.PositiveInfinity)
    {
        EnterOperation();
        try
        {
            if (_rootIndex.IsNull || results.IsEmpty)
            {
                return 0;
            }

            float maxDistSq = float.IsPositiveInfinity(maxDistance) ? float.PositiveInfinity : maxDistance * maxDistance;
            int heapCount = 0;

            Span<NodeIndex> stack = stackalloc NodeIndex[128];
            int stackPtr = 0;
            stack[stackPtr++] = _rootIndex;

            while (stackPtr > 0)
            {
                NodeIndex nodeIndex = stack[--stackPtr];
                if (nodeIndex.IsNull)
                {
                    continue;
                }

                ref Node node = ref GetNodeRef(nodeIndex);
                float nodeDistSq = node.Box.DistanceSquared(in point);

                if (nodeDistSq > maxDistSq)
                {
                    continue;
                }

                if (node.IsLeaf())
                {
                    TUserData data = _userData[(uint)node.ItemId.Value]!;
                    KnnResult<TUserData> item = new(node.ItemId, data, nodeDistSq);

                    if (heapCount < results.Length)
                    {
                        PushMaxHeap(results, ref heapCount, in item);
                        if (heapCount == results.Length)
                        {
                            maxDistSq = results[0].DistanceSq;
                        }
                    }
                    else if (nodeDistSq < results[0].DistanceSq)
                    {
                        results[0] = item;
                        HeapifyDown(results, heapCount, 0);
                        maxDistSq = results[0].DistanceSq;
                    }
                }
                else
                {
                    float dist1 = GetNodeRef(node.LeftChild).Box.DistanceSquared(in point);
                    float dist2 = GetNodeRef(node.RightChild).Box.DistanceSquared(in point);

                    NodeIndex closeChild = dist1 < dist2 ? node.LeftChild : node.RightChild;
                    NodeIndex farChild = dist1 < dist2 ? node.RightChild : node.LeftChild;

                    stack[stackPtr++] = farChild;
                    stack[stackPtr++] = closeChild;
                }
            }

            SortMaxHeapToAscending(results, heapCount);
            return heapCount;
        }
        finally
        {
            ExitOperation();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushMaxHeap(Span<KnnResult<TUserData>> heap, ref int count, in KnnResult<TUserData> item)
    {
        int i = count++;
        heap[i] = item;

        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (heap[i].DistanceSq <= heap[parent].DistanceSq)
            {
                break;
            }

            (heap[i], heap[parent]) = (heap[parent], heap[i]);
            i = parent;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HeapifyDown(Span<KnnResult<TUserData>> heap, int count, int index)
    {
        while (true)
        {
            int largest = index;
            int left = (index << 1) + 1;
            int right = left + 1;

            if (left < count && heap[left].DistanceSq > heap[largest].DistanceSq)
            {
                largest = left;
            }

            if (right < count && heap[right].DistanceSq > heap[largest].DistanceSq)
            {
                largest = right;
            }

            if (largest == index)
            {
                break;
            }

            (heap[index], heap[largest]) = (heap[largest], heap[index]);
            index = largest;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SortMaxHeapToAscending(Span<KnnResult<TUserData>> heap, int count)
    {
        for (int i = count - 1; i > 0; i--)
        {
            (heap[0], heap[i]) = (heap[i], heap[0]);
            HeapifyDown(heap, i, 0);
        }
    }
}
