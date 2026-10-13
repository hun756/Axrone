namespace Axrone.Geometry;

using System.Runtime.ExceptionServices;
using Axrone.Utility.Backoff;

/// <summary>
/// Tuning knobs of a quad tree: how many elements a leaf holds before it
/// splits, how deep the split cascade may go, and how large the node and
/// element pools open. Guards live in the properties, so an invalid
/// combination is rejected at assignment rather than at the first insert that
/// trips over it. Unlike the source sketch, out-of-range values throw instead
/// of silently falling back to defaults.
/// </summary>
public readonly struct QuadTreeConfig
{
    /// <summary>Creates the default tuning set.</summary>
    public QuadTreeConfig()
    {
    }

    /// <summary>Elements a leaf holds before it splits; 1 through 256.</summary>
    public int MaxElementsPerNode
    {
        get;
        init
        {
            if (value < 1 || value > 256)
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(MaxElementsPerNode));
            }

            field = value;
        }
    } = 16;

    /// <summary>Deepest split level; 1 through 32.</summary>
    public int MaxDepth
    {
        get;
        init
        {
            if (value < 1 || value > 32)
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(MaxDepth));
            }

            field = value;
        }
    } = 16;

    /// <summary>Node pool slots reserved up front; at least 4.</summary>
    public int InitialNodeCapacity
    {
        get;
        init
        {
            if (value < 4)
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(InitialNodeCapacity));
            }

            field = value;
        }
    } = 256;

    /// <summary>Element pool slots reserved up front; at least 4.</summary>
    public int InitialElementCapacity
    {
        get;
        init
        {
            if (value < 4)
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(InitialElementCapacity));
            }

            field = value;
        }
    } = 1024;
}

/// <summary>
/// One node of a quad tree: the covered bounds, the child-block head or the
/// element-list head, the fill count, the depth, the leaf flag and the free
/// link. Children of a split node always occupy four consecutive slots, so a
/// quadrant selects its child by offset.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 32)]
internal struct QuadNode
{
    /// <summary>Bounds covered by this node.</summary>
    [FieldOffset(0)]
    public Aabb2D Bounds;

    /// <summary>First child slot; invalid while the node is a leaf.</summary>
    [FieldOffset(16)]
    public NodeIndex FirstChildIndex;

    /// <summary>Head of the element list; invalid while the node is internal.</summary>
    [FieldOffset(20)]
    public ElementIndex FirstElementHead;

    /// <summary>Elements linked to this leaf.</summary>
    [FieldOffset(24)]
    public ushort ElementCount;

    /// <summary>Split depth; zero at the root.</summary>
    [FieldOffset(26)]
    public byte Depth;

    /// <summary>Leaf flag; see <see cref="QuadNodeFlags"/>.</summary>
    [FieldOffset(27)]
    public byte Flags;

    /// <summary>Next free slot while pooled.</summary>
    [FieldOffset(28)]
    public NodeIndex NextFreeNode;
}

/// <summary>Leaf flag of a quad node.</summary>
[Flags]
internal enum QuadNodeFlags : byte
{
    /// <summary>No flags set.</summary>
    None = 0,

    /// <summary>The node is a leaf and carries elements.</summary>
    IsLeaf = 1 << 0,
}

/// <summary>
/// One pooled element: the point, its payload and the next link of the leaf's
/// intrusive list.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct QuadElementEntry<T> where T : unmanaged
{
    /// <summary>Stored position.</summary>
    public Vec2 Position;

    /// <summary>Stored payload.</summary>
    public T Value;

    /// <summary>Next element of the leaf list.</summary>
    public ElementIndex NextElementIndex;
}

/// <summary>
/// Seqlock word plus operation and lifecycle counters, each on its own cache
/// line so optimistic readers never contend with the writer's counters.
/// Namespace-level because the CLR forbids explicit layout on types nested in
/// a generic class.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 192)]
internal struct QuadTreeState
{
    /// <summary>Odd while a mutation is in flight.</summary>
    [FieldOffset(0)]
    public long Version;

    /// <summary>Operations currently inside the tree.</summary>
    [FieldOffset(64)]
    public int ActiveCount;

