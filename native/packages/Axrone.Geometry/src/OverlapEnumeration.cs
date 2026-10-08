namespace Axrone.Geometry;

/// <summary>
/// Allocation-free overlap scan over caller-owned stack storage; the tree is
/// held (operation entered) for the enumeration lifetime.
/// </summary>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
/// <typeparam name="TStrategy">Insertion policy of the enumerated tree.</typeparam>
/// <typeparam name="TMetrics">Traversal instrumentation sink of the enumerated tree.</typeparam>
public readonly ref struct OverlapEnumerable<TUserData, TStrategy, TMetrics>
    where TStrategy : struct, ISpatialPartitionStrategy
    where TMetrics : struct, ISpatialMetricsSink
{
    private readonly DynamicAabbTree<TUserData, TStrategy, TMetrics> _tree;
    private readonly Aabb3D _queryBox;
    private readonly Span<NodeIndex> _stackBuffer;

    /// <summary>Creates an overlap scan; prefer <c>EnumerateOverlaps</c> on the tree.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal OverlapEnumerable(DynamicAabbTree<TUserData, TStrategy, TMetrics> tree, in Aabb3D queryBox, Span<NodeIndex> stackBuffer)
    {
        _tree = tree;
        _queryBox = queryBox;
        _stackBuffer = stackBuffer;
    }

    /// <summary>Returns the stack-backed enumerator over the leaf hits.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OverlapEnumerator<TUserData, TStrategy, TMetrics> GetEnumerator() =>
        new(_tree, _queryBox, _stackBuffer);
}

/// <summary>
/// Allocation-free stack-backed enumerator over leaf entity hits. Dispose when
/// done (explicitly or via <see langword="using"/>) to release the tree.
/// </summary>
public ref struct OverlapEnumerator<TUserData, TStrategy, TMetrics>
    where TStrategy : struct, ISpatialPartitionStrategy
    where TMetrics : struct, ISpatialMetricsSink
{
    private readonly DynamicAabbTree<TUserData, TStrategy, TMetrics> _tree;
    private readonly Aabb3D _queryBox;
    private readonly Span<NodeIndex> _stack;
    private int _stackPtr;
    private SpatialItemId _current;
    private bool _disposed;

    /// <summary>Creates an enumerator; prefer <c>EnumerateOverlaps</c> on the tree.</summary>
    /// <param name="tree">The tree being enumerated.</param>
    /// <param name="queryBox">Query bounds; copied once, never captured by reference.</param>
    /// <param name="stack">Caller-owned traversal storage.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal OverlapEnumerator(DynamicAabbTree<TUserData, TStrategy, TMetrics> tree, Aabb3D queryBox, Span<NodeIndex> stack)
    {
        _tree = tree;
        _queryBox = queryBox;
        _stack = stack;
        _stackPtr = 0;
        _current = SpatialItemId.Invalid;
        _disposed = false;

        _tree.EnterOperation();

        NodeIndex root = _tree.RootIndex;
        if (root.IsValid && _stack.Length > 0)
        {
            _stack[_stackPtr++] = root;
        }
    }

    /// <summary>The identity at the current enumerator position.</summary>
    public SpatialItemId Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    /// <summary>Advances to the next overlapping leaf, if any.</summary>
    /// <returns><see langword="true"/> while a further hit exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        while (_stackPtr > 0)
        {
            NodeIndex nodeIndex = _stack[--_stackPtr];
            if (nodeIndex.IsNull)
            {
                continue;
            }

            ref readonly Node node = ref _tree.GetNodeRef(nodeIndex);
            TMetrics.OnNodeVisited();

            if (node.Box.Overlaps(in _queryBox))
            {
                TMetrics.OnIntersectionTested(true);
                if (node.IsLeaf())
                {
                    _current = node.ItemId;
                    return true;
                }

                if ((uint)(_stackPtr + 2) <= (uint)_stack.Length)
                {
                    _stack[_stackPtr++] = node.LeftChild;
                    _stack[_stackPtr++] = node.RightChild;
                }
            }
            else
            {
                TMetrics.OnIntersectionTested(false);
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
