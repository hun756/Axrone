namespace Axrone.Geometry;

using Axrone.Utility.Backoff;

/// <summary>
/// Query half of the point quad tree: box range scans behind a sequence lock
/// with a locked retry fallback, best-first nearest search, and an
/// allocation-free range enumerable over the live leaves.
/// </summary>
public sealed partial class QuadTree<T, TBackoff, TMetrics>
    where T : unmanaged
    where TBackoff : struct, Axrone.Utility.Backoff.ISpinBackoff
    where TMetrics : struct, IQuadMetricsSink
{
    /// <inheritdoc/>
    public void QueryRange<TVisitor>(in Aabb2D range, ref TVisitor visitor)
        where TVisitor : IQuadVisitor<T>, allows ref struct
    {
        EnterOperation();
        try
        {
            ThrowIfDisposedOrFaulted();
            int spinCount = 0;

            while (true)
            {
                long version = Volatile.Read(ref _state.Version);
                if ((version & 1L) == 0L)
                {
                    QuadNode[] localNodes = Volatile.Read(ref _nodes);
                    QuadElementEntry<T>[] localElements = Volatile.Read(ref _elements);
                    int foundCount = 0;

                    if (ExecuteOptimisticRange(localNodes, localElements, in range, ref visitor, ref foundCount))
                    {
                        long version2 = Volatile.Read(ref _state.Version);
                        if (version == version2)
                        {
                            TMetrics.RecordQuery(foundCount);
                            return;
                        }
                    }
                }

                visitor.Reset();
                TBackoff.Advance(ref spinCount);

                if (spinCount > MaxSpinBeforeLock)
                {
                    lock (_syncLock)
                    {
                        ThrowIfDisposedOrFaulted();
                        visitor.Reset();
                        int foundCount = 0;
                        ExecuteRangeUnderLock(in range, ref visitor, ref foundCount);
                        TMetrics.RecordQuery(foundCount);
                        return;
                    }
                }
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>Exposes the range scan as an allocation-free enumerable; the tree is held for the walk.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpatialRangeEnumerable<T, TBackoff, TMetrics> EnumerateRange(in Aabb2D range) =>
        new(this, in range);

    /// <inheritdoc/>
    public NearestQueryResult<T> FindNearest(in Vec2 target)
    {
        EnterOperation();
        try
        {
            ThrowIfDisposedOrFaulted();
            int spinCount = 0;

            while (true)
            {
                long version = Volatile.Read(ref _state.Version);
                if ((version & 1L) == 0L)
                {
                    QuadNode[] localNodes = Volatile.Read(ref _nodes);
                    QuadElementEntry<T>[] localElements = Volatile.Read(ref _elements);

                    if (ExecuteOptimisticNearest(localNodes, localElements, in target, out NearestQueryResult<T> result))
                    {
                        long version2 = Volatile.Read(ref _state.Version);
                        if (version == version2)
                        {
                            return result;
                        }
                    }
                }

                TBackoff.Advance(ref spinCount);

                if (spinCount > MaxSpinBeforeLock)
                {
                    lock (_syncLock)
                    {
                        ThrowIfDisposedOrFaulted();
                        return ExecuteNearestUnderLock(in target);
                    }
                }
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool FindNearest(in Vec2 target, out Vec2 foundPoint, out T foundValue, out float distanceSquared)
    {
        NearestQueryResult<T> result = FindNearest(in target);
        if (result.IsFound)
        {
            foundPoint = result.Point;
            foundValue = result.Value;
            distanceSquared = result.DistanceSquared;
            return true;
        }

        foundPoint = default;
        foundValue = default;
        distanceSquared = float.PositiveInfinity;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool ExecuteOptimisticRange<TVisitor>(
        QuadNode[] nodes,
        QuadElementEntry<T>[] elements,
        in Aabb2D range,
        ref TVisitor visitor,
        ref int foundCount)
        where TVisitor : IQuadVisitor<T>, allows ref struct
    {
        Span<uint> stack = stackalloc uint[128];
        int stackPtr = 0;
        stack[stackPtr++] = 0;

        ref QuadNode nodeBase = ref MemoryMarshal.GetArrayDataReference(nodes);
        ref QuadElementEntry<T> elemBase = ref MemoryMarshal.GetArrayDataReference(elements);
        uint nodeLength = (uint)nodes.Length;
        uint elemLength = (uint)elements.Length;

        while (stackPtr > 0)
        {
            uint nodeIdx = stack[--stackPtr];
            if (nodeIdx >= nodeLength)
            {
                return false;
            }

            ref readonly QuadNode node = ref Unsafe.Add(ref nodeBase, (nuint)nodeIdx);
            if (!node.Bounds.Overlaps(in range))
            {
                continue;
            }

            if ((node.Flags & (byte)QuadNodeFlags.IsLeaf) != 0)
            {
                ElementIndex elemIdx = node.FirstElementHead;
                while (elemIdx.IsValid)
                {
                    if (elemIdx.Value >= elemLength)
                    {
                        return false;
                    }

                    ref readonly QuadElementEntry<T> entry = ref Unsafe.Add(ref elemBase, (nuint)elemIdx.Value);
                    if (range.Contains(entry.Position))
                    {
                        foundCount++;
                        if (!visitor.Visit(in entry.Position, in entry.Value))
                        {
                            return true;
                        }
                    }

                    elemIdx = entry.NextElementIndex;
                }
            }
            else
            {
                NodeIndex childBase = node.FirstChildIndex;
                if (!childBase.IsValid || (uint)childBase.Value + 3u >= nodeLength)
                {
                    return false;
                }

                if ((uint)(stackPtr + 4) > (uint)stack.Length)
                {
                    return false;
                }

                uint child = (uint)childBase.Value;
                stack[stackPtr++] = child + 3u;
                stack[stackPtr++] = child + 2u;
                stack[stackPtr++] = child + 1u;
                stack[stackPtr++] = child + 0u;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void ExecuteRangeUnderLock<TVisitor>(in Aabb2D range, ref TVisitor visitor, ref int foundCount)
        where TVisitor : IQuadVisitor<T>, allows ref struct
    {
        Span<uint> stack = stackalloc uint[128];
        int stackPtr = 0;
        stack[stackPtr++] = 0;

        ref QuadNode nodeBase = ref MemoryMarshal.GetArrayDataReference(_nodes);
        ref QuadElementEntry<T> elemBase = ref MemoryMarshal.GetArrayDataReference(_elements);

        while (stackPtr > 0)
        {
            uint nodeIdx = stack[--stackPtr];
            ref readonly QuadNode node = ref Unsafe.Add(ref nodeBase, (nuint)nodeIdx);

            if (!node.Bounds.Overlaps(in range))
            {
                continue;
            }

            if ((node.Flags & (byte)QuadNodeFlags.IsLeaf) != 0)
            {
                ElementIndex elemIdx = node.FirstElementHead;
                while (elemIdx.IsValid)
                {
                    ref readonly QuadElementEntry<T> entry = ref Unsafe.Add(ref elemBase, (nuint)elemIdx.Value);
                    if (range.Contains(entry.Position))
                    {
                        foundCount++;
                        if (!visitor.Visit(in entry.Position, in entry.Value))
                        {
                            return;
                        }
                    }

                    elemIdx = entry.NextElementIndex;
                }
            }
            else
            {
                uint child = (uint)node.FirstChildIndex.Value;
                stack[stackPtr++] = child + 3u;
                stack[stackPtr++] = child + 2u;
                stack[stackPtr++] = child + 1u;
                stack[stackPtr++] = child + 0u;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool ExecuteOptimisticNearest(
        QuadNode[] nodes,
        QuadElementEntry<T>[] elements,
        in Vec2 target,
        out NearestQueryResult<T> result)
    {
        result = NearestQueryResult<T>.None();
        Vec2 nearestPoint = default;
        T nearestValue = default;
        float nearestDistSq = float.PositiveInfinity;
        bool foundAny = false;

        Span<uint> stack = stackalloc uint[128];
        int stackPtr = 0;
        stack[stackPtr++] = 0;

        ref QuadNode nodeBase = ref MemoryMarshal.GetArrayDataReference(nodes);
        ref QuadElementEntry<T> elemBase = ref MemoryMarshal.GetArrayDataReference(elements);
        uint nodeLength = (uint)nodes.Length;
        uint elemLength = (uint)elements.Length;

        while (stackPtr > 0)
        {
            uint nodeIdx = stack[--stackPtr];
            if (nodeIdx >= nodeLength)
            {
                return false;
            }

            ref readonly QuadNode node = ref Unsafe.Add(ref nodeBase, (nuint)nodeIdx);
            if (node.Bounds.DistanceSquared(target) >= nearestDistSq)
            {
                continue;
            }

            if ((node.Flags & (byte)QuadNodeFlags.IsLeaf) != 0)
            {
                ElementIndex elemIdx = node.FirstElementHead;
                while (elemIdx.IsValid)
                {
                    if (elemIdx.Value >= elemLength)
                    {
                        return false;
                    }

                    ref readonly QuadElementEntry<T> entry = ref Unsafe.Add(ref elemBase, (nuint)elemIdx.Value);
                    float distSq = Vec2.DistanceSquared(target, entry.Position);
                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearestPoint = entry.Position;
                        nearestValue = entry.Value;
                        foundAny = true;
                    }

                    elemIdx = entry.NextElementIndex;
                }
            }
            else
            {
                NodeIndex childBase = node.FirstChildIndex;
                if (!childBase.IsValid || (uint)childBase.Value + 3u >= nodeLength)
                {
                    return false;
                }

                if ((uint)(stackPtr + 4) > (uint)stack.Length)
                {
                    return false;
                }

                PushOrderedChildren(ref nodeBase, (uint)childBase.Value, in target, nearestDistSq, stack, ref stackPtr);
            }
        }

        if (foundAny)
        {
            result = NearestQueryResult<T>.Success(nearestPoint, nearestValue, nearestDistSq);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private NearestQueryResult<T> ExecuteNearestUnderLock(in Vec2 target)
    {
        Vec2 nearestPoint = default;
        T nearestValue = default;
        float nearestDistSq = float.PositiveInfinity;
        bool foundAny = false;

        Span<uint> stack = stackalloc uint[128];
        int stackPtr = 0;
        stack[stackPtr++] = 0;

        ref QuadNode nodeBase = ref MemoryMarshal.GetArrayDataReference(_nodes);
        ref QuadElementEntry<T> elemBase = ref MemoryMarshal.GetArrayDataReference(_elements);

        while (stackPtr > 0)
        {
            uint nodeIdx = stack[--stackPtr];
            ref readonly QuadNode node = ref Unsafe.Add(ref nodeBase, (nuint)nodeIdx);

            if (node.Bounds.DistanceSquared(target) >= nearestDistSq)
            {
                continue;
            }

            if ((node.Flags & (byte)QuadNodeFlags.IsLeaf) != 0)
            {
                ElementIndex elemIdx = node.FirstElementHead;
                while (elemIdx.IsValid)
                {
                    ref readonly QuadElementEntry<T> entry = ref Unsafe.Add(ref elemBase, (nuint)elemIdx.Value);
                    float distSq = Vec2.DistanceSquared(target, entry.Position);
                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearestPoint = entry.Position;
                        nearestValue = entry.Value;
                        foundAny = true;
                    }

                    elemIdx = entry.NextElementIndex;
                }
            }
            else
            {
                PushOrderedChildren(ref nodeBase, (uint)node.FirstChildIndex.Value, in target, nearestDistSq, stack, ref stackPtr);
            }
        }

        return foundAny
            ? NearestQueryResult<T>.Success(nearestPoint, nearestValue, nearestDistSq)
            : NearestQueryResult<T>.None();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void PushOrderedChildren(
        ref QuadNode nodeBase,
        uint childBase,
        in Vec2 target,
        float maxDistSq,
        Span<uint> stack,
        ref int stackPtr)
    {
        uint c0 = childBase + 0u;
        uint c1 = childBase + 1u;
        uint c2 = childBase + 2u;
        uint c3 = childBase + 3u;

        float d0 = Unsafe.Add(ref nodeBase, (nuint)c0).Bounds.DistanceSquared(target);
        float d1 = Unsafe.Add(ref nodeBase, (nuint)c1).Bounds.DistanceSquared(target);
        float d2 = Unsafe.Add(ref nodeBase, (nuint)c2).Bounds.DistanceSquared(target);
        float d3 = Unsafe.Add(ref nodeBase, (nuint)c3).Bounds.DistanceSquared(target);

        uint i0 = c0, i1 = c1, i2 = c2, i3 = c3;

        if (d0 < d1)
        {
            (d0, d1) = (d1, d0);
            (i0, i1) = (i1, i0);
        }

        if (d2 < d3)
        {
            (d2, d3) = (d3, d2);
            (i2, i3) = (i3, i2);
        }

        if (d0 < d2)
        {
            (d0, d2) = (d2, d0);
            (i0, i2) = (i2, i0);
        }

        if (d1 < d3)
        {
            (d1, d3) = (d3, d1);
            (i1, i3) = (i3, i1);
        }

        if (d1 < d2)
        {
            (d1, d2) = (d2, d1);
            (i1, i2) = (i2, i1);
        }

        if (d0 < maxDistSq)
        {
            stack[stackPtr++] = i0;
        }

        if (d1 < maxDistSq)
        {
            stack[stackPtr++] = i1;
        }

        if (d2 < maxDistSq)
        {
            stack[stackPtr++] = i2;
        }

        if (d3 < maxDistSq)
        {
            stack[stackPtr++] = i3;
        }
    }
}

/// <summary>
/// Allocation-free range scan over caller-owned traversal storage; the tree is
/// held (operation entered) for the enumeration lifetime.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
/// <typeparam name="TBackoff">Spin policy of the enumerated tree.</typeparam>
/// <typeparam name="TMetrics">Instrumentation sink of the enumerated tree.</typeparam>
public readonly ref struct SpatialRangeEnumerable<T, TBackoff, TMetrics>
    where T : unmanaged
    where TBackoff : struct, Axrone.Utility.Backoff.ISpinBackoff
    where TMetrics : struct, IQuadMetricsSink
{
    private readonly QuadTree<T, TBackoff, TMetrics> _tree;
    private readonly Aabb2D _range;

    /// <summary>Creates a range scan; prefer <c>EnumerateRange</c> on the tree.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SpatialRangeEnumerable(QuadTree<T, TBackoff, TMetrics> tree, in Aabb2D range)
    {
        _tree = tree;
        _range = range;
    }

    /// <summary>Returns the enumerator over the points in range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpatialRangeEnumerator<T, TBackoff, TMetrics> GetEnumerator() => new(_tree, _range);
}

/// <summary>
/// Allocation-free enumerator over the points in range. Dispose when done
/// (explicitly or via <see langword="foreach"/>) to release the tree.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
/// <typeparam name="TBackoff">Spin policy of the enumerated tree.</typeparam>
/// <typeparam name="TMetrics">Instrumentation sink of the enumerated tree.</typeparam>
public ref struct SpatialRangeEnumerator<T, TBackoff, TMetrics>
    where T : unmanaged
    where TBackoff : struct, Axrone.Utility.Backoff.ISpinBackoff
    where TMetrics : struct, IQuadMetricsSink
{
    [InlineArray(128)]
    private struct TraversalStack
    {
        private uint _element;
    }

    private readonly QuadTree<T, TBackoff, TMetrics> _tree;
    private readonly Aabb2D _range;
    private TraversalStack _stack;
    private int _stackPtr;
    private ElementIndex _currentElementIndex;
    private SpatialElement<T> _current;
    private bool _disposed;

    /// <summary>Creates an enumerator; prefer <c>EnumerateRange</c> on the tree.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SpatialRangeEnumerator(QuadTree<T, TBackoff, TMetrics> tree, Aabb2D range)
    {
        _tree = tree;
        _range = range;
        _stack = default;
        _stack[0] = 0;
        _stackPtr = 1;
        _currentElementIndex = ElementIndex.Invalid;
        _current = default;
        _disposed = false;
        _tree.EnterOperation();
    }

    /// <summary>The element at the current enumerator position.</summary>
    public SpatialElement<T> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    /// <summary>Advances to the next point in range, if any.</summary>
    /// <returns><see langword="true"/> while a further element exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        while (_currentElementIndex.IsValid)
        {
            ref readonly QuadElementEntry<T> entry = ref _tree.GetElement(_currentElementIndex);
            _currentElementIndex = entry.NextElementIndex;
            if (_range.Contains(entry.Position))
            {
                _current = new SpatialElement<T>(entry.Position, entry.Value);
                return true;
            }
        }

        while (_stackPtr > 0)
        {
            NodeIndex nodeIdx = new((int)_stack[--_stackPtr]);

            ref readonly QuadNode node = ref _tree.GetNode(nodeIdx);
            if (!node.Bounds.Overlaps(in _range))
            {
                continue;
            }

            if ((node.Flags & (byte)QuadNodeFlags.IsLeaf) != 0)
            {
                _currentElementIndex = node.FirstElementHead;
                while (_currentElementIndex.IsValid)
                {
                    ref readonly QuadElementEntry<T> entry = ref _tree.GetElement(_currentElementIndex);
                    _currentElementIndex = entry.NextElementIndex;
                    if (_range.Contains(entry.Position))
                    {
                        _current = new SpatialElement<T>(entry.Position, entry.Value);
                        return true;
                    }
                }
            }
            else
            {
                NodeIndex childBase = node.FirstChildIndex;
                if (childBase.IsValid && _stackPtr + 4 <= 128)
                {
                    uint child = (uint)childBase.Value;
                    _stack[_stackPtr++] = child + 3u;
                    _stack[_stackPtr++] = child + 2u;
                    _stack[_stackPtr++] = child + 1u;
                    _stack[_stackPtr++] = child + 0u;
                }
            }
        }

        return false;
    }

    /// <summary>Releases the tree held for the enumeration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _tree.ExitOperation();
        }
    }
}