    /// <summary>Lifecycle word; see the tree's private lifecycle states.</summary>
    [FieldOffset(128)]
    public int Lifecycle;
}

/// <summary>
/// Point quad tree over fixed root bounds: leaves hold intrusive element
/// lists and split into four children past the fill threshold, readers run
/// lock-free behind a sequence lock with a locked retry fallback, and writers
/// serialize under a gate. Points outside the root bounds are rejected rather
/// than growing the tree: a quad covers exactly what it was built with.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the point.</typeparam>
/// <typeparam name="TBackoff">Spin policy for the optimistic-retry loop; the house backoff, not a local copy.</typeparam>
/// <typeparam name="TMetrics">Traversal instrumentation sink.</typeparam>
public sealed partial class QuadTree<T, TBackoff, TMetrics> : IQuadIndex<T>
    where T : unmanaged
    where TBackoff : struct, Axrone.Utility.Backoff.ISpinBackoff
    where TMetrics : struct, IQuadMetricsSink
{
    private const int MaxSpinBeforeLock = 64;

    private static class LifecycleState
    {
        public const int Active = 0;
        public const int Completed = 1;
        public const int Disposing = 2;
        public const int Disposed = 3;
        public const int Faulted = 4;
    }

    private readonly Lock _syncLock = new();
    private readonly Aabb2D _rootBounds;
    private readonly int _maxElementsPerNode;
    private readonly int _maxDepth;
    private ExceptionDispatchInfo? _faultInfo;

    private QuadTreeState _state;
    private QuadNode[] _nodes;
    private QuadElementEntry<T>[] _elements;
    private uint _nodeCount;
    private uint _elementCount;
    private ElementIndex _freeElementHead;
    private int _totalItems;

    /// <summary>Creates a tree over <paramref name="rootBounds"/> with the given tuning.</summary>
    public QuadTree(in Aabb2D rootBounds, in QuadTreeConfig config = default)
    {
        if (!rootBounds.IsValid)
        {
            ThrowHelper.ThrowInvalidQuadBounds();
        }

        _rootBounds = rootBounds;
        _maxElementsPerNode = config.MaxElementsPerNode;
        _maxDepth = config.MaxDepth;
        _freeElementHead = ElementIndex.Invalid;

        int initialNodes = Math.Max(config.InitialNodeCapacity, 4);
        int initialElements = Math.Max(config.InitialElementCapacity, 4);

        _nodes = GC.AllocateArray<QuadNode>(initialNodes, pinned: true);
        _elements = GC.AllocateArray<QuadElementEntry<T>>(initialElements, pinned: true);

        _nodes[0] = new QuadNode
        {
            Bounds = rootBounds,
            FirstChildIndex = NodeIndex.Null,
            FirstElementHead = ElementIndex.Invalid,
            ElementCount = 0,
            Depth = 0,
            Flags = (byte)QuadNodeFlags.IsLeaf,
            NextFreeNode = NodeIndex.Null,
        };

        _nodeCount = 1;
        _elementCount = 0;
        _totalItems = 0;
        _state.Version = 0;
        _state.ActiveCount = 0;
        _state.Lifecycle = LifecycleState.Active;
    }

    /// <summary>Number of live elements in the tree.</summary>
    public int Count => Volatile.Read(ref _totalItems);

    /// <summary>Fixed root bounds the tree was built with.</summary>
    public Aabb2D Bounds => _rootBounds;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Insert(in Vec2 point, in T value) => TryInsert(in point, in value);

    /// <inheritdoc/>
    public bool TryInsert(in Vec2 point, in T value)
    {
        if (!_rootBounds.Contains(point))
        {
            return false;
        }

        EnterOperation();
        try
        {
            lock (_syncLock)
            {
                ThrowIfDisposedOrFaulted();
                if (Volatile.Read(ref _state.Lifecycle) == LifecycleState.Completed)
                {
                    ThrowHelper.ThrowComponentCompleted();
                }

                BeginMutation();

                NodeIndex currentIdx = NodeIndex.FromIndex(0);
                while (true)
                {
                    ref QuadNode currNode = ref GetNodeRef(currentIdx);

                    if ((currNode.Flags & (byte)QuadNodeFlags.IsLeaf) != 0)
                    {
                        if (currNode.ElementCount < _maxElementsPerNode || currNode.Depth >= _maxDepth)
                        {
                            ElementIndex elemIdx = AllocateElement(in point, in value);
                            currNode = ref GetNodeRef(currentIdx);
                            GetElementRef(elemIdx).NextElementIndex = currNode.FirstElementHead;
                            currNode.FirstElementHead = elemIdx;
                            currNode.ElementCount++;
                            _totalItems++;

                            EndMutation();
                            TMetrics.RecordInsert();
                            return true;
                        }

                        SplitNode(currentIdx);
                        currNode = ref GetNodeRef(currentIdx);
                        TMetrics.RecordSubdivision();
                    }

                    currentIdx = ChildAt(currNode.FirstChildIndex, GetQuadrant(in currNode.Bounds, in point));
                }
            }
        }
        catch (Exception ex) when (FaultIfNeeded(ex))
        {
            throw;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(in Vec2 point, in T value) => TryRemove(in point, in value);

    /// <inheritdoc/>
    public bool TryRemove(in Vec2 point, in T value)
    {
        if (!_rootBounds.Contains(point))
        {
            return false;
        }

        EnterOperation();
        try
        {
            lock (_syncLock)
            {
                ThrowIfDisposedOrFaulted();
                if (Volatile.Read(ref _state.Lifecycle) == LifecycleState.Completed)
                {
                    ThrowHelper.ThrowComponentCompleted();
                }

                BeginMutation();

                NodeIndex currentIdx = NodeIndex.FromIndex(0);
                while (true)
                {
                    ref QuadNode currNode = ref GetNodeRef(currentIdx);

                    if ((currNode.Flags & (byte)QuadNodeFlags.IsLeaf) != 0)
                    {
                        ElementIndex prevElem = ElementIndex.Invalid;
                        ElementIndex currElem = currNode.FirstElementHead;

                        while (currElem.IsValid)
                        {
                            ref QuadElementEntry<T> entry = ref GetElementRef(currElem);
                            if (entry.Position == point && EqualityComparer<T>.Default.Equals(entry.Value, value))
                            {
                                if (!prevElem.IsValid)
                                {
                                    currNode.FirstElementHead = entry.NextElementIndex;
                                }
                                else
                                {
                                    GetElementRef(prevElem).NextElementIndex = entry.NextElementIndex;
                                }

                                currNode.ElementCount--;
                                FreeElement(currElem);
                                _totalItems--;

                                EndMutation();
                                TMetrics.RecordRemove();
                                return true;
                            }

                            prevElem = currElem;
                            currElem = entry.NextElementIndex;
                        }

                        EndMutation();
                        return false;
                    }

                    currentIdx = ChildAt(currNode.FirstChildIndex, GetQuadrant(in currNode.Bounds, in point));
                }
            }
        }
        catch (Exception ex) when (FaultIfNeeded(ex))
        {
            throw;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        EnterOperation();
        try
        {
            lock (_syncLock)
            {
                ThrowIfDisposedOrFaulted();
                if (Volatile.Read(ref _state.Lifecycle) == LifecycleState.Completed)
                {
                    ThrowHelper.ThrowComponentCompleted();
                }

                BeginMutation();

                _nodes[0] = new QuadNode
                {
                    Bounds = _rootBounds,
                    FirstChildIndex = NodeIndex.Null,
                    FirstElementHead = ElementIndex.Invalid,
                    ElementCount = 0,
                    Depth = 0,
                    Flags = (byte)QuadNodeFlags.IsLeaf,
                    NextFreeNode = NodeIndex.Null,
                };

                _nodeCount = 1;
                _elementCount = 0;
                _freeElementHead = ElementIndex.Invalid;
                _totalItems = 0;

                EndMutation();
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    public void Complete(Exception? error = null)
    {
        if (error is not null)
        {
            _faultInfo = ExceptionDispatchInfo.Capture(error);
            Interlocked.Exchange(ref _state.Lifecycle, LifecycleState.Faulted);
            return;
        }

        Interlocked.CompareExchange(ref _state.Lifecycle, LifecycleState.Completed, LifecycleState.Active);
    }

    /// <inheritdoc/>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        Complete();

        int spinCount = 0;
        while (Volatile.Read(ref _state.ActiveCount) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TBackoff.Advance(ref spinCount);
            if (spinCount > 30)
            {
                await Task.Yield();
                spinCount = 0;
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Waits out live operations (including enumerations) before releasing
    /// the pools, mirroring the native octree: abandoning an enumerator
    /// without disposing it blocks here until it is released.
    /// </remarks>
    public void Dispose()
    {
        int prior = Interlocked.Exchange(ref _state.Lifecycle, LifecycleState.Disposed);
        if (prior == LifecycleState.Disposed)
        {
            return;
        }

        SpinWait spinner = default;
        while (Volatile.Read(ref _state.ActiveCount) > 0)
        {
            spinner.SpinOnce();
        }

        lock (_syncLock)
        {
            _nodes = [];
            _elements = [];
            _nodeCount = 0;
            _elementCount = 0;
            _totalItems = 0;
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        int prior = Interlocked.CompareExchange(ref _state.Lifecycle, LifecycleState.Disposing, LifecycleState.Active);
        if (prior == LifecycleState.Disposed || prior == LifecycleState.Disposing)
        {
            return;
        }

        try
        {
            await DrainAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            Dispose();
            GC.SuppressFinalize(this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly QuadNode GetNode(NodeIndex index) => ref _nodes[index.RawValue];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly QuadElementEntry<T> GetElement(ElementIndex index) => ref _elements[index.Value];

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref QuadNode GetNodeRef(NodeIndex index) => ref _nodes[index.RawValue];

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref QuadElementEntry<T> GetElementRef(ElementIndex index) => ref _elements[index.Value];

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Quadrant GetQuadrant(in Aabb2D bounds, in Vec2 point)
    {
        Vec2 center = bounds.Center;
        int quad = (point.Y < center.Y ? 2 : 0) | (point.X >= center.X ? 1 : 0);
        return (Quadrant)quad;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static NodeIndex ChildAt(NodeIndex childBase, Quadrant quadrant) =>
        NodeIndex.FromRaw(childBase.RawValue + (int)quadrant);

    private void SplitNode(NodeIndex nodeIdx)
    {
        NodeIndex childBase = AllocateNodeBlock();
        ref QuadNode parent = ref GetNodeRef(nodeIdx);

        Aabb2D bounds = parent.Bounds;
        byte nextDepth = (byte)(parent.Depth + 1);

        Vec2 min = bounds.Min;
        Vec2 max = bounds.Max;
        Vec2 center = bounds.Center;

        GetNodeRef(NodeIndex.FromRaw(childBase.RawValue + 0)) = new QuadNode
        {
            Bounds = new Aabb2D(min.X, center.Y, center.X, max.Y),
            Depth = nextDepth,
            Flags = (byte)QuadNodeFlags.IsLeaf,
            FirstChildIndex = NodeIndex.Null,
            FirstElementHead = ElementIndex.Invalid,
            ElementCount = 0,
            NextFreeNode = NodeIndex.Null,
        };

        GetNodeRef(NodeIndex.FromRaw(childBase.RawValue + 1)) = new QuadNode
        {
            Bounds = new Aabb2D(center.X, center.Y, max.X, max.Y),
            Depth = nextDepth,
            Flags = (byte)QuadNodeFlags.IsLeaf,
            FirstChildIndex = NodeIndex.Null,
            FirstElementHead = ElementIndex.Invalid,
            ElementCount = 0,
            NextFreeNode = NodeIndex.Null,
        };

        GetNodeRef(NodeIndex.FromRaw(childBase.RawValue + 2)) = new QuadNode
        {
            Bounds = new Aabb2D(min.X, min.Y, center.X, center.Y),
            Depth = nextDepth,
            Flags = (byte)QuadNodeFlags.IsLeaf,
            FirstChildIndex = NodeIndex.Null,
            FirstElementHead = ElementIndex.Invalid,
            ElementCount = 0,
            NextFreeNode = NodeIndex.Null,
        };

        GetNodeRef(NodeIndex.FromRaw(childBase.RawValue + 3)) = new QuadNode
        {
            Bounds = new Aabb2D(center.X, min.Y, max.X, center.Y),
            Depth = nextDepth,
            Flags = (byte)QuadNodeFlags.IsLeaf,
            FirstChildIndex = NodeIndex.Null,
            FirstElementHead = ElementIndex.Invalid,
            ElementCount = 0,
            NextFreeNode = NodeIndex.Null,
        };

        parent = ref GetNodeRef(nodeIdx);
        ElementIndex elemIdx = parent.FirstElementHead;

        parent.FirstChildIndex = childBase;
        parent.Flags &= (byte)~QuadNodeFlags.IsLeaf;
        parent.FirstElementHead = ElementIndex.Invalid;
        parent.ElementCount = 0;

        while (elemIdx.IsValid)
        {
            ref QuadElementEntry<T> entry = ref GetElementRef(elemIdx);
            ElementIndex nextElem = entry.NextElementIndex;

            Quadrant quad = GetQuadrant(in bounds, in entry.Position);
            NodeIndex childIdx = ChildAt(childBase, quad);
            ref QuadNode child = ref GetNodeRef(childIdx);

            entry.NextElementIndex = child.FirstElementHead;
            child.FirstElementHead = elemIdx;
            child.ElementCount++;

            elemIdx = nextElem;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ElementIndex AllocateElement(in Vec2 point, in T value)
    {
        if (_freeElementHead.IsValid)
        {
            ElementIndex idx = _freeElementHead;
            ref QuadElementEntry<T> recycled = ref GetElementRef(idx);
            _freeElementHead = recycled.NextElementIndex;

            recycled.Position = point;
            recycled.Value = value;
            recycled.NextElementIndex = ElementIndex.Invalid;
            return idx;
        }

        if ((uint)_elements.Length <= _elementCount)
        {
            int newCap = _elements.Length * 2;
            QuadElementEntry<T>[] newArr = GC.AllocateArray<QuadElementEntry<T>>(newCap, pinned: true);
            _elements.AsSpan().CopyTo(newArr);
            Volatile.Write(ref _elements, newArr);
        }

        ElementIndex newIdx = new(_elementCount++);
        ref QuadElementEntry<T> fresh = ref GetElementRef(newIdx);
        fresh.Position = point;
        fresh.Value = value;
        fresh.NextElementIndex = ElementIndex.Invalid;
        return newIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FreeElement(ElementIndex elemIdx)
    {
        ref QuadElementEntry<T> entry = ref GetElementRef(elemIdx);
        entry.Position = default;
        entry.Value = default;
        entry.NextElementIndex = _freeElementHead;
        _freeElementHead = elemIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private NodeIndex AllocateNodeBlock()
    {
        if ((uint)_nodes.Length < _nodeCount + 4)
        {
            int newCap = _nodes.Length * 2;
            QuadNode[] newArr = GC.AllocateArray<QuadNode>(newCap, pinned: true);
            _nodes.AsSpan().CopyTo(newArr);
            Volatile.Write(ref _nodes, newArr);
        }

        NodeIndex baseIdx = NodeIndex.FromIndex((int)_nodeCount);
        _nodeCount += 4;
        return baseIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BeginMutation()
    {
        long version = Volatile.Read(ref _state.Version);
        Volatile.Write(ref _state.Version, version + 1L);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EndMutation()
    {
        long version = Volatile.Read(ref _state.Version);
        Volatile.Write(ref _state.Version, version + 1L);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnterOperation()
    {
        ThrowIfDisposedOrFaulted();
        Interlocked.Increment(ref _state.ActiveCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExitOperation()
    {
        Interlocked.Decrement(ref _state.ActiveCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposedOrFaulted()
    {
        int state = Volatile.Read(ref _state.Lifecycle);
        if (state == LifecycleState.Faulted)
        {
            ThrowHelper.ThrowComponentFaulted(_faultInfo);
        }

        if (state >= LifecycleState.Disposing)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(QuadTree<T, TBackoff, TMetrics>));
        }
    }

    private bool FaultIfNeeded(Exception ex)
    {
        _faultInfo = ExceptionDispatchInfo.Capture(ex);
        Interlocked.Exchange(ref _state.Lifecycle, LifecycleState.Faulted);
        return false;
    }
}
