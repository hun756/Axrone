namespace Axrone.Raycast;

using System.Runtime.ExceptionServices;

/// <summary>
/// Concurrency shield of a BVH pipeline: traversal and hit counters plus the
/// in-flight gauge and lifecycle word, each on its own cache line.
/// Namespace-level because the CLR forbids explicit layout on types nested in
/// a generic class.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 256)]
internal struct BvhConcurrencyShield
{
    /// <summary>Traversals started.</summary>
    [FieldOffset(0)]
    public long TraversalCount;

    /// <summary>Contacts confirmed.</summary>
    [FieldOffset(64)]
    public long HitConfirmationCount;

    /// <summary>Traversals currently inside the engine.</summary>
    [FieldOffset(128)]
    public int InFlightQueries;

    /// <summary>Lifecycle word.</summary>
    [FieldOffset(192)]
    public int Status;
}

/// <summary>
/// Built linear BVH over one primitive soup: SAH-constructed node, index and
/// primitive pools in aligned native memory, policy-driven traversal
/// (closest, any-hit, collector, lazy enumeration), batch queries, and a
/// drain/dispose lifecycle with live operation counters.
/// </summary>
/// <typeparam name="TPrimitive">Primitive type stored in the hierarchy.</typeparam>
public sealed partial class BvhPipeline<TPrimitive> :
    IRaycaster<TPrimitive>,
    IBatchRaycaster<TPrimitive>,
    ISpatialLifecycle,
    IDisposable,
    IAsyncDisposable,
    IHierarchyMetrics
    where TPrimitive : unmanaged, IPrimitiveGeometry<TPrimitive>
{
    private const int StateActive = 1;
    private const int StateDraining = 2;
    private const int StateDisposed = 3;
    private const int StateFaulted = 4;

    private struct SahBin
    {
        public Aabb3D Bounds;
        public int PrimitiveCount;
    }

    private BvhConcurrencyShield _metrics;
    private readonly Lock _lifecycleLock = new();

    private readonly AlignedMemoryBlock<LinearBvhNode> _nodes;
    private readonly AlignedMemoryBlock<int> _indices;
    private readonly AlignedMemoryBlock<TPrimitive> _primitives;

    private readonly int _nodeCount;
    private readonly int _primitiveCount;
    private ExceptionDispatchInfo? _fatalError;
    private int _disposalFlag;

    /// <inheritdoc/>
    public long TotalRays => Volatile.Read(ref _metrics.TraversalCount);

    /// <inheritdoc/>
    public long TotalHits => Volatile.Read(ref _metrics.HitConfirmationCount);

    /// <inheritdoc/>
    public int ActiveQueries => Volatile.Read(ref _metrics.InFlightQueries);

    /// <inheritdoc/>
    public bool IsOperational => Volatile.Read(ref _metrics.Status) == StateActive;

    /// <inheritdoc/>
    public int NodeCount => _nodeCount;

    /// <inheritdoc/>
    public int PrimitiveCount => _primitiveCount;

    private BvhPipeline(
        AlignedMemoryBlock<LinearBvhNode> nodes,
        AlignedMemoryBlock<int> indices,
        AlignedMemoryBlock<TPrimitive> primitives,
        int nodeCount,
        int primitiveCount)
    {
        _nodes = nodes;
        _indices = indices;
        _primitives = primitives;
        _nodeCount = nodeCount;
        _primitiveCount = primitiveCount;
        _metrics.Status = StateActive;
    }

    internal static BvhPipeline<TPrimitive> Construct(
        ReadOnlySpan<TPrimitive> primitives,
        int maxLeafCapacity,
        int sahBinCount)
    {
        int count = primitives.Length;

        AlignedMemoryBlock<TPrimitive>? primBlock = null;
        AlignedMemoryBlock<int>? indexBlock = null;
        AlignedMemoryBlock<LinearBvhNode>? nodeBlock = null;

        try
        {
            primBlock = new AlignedMemoryBlock<TPrimitive>(count);
            indexBlock = new AlignedMemoryBlock<int>(count);

            Span<TPrimitive> primSpan = primBlock.AsSpan();
            Span<int> indexSpan = indexBlock.AsSpan();

            primitives.CopyTo(primSpan);
            for (int i = 0; i < count; i++)
            {
                indexSpan[i] = i;
            }

            Span<Aabb3D> bounds = count <= 2048
                ? stackalloc Aabb3D[count]
                : new Aabb3D[count];

            Span<Vec3> centroids = count <= 2048
                ? stackalloc Vec3[count]
                : new Vec3[count];

            for (int i = 0; i < count; i++)
            {
                bounds[i] = TPrimitive.ComputeBounds(in primSpan[i]);
                centroids[i] = TPrimitive.ComputeCentroid(in primSpan[i]);
            }

            int estimatedMaxNodes = Math.Max(4, (count * 2) + 4);
            nodeBlock = new AlignedMemoryBlock<LinearBvhNode>(estimatedMaxNodes);

            int allocatedNodes = 2;
            Span<LinearBvhNode> nodeSpan = nodeBlock.AsSpan();

            BuildSubtree(
                nodeIndex: 0,
                start: 0,
                count: count,
                indexSpan,
                primSpan,
                bounds,
                centroids,
                ref allocatedNodes,
                nodeSpan,
                maxLeafCapacity,
                sahBinCount);

            BvhPipeline<TPrimitive> pipeline = new(
                nodeBlock,
                indexBlock,
                primBlock,
                allocatedNodes,
                count);

            nodeBlock = null;
            indexBlock = null;
            primBlock = null;

            return pipeline;
        }
        finally
        {
            nodeBlock?.Dispose();
            indexBlock?.Dispose();
            primBlock?.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HalfArea(in Aabb3D box) => box.SurfaceArea() * 0.5f;

    private static void BuildSubtree(
        int nodeIndex,
        int start,
        int count,
        Span<int> indices,
        ReadOnlySpan<TPrimitive> primitives,
        ReadOnlySpan<Aabb3D> bounds,
        ReadOnlySpan<Vec3> centroids,
        ref int allocatedNodes,
        Span<LinearBvhNode> nodes,
        int maxLeafCapacity,
        int sahBinCount)
    {
        Aabb3D nodeBounds = bounds[indices[start]];
        Aabb3D centroidBounds = new(centroids[indices[start]], centroids[indices[start]]);

        for (int i = start + 1; i < start + count; i++)
        {
            int index = indices[i];
            nodeBounds = Aabb3D.CreateMerged(in nodeBounds, in bounds[index]);
            Aabb3D centroidPoint = new(centroids[index], centroids[index]);
            centroidBounds = Aabb3D.CreateMerged(in centroidBounds, in centroidPoint);
        }

        if (count <= maxLeafCapacity)
        {
            nodes[nodeIndex] = new LinearBvhNode(in nodeBounds, new NodeIndex(start), count);
            return;
        }

        Vec3 size = centroidBounds.Size;
        int axis = 0;
        if (size.Y > size.X && size.Y > size.Z)
        {
            axis = 1;
        }
        else if (size.Z > size.X)
        {
            axis = 2;
        }

        float maxExtent = axis switch
        {
            0 => size.X,
            1 => size.Y,
            _ => size.Z,
        };

        if (maxExtent < 1e-6f)
        {
            nodes[nodeIndex] = new LinearBvhNode(in nodeBounds, new NodeIndex(start), count);
            return;
        }

        Span<SahBin> bins = stackalloc SahBin[sahBinCount];
        for (int i = 0; i < sahBinCount; i++)
        {
            bins[i].Bounds = Aabb3D.Empty;
            bins[i].PrimitiveCount = 0;
        }

        float axisMin = axis switch
        {
            0 => centroidBounds.Min.X,
            1 => centroidBounds.Min.Y,
            _ => centroidBounds.Min.Z,
        };

        float scale = sahBinCount / maxExtent;

        for (int i = start; i < start + count; i++)
        {
            int index = indices[i];
            float value = GetCoordinate(centroids[index], axis);
            int binIndex = (int)((value - axisMin) * scale);
            binIndex = Math.Clamp(binIndex, 0, sahBinCount - 1);
            bins[binIndex].PrimitiveCount++;
            Aabb3D binBounds = bins[binIndex].Bounds;
            bins[binIndex].Bounds = Aabb3D.CreateMerged(in binBounds, in bounds[index]);
        }

        Span<Aabb3D> leftBounds = stackalloc Aabb3D[sahBinCount];
        Span<int> leftCounts = stackalloc int[sahBinCount];
        Aabb3D currentLeftBound = Aabb3D.Empty;
        int currentLeftCount = 0;

        for (int i = 0; i < sahBinCount; i++)
        {
            currentLeftCount += bins[i].PrimitiveCount;
            currentLeftBound = Aabb3D.CreateMerged(in currentLeftBound, in bins[i].Bounds);
            leftCounts[i] = currentLeftCount;
            leftBounds[i] = currentLeftBound;
        }

        Aabb3D currentRightBound = Aabb3D.Empty;
        int currentRightCount = 0;
        float bestCost = float.PositiveInfinity;
        int bestSplitBin = -1;
        float parentHalfArea = HalfArea(in nodeBounds);

        for (int i = sahBinCount - 1; i > 0; i--)
        {
            currentRightCount += bins[i].PrimitiveCount;
            currentRightBound = Aabb3D.CreateMerged(in currentRightBound, in bins[i].Bounds);

            int countLeft = leftCounts[i - 1];
            int countRight = currentRightCount;
            if (countLeft == 0 || countRight == 0)
            {
                continue;
            }

            float areaLeft = HalfArea(in leftBounds[i - 1]);
            float areaRight = HalfArea(in currentRightBound);

            float cost = 1.0f + (((areaLeft * countLeft) + (areaRight * countRight)) / parentHalfArea);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestSplitBin = i - 1;
            }
        }

        int mid;
        if (bestCost >= count && count <= (maxLeafCapacity * 2))
        {
            nodes[nodeIndex] = new LinearBvhNode(in nodeBounds, new NodeIndex(start), count);
            return;
        }

        if (bestSplitBin != -1)
        {
            int leftIter = start;
            int rightIter = (start + count) - 1;

            while (leftIter <= rightIter)
            {
                int index = indices[leftIter];
                float value = GetCoordinate(centroids[index], axis);
                int binIndex = Math.Clamp((int)((value - axisMin) * scale), 0, sahBinCount - 1);

                if (binIndex <= bestSplitBin)
                {
                    leftIter++;
                }
                else
                {
                    (indices[leftIter], indices[rightIter]) = (indices[rightIter], indices[leftIter]);
                    rightIter--;
                }
            }

            mid = leftIter;
            if (mid == start || mid == start + count)
            {
                mid = start + (count / 2);
                QuickPartition(indices, centroids, start, (start + count) - 1, mid, axis);
            }
        }
        else
        {
            mid = start + (count / 2);
            QuickPartition(indices, centroids, start, (start + count) - 1, mid, axis);
        }

        int leftCount = mid - start;
        int rightCount = count - leftCount;

        int leftChild = allocatedNodes;
        int rightChild = allocatedNodes + 1;
        allocatedNodes += 2;

        BuildSubtree(leftChild, start, leftCount, indices, primitives, bounds, centroids, ref allocatedNodes, nodes, maxLeafCapacity, sahBinCount);
        BuildSubtree(rightChild, mid, rightCount, indices, primitives, bounds, centroids, ref allocatedNodes, nodes, maxLeafCapacity, sahBinCount);

        nodes[nodeIndex] = new LinearBvhNode(in nodeBounds, new NodeIndex(leftChild), 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetCoordinate(Vec3 value, int axis) => axis switch
    {
        0 => value.X,
        1 => value.Y,
        _ => value.Z,
    };

    private static void QuickPartition(
        Span<int> indices,
        ReadOnlySpan<Vec3> centroids,
        int left,
        int right,
        int rank,
        int axis)
    {
        while (left < right)
        {
            int pivotIndex = PartitionAroundPivot(indices, centroids, left, right, axis);
            if (pivotIndex == rank)
            {
                return;
            }
            if (pivotIndex < rank)
            {
                left = pivotIndex + 1;
            }
            else
            {
                right = pivotIndex - 1;
            }
        }
    }

    private static int PartitionAroundPivot(
        Span<int> indices,
        ReadOnlySpan<Vec3> centroids,
        int left,
        int right,
        int axis)
    {
        int mid = left + ((right - left) / 2);
        int pivotElement = indices[mid];
        float pivotScalar = GetCoordinate(centroids[pivotElement], axis);

        (indices[mid], indices[right]) = (indices[right], indices[mid]);

        int store = left;
        for (int i = left; i < right; i++)
        {
            if (GetCoordinate(centroids[indices[i]], axis) < pivotScalar)
            {
                (indices[i], indices[store]) = (indices[store], indices[i]);
                store++;
            }
        }
        (indices[store], indices[right]) = (indices[right], indices[store]);
        return store;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe RayQueryResult Raycast(in Ray ray)
    {
        NullTraversalObserver observer = default;
        bool hasHit = Traverse<ClosestHitPolicy, NullTraversalObserver>(in ray, out PrimitiveHit hit, ref observer);
        return hasHit ? RayQueryResult.HitResult(in hit) : RayQueryResult.Miss();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool RaycastAny(in Ray ray)
    {
        NullTraversalObserver observer = default;
        return Traverse<AnyHitPolicy, NullTraversalObserver>(in ray, out _, ref observer);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe RayHitEnumerator<TPrimitive> EnumerateHits(in Ray ray)
    {
        int state = Volatile.Read(ref _metrics.Status);
        if (state != StateActive)
        {
            VerifyStatePermitsTraversal(state);
        }

        return new RayHitEnumerator<TPrimitive>(
            _nodes.Pointer,
            _indices.Pointer,
            _primitives.Pointer,
            in ray);
    }

    /// <inheritdoc/>
    public void RaycastBatch(ReadOnlySpan<Ray> rays, Span<RayQueryResult> results)
    {
        if ((uint)results.Length < (uint)rays.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(results));
        }

        ref Ray rayRef = ref MemoryMarshal.GetReference(rays);
        ref RayQueryResult resultRef = ref MemoryMarshal.GetReference(results);
        nuint length = (nuint)rays.Length;

        for (nuint i = 0; i < length; i++)
        {
            Unsafe.Add(ref resultRef, i) = Raycast(in Unsafe.Add(ref rayRef, i));
        }
    }

    /// <inheritdoc/>
    public void RaycastBatchAny(ReadOnlySpan<Ray> rays, Span<bool> results)
    {
        if ((uint)results.Length < (uint)rays.Length)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(results));
        }

        ref Ray rayRef = ref MemoryMarshal.GetReference(rays);
        ref bool resultRef = ref MemoryMarshal.GetReference(results);
        nuint length = (nuint)rays.Length;

        for (nuint i = 0; i < length; i++)
        {
            Unsafe.Add(ref resultRef, i) = RaycastAny(in Unsafe.Add(ref rayRef, i));
        }
    }

    /// <summary>
    /// Policy-driven traversal: prunes by enter distance, folds primitive
    /// contacts through <typeparamref name="TPolicy"/>, and reports through
    /// <paramref name="observer"/>. Holds an in-flight count for the walk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool Traverse<TPolicy, TObserver>(
        in Ray ray,
        out PrimitiveHit resultHit,
        ref TObserver observer)
        where TPolicy : struct, IRayTraversalPolicy<TPolicy>
        where TObserver : struct, ITraversalObserver<TObserver>, allows ref struct
    {
        int state = Volatile.Read(ref _metrics.Status);
        if (state != StateActive)
        {
            VerifyStatePermitsTraversal(state);
        }

        Interlocked.Increment(ref _metrics.InFlightQueries);
        Interlocked.Increment(ref _metrics.TraversalCount);

        try
        {
            LinearBvhNode* nodeArray = _nodes.Pointer;
            int* indexArray = _indices.Pointer;
            TPrimitive* primitiveArray = _primitives.Pointer;

            TraversalStack64 stack = default;
            int stackIndex = 0;
            stack[stackIndex++] = 0;

            int nodesSeen = 0;
            int primsTested = 0;
            PrimitiveHit bestHit = PrimitiveHit.None;
            Distance maxDistance = ray.MaxDistance;

            while (stackIndex > 0)
            {
                int current = stack[--stackIndex];
                nodesSeen++;
                LinearBvhNode* nodePtr = nodeArray + current;

                if (!nodePtr->Intersects(in ray, out Distance enterDistance) || TPolicy.ShouldPrune(enterDistance, maxDistance))
                {
                    continue;
                }

                if (nodePtr->IsLeaf)
                {
                    int startOffset = nodePtr->ChildOrIndex.Value;
                    int count = nodePtr->PrimitiveCount;

                    for (int i = 0; i < count; i++)
                    {
                        primsTested++;
                        int primitiveId = indexArray[startOffset + i];
                        if (TPrimitive.TryIntersect(in ray, in primitiveArray[primitiveId], out PrimitiveHit candidateHit))
                        {
                            if (candidateHit.Distance <= maxDistance)
                            {
                                if (!TPolicy.OnPrimitiveHit(ref bestHit, ref maxDistance, in candidateHit))
                                {
                                    Interlocked.Increment(ref _metrics.HitConfirmationCount);
                                    observer.OnTraversalComplete(nodesSeen, primsTested, true);
                                    resultHit = bestHit;
                                    return true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    int leftChild = nodePtr->ChildOrIndex.Value;
                    int rightChild = leftChild + 1;

                    LinearBvhNode* leftPtr = nodeArray + leftChild;
                    LinearBvhNode* rightPtr = nodeArray + rightChild;

                    bool hitLeft = leftPtr->Intersects(in ray, out Distance leftDistance) && !TPolicy.ShouldPrune(leftDistance, maxDistance);
                    bool hitRight = rightPtr->Intersects(in ray, out Distance rightDistance) && !TPolicy.ShouldPrune(rightDistance, maxDistance);

                    if (hitLeft & hitRight)
                    {
                        if (leftDistance < rightDistance)
                        {
                            stack[stackIndex++] = rightChild;
                            stack[stackIndex++] = leftChild;
                        }
                        else
                        {
                            stack[stackIndex++] = leftChild;
                            stack[stackIndex++] = rightChild;
                        }
                    }
                    else if (hitLeft)
                    {
                        stack[stackIndex++] = leftChild;
                    }
                    else if (hitRight)
                    {
                        stack[stackIndex++] = rightChild;
                    }
                }
            }

            bool hasHit = bestHit.IsHit;
            if (hasHit)
            {
                Interlocked.Increment(ref _metrics.HitConfirmationCount);
            }

            observer.OnTraversalComplete(nodesSeen, primsTested, hasHit);
            resultHit = bestHit;
            return hasHit;
        }
        finally
        {
            Interlocked.Decrement(ref _metrics.InFlightQueries);
        }
    }

    /// <summary>
    /// Collector traversal: buffers contacts into <paramref name="collector"/>
    /// instead of reducing to one hit. Holds an in-flight count for the walk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool TraverseWithCollector<TCollector, TObserver>(
        in Ray ray,
        ref TCollector collector,
        ref TObserver observer)
        where TCollector : ITraversalCollector<TCollector>, allows ref struct
        where TObserver : ITraversalObserver<TObserver>, allows ref struct
    {
        int state = Volatile.Read(ref _metrics.Status);
        if (state != StateActive)
        {
            VerifyStatePermitsTraversal(state);
        }

        Interlocked.Increment(ref _metrics.InFlightQueries);
        Interlocked.Increment(ref _metrics.TraversalCount);

        try
        {
            LinearBvhNode* nodeArray = _nodes.Pointer;
            int* indexArray = _indices.Pointer;
            TPrimitive* primitiveArray = _primitives.Pointer;

            TraversalStack64 stack = default;
            int stackIndex = 0;
            stack[stackIndex++] = 0;

            int nodesSeen = 0;
            int primsTested = 0;

            while (stackIndex > 0)
            {
                int current = stack[--stackIndex];
                nodesSeen++;
                LinearBvhNode* nodePtr = nodeArray + current;

                if (!nodePtr->Intersects(in ray, out Distance enterDistance) || enterDistance > collector.MaxDistance)
                {
                    continue;
                }

                if (nodePtr->IsLeaf)
                {
                    int startOffset = nodePtr->ChildOrIndex.Value;
                    int count = nodePtr->PrimitiveCount;

                    for (int i = 0; i < count; i++)
                    {
                        primsTested++;
                        int primitiveId = indexArray[startOffset + i];
                        if (TPrimitive.TryIntersect(in ray, in primitiveArray[primitiveId], out PrimitiveHit hit))
                        {
                            if (hit.Distance <= collector.MaxDistance)
                            {
                                if (!collector.OnHit(in hit))
                                {
                                    Interlocked.Increment(ref _metrics.HitConfirmationCount);
                                    observer.OnTraversalComplete(nodesSeen, primsTested, true);
                                    return true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    int leftChild = nodePtr->ChildOrIndex.Value;
                    int rightChild = leftChild + 1;

                    LinearBvhNode* leftPtr = nodeArray + leftChild;
                    LinearBvhNode* rightPtr = nodeArray + rightChild;

                    bool hitLeft = leftPtr->Intersects(in ray, out Distance leftDistance) && (leftDistance <= collector.MaxDistance);
                    bool hitRight = rightPtr->Intersects(in ray, out Distance rightDistance) && (rightDistance <= collector.MaxDistance);

                    if (hitLeft & hitRight)
                    {
                        if (leftDistance < rightDistance)
                        {
                            stack[stackIndex++] = rightChild;
                            stack[stackIndex++] = leftChild;
                        }
                        else
                        {
                            stack[stackIndex++] = leftChild;
                            stack[stackIndex++] = rightChild;
                        }
                    }
                    else if (hitLeft)
                    {
                        stack[stackIndex++] = leftChild;
                    }
                    else if (hitRight)
                    {
                        stack[stackIndex++] = rightChild;
                    }
                }
            }

            bool hitRegistered = collector.HasHit;
            if (hitRegistered)
            {
                Interlocked.Increment(ref _metrics.HitConfirmationCount);
            }

            observer.OnTraversalComplete(nodesSeen, primsTested, hitRegistered);
            return hitRegistered;
        }
        finally
        {
            Interlocked.Decrement(ref _metrics.InFlightQueries);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void VerifyStatePermitsTraversal(int state)
    {
        if (state == StateFaulted)
        {
            _fatalError?.Throw();
        }
        if (state is StateDisposed or StateDraining)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(BvhPipeline<TPrimitive>));
        }
    }

    /// <inheritdoc/>
    public void Complete(Exception? error = null)
    {
        if (error != null)
        {
            _fatalError = ExceptionDispatchInfo.Capture(error);
            Interlocked.Exchange(ref _metrics.Status, StateFaulted);
        }
        else
        {
            Interlocked.CompareExchange(ref _metrics.Status, StateDraining, StateActive);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        Complete();

        while (Volatile.Read(ref _metrics.InFlightQueries) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposalFlag, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _metrics.Status, StateDisposed);

        SpinWait spinner = default;
        while (Volatile.Read(ref _metrics.InFlightQueries) > 0)
        {
            spinner.SpinOnce();
        }

        lock (_lifecycleLock)
        {
            _nodes.Dispose();
            _indices.Dispose();
            _primitives.Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposalFlag, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _metrics.Status, StateDisposed);

        while (Volatile.Read(ref _metrics.InFlightQueries) > 0)
        {
            await Task.Yield();
        }

        lock (_lifecycleLock)
        {
            _nodes.Dispose();
            _indices.Dispose();
            _primitives.Dispose();
        }
    }
}
