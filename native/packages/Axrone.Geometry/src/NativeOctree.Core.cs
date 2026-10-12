namespace Axrone.Geometry;

using System.Runtime.ExceptionServices;

/// <summary>
/// One node of a native octree: the covered bounds and center, the child-block
/// head or the item-list head, the fill count, the child occupancy mask, the
/// depth and the free link. Children of a split node always occupy eight
/// consecutive slots, so an octant selects its child by offset. The 64-byte
/// explicit layout keeps a node to one cache line.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct OctreeNode
{
    /// <summary>Bounds covered by this node.</summary>
    [FieldOffset(0)]
    public Aabb3D Bounds;

    /// <summary>Center used for octant classification.</summary>
    [FieldOffset(24)]
    public Vec3 Center;

    /// <summary>First child slot; invalid while the node is a leaf.</summary>
    [FieldOffset(36)]
    public NodeIndex ChildBaseIndex;

    /// <summary>Head of the item list.</summary>
    [FieldOffset(40)]
    public SlotIndex FirstItemIndex;

    /// <summary>Parent slot; invalid on the root.</summary>
    [FieldOffset(44)]
    public NodeIndex ParentIndex;

    /// <summary>Items linked directly to this node.</summary>
    [FieldOffset(48)]
    public ushort ItemCount;

    /// <summary>Occupancy mask of the eight child slots.</summary>
    [FieldOffset(50)]
    public byte ChildMask;

    /// <summary>Split depth; zero at the root.</summary>
    [FieldOffset(51)]
    public TreeDepth Depth;

    [FieldOffset(52)]
    private readonly uint _pad0;

    [FieldOffset(56)]
    private readonly ulong _pad1;
}

/// <summary>
/// One pooled item: the tight bounds, its payload, the next link of the
/// node's intrusive list, the home node and the allocation epoch.
/// </summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct OctreeItem<T> where T : unmanaged
{
    /// <summary>Tight bounds of the item.</summary>
    public Aabb3D Bounds;

    /// <summary>Stored payload.</summary>
    public T Value;

    /// <summary>Next item of the node list.</summary>
    public SlotIndex NextItemIndex;

    /// <summary>Node the item is linked to.</summary>
    public NodeIndex HomeNode;

    /// <summary>Allocation epoch of this slot.</summary>
    public Generation Generation;
}

/// <summary>Lifecycle words of a native octree.</summary>
internal static class OctreeState
{
    /// <summary>Accepting work.</summary>
    public const int Active = 1;

    /// <summary>Closed to new work, draining the in-flight operations.</summary>
    public const int Draining = 2;

    /// <summary>Terminal: pools released.</summary>
    public const int Disposed = 3;

    /// <summary>Terminal: a recorded failure ended the stream; operations fail closed.</summary>
    public const int Faulted = 4;
}

/// <summary>Fixed traversal stack storage.</summary>
[InlineArray(128)]
internal struct TraversalStack128
{
    private NodeIndex _element0;
}

/// <summary>Retired pool blocks awaiting release at dispose.</summary>
[InlineArray(32)]
internal struct RetiredBuffersTable
{
    private nuint _element0;
}

/// <summary>
/// Native octree over a fixed root domain: leaves hold intrusive item lists
/// and split into eight children past the policy threshold, items carry
/// generational handles, pools grow geometrically with retired blocks kept
/// alive for lock-free readers, and empty branches collapse per policy.
/// </summary>
/// <remarks>
/// Memory comes from 64-byte aligned native blocks released at dispose; a
/// finalizer frees them when disposal is skipped. <see cref="DrainAsync"/>
/// is a graceful shutdown: it closes the stream, waits out the in-flight
/// operations and disposes the tree.
/// </remarks>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
/// <typeparam name="TPartition">Split/collapse policy.</typeparam>
/// <typeparam name="TMetrics">Instrumentation sink.</typeparam>
public sealed partial class NativeOctree<T, TPartition, TMetrics> : IOctreeIndex<T>
    where T : unmanaged
    where TPartition : struct, IPartitionPolicy<TPartition>
    where TMetrics : struct, IOctreeMetrics<TMetrics>
{
    private readonly Lock _gate = new();
    private readonly Aabb3D _rootBounds;
    private ExceptionDispatchInfo? _faultInfo;

    private int _state;
    private int _inFlightOperations;
    private uint _activeItemCount;

    private unsafe OctreeNode* _nodes;
    private uint _nodeCapacity;
    private uint _nodeCount;
    private NodeIndex _freeNodeBlockHead;

    private unsafe OctreeItem<T>* _items;
    private uint _itemCapacity;
    private uint _itemWatermark;
    private SlotIndex _freeItemHead;

    /// <summary>
    /// Monotonic generation source: every rent stamps the next value, so a
    /// generation is never reused while the counter lives (4B rents). Zero is
    /// skipped because it means "unallocated". Survives <see cref="Clear"/>
    /// untouched, which is what keeps pre-Clear handles dead.
    /// </summary>
    private uint _itemGenCounter;

    private RetiredBuffersTable _retiredBlocks;
    private int _retiredCount;

    /// <summary>Creates a tree over <paramref name="rootBounds"/> with the given capacities.</summary>
    public unsafe NativeOctree(
        in Aabb3D rootBounds,
        NodeCapacity initialNodeCapacity = default,
        ItemCapacity initialItemCapacity = default)
    {
        // The root needs strictly positive volume: unlike items (which may be
        // degenerate), a flat root would misfile every octant decision.
        Vec3 rootMin = rootBounds.Min;
        Vec3 rootMax = rootBounds.Max;
        if (IsInvalidDomain(in rootBounds) ||
            rootMin.X >= rootMax.X || rootMin.Y >= rootMax.Y || rootMin.Z >= rootMax.Z)
        {
            ThrowHelper.ThrowInvalidBounds(in rootBounds);
        }

        uint nodeCap = initialNodeCapacity.Value == 0 ? 1024u : initialNodeCapacity.Value;
        uint itemCap = initialItemCapacity.Value == 0 ? 2048u : initialItemCapacity.Value;

        if (nodeCap < 8)
        {
            ThrowHelper.ThrowInvalidCapacity(nameof(initialNodeCapacity));
        }

        if (itemCap < 16)
        {
            ThrowHelper.ThrowInvalidCapacity(nameof(initialItemCapacity));
        }

        _rootBounds = rootBounds;

        _nodeCapacity = (nodeCap + 7u) & ~7u;
        nuint nodeBytes = checked((nuint)_nodeCapacity * (nuint)sizeof(OctreeNode));
        _nodes = (OctreeNode*)NativeMemory.AlignedAlloc(nodeBytes, 64);
        NativeMemory.Clear(_nodes, nodeBytes);

        _itemCapacity = itemCap;
        nuint itemBytes = checked((nuint)_itemCapacity * (nuint)sizeof(OctreeItem<T>));
        _items = (OctreeItem<T>*)NativeMemory.AlignedAlloc(itemBytes, 64);
        NativeMemory.Clear(_items, itemBytes);

        _freeNodeBlockHead = NodeIndex.Null;
        _freeItemHead = SlotIndex.Invalid;
        _itemWatermark = 0;
        _activeItemCount = 0;
        _retiredCount = 0;
        _retiredBlocks = default;

        ref OctreeNode root = ref _nodes[0];
        root.Bounds = rootBounds;
        root.Center = rootBounds.Center;
        root.ChildBaseIndex = NodeIndex.Null;
        root.FirstItemIndex = SlotIndex.Invalid;
        root.ParentIndex = NodeIndex.Null;
        root.ItemCount = 0;
        root.ChildMask = 0;
        root.Depth = TreeDepth.Root;
        _nodeCount = 1;

        _state = OctreeState.Active;
        _inFlightOperations = 0;

        TMetrics.OnMemoryResized(nodeBytes + itemBytes);
    }

    /// <summary>Finalizer; frees native pools when disposal was skipped.</summary>
    ~NativeOctree()
    {
        unsafe
        {
            FreeNative();
        }
    }

    /// <summary>Number of live items in the tree.</summary>
    public uint Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _activeItemCount);
    }

    /// <summary>Fixed root domain the tree was built with.</summary>
    public Aabb3D RootBounds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _rootBounds;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe OctreeHandle Insert(in Aabb3D bounds, in T value)
    {
        InsertResult result = TryInsert(in bounds, in value);
        return result.Outcome switch
        {
            InsertOutcome.Success => result.Handle,
            InsertOutcome.OutOfBounds => ThrowHelper.ThrowOutOfBounds<OctreeHandle>(in bounds, in _rootBounds),
            InsertOutcome.InvalidDomain => ThrowHelper.ThrowInvalidBounds<OctreeHandle>(in bounds),
            InsertOutcome.Disposed => ThrowHelper.ThrowObjectDisposed<OctreeHandle>(nameof(NativeOctree<T, TPartition, TMetrics>)),
            _ => ThrowHelper.ThrowInvalidOutcome<OctreeHandle>(result.Outcome),
        };
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe InsertResult TryInsert(in Aabb3D bounds, in T value)
    {
        if (!TryEnterOperation())
        {
            return InsertResult.Disposed();
        }

        try
        {
            if (IsInvalidDomain(in bounds))
            {
                return InsertResult.InvalidDomain();
            }

            if (!_rootBounds.Contains(in bounds))
            {
                return InsertResult.OutOfBounds();
            }

            lock (_gate)
            {
                if (Volatile.Read(ref _state) != OctreeState.Active)
                {
                    return InsertResult.Disposed();
                }

                SlotIndex itemSlot = RentItemSlot();
                ref OctreeItem<T> item = ref _items[itemSlot.Value];
                item.Bounds = bounds;
                item.Value = value;
                item.NextItemIndex = SlotIndex.Invalid;
                Generation generation = item.Generation;

                NodeIndex targetNode = NavigateTargetNode(new NodeIndex(0), in bounds);
                LinkItem(itemSlot, targetNode);

                uint active = ++_activeItemCount;
                TMetrics.OnItemInserted(active);
                return InsertResult.Success(new OctreeHandle(itemSlot, generation));
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe bool Remove(OctreeHandle handle)
    {
        if (!handle.IsValid)
        {
            return false;
        }

        if (!TryEnterOperation())
        {
            return false;
        }

        try
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _state) != OctreeState.Active)
                {
                    return false;
                }

                if (handle.Slot.Value >= _itemWatermark)
                {
                    return false;
                }

                ref OctreeItem<T> item = ref _items[handle.Slot.Value];
                if (item.Generation != handle.Generation)
                {
                    return false;
                }

                NodeIndex nodeIndex = item.HomeNode;
                UnlinkItem(handle.Slot, nodeIndex);
                ReturnItemSlot(handle.Slot);

                uint active = --_activeItemCount;
                TMetrics.OnItemRemoved(active);

                PruneBranch(nodeIndex);
                return true;
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe bool Update(OctreeHandle handle, in Aabb3D newBounds, in T newValue)
    {
        if (!handle.IsValid)
        {
            return false;
        }

        if (!TryEnterOperation())
        {
            return false;
        }

        try
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _state) != OctreeState.Active)
                {
                    return false;
                }

                if (handle.Slot.Value >= _itemWatermark)
                {
                    return false;
                }

                ref OctreeItem<T> item = ref _items[handle.Slot.Value];
                if (item.Generation != handle.Generation)
                {
                    return false;
                }

                if (IsInvalidDomain(in newBounds) || !_rootBounds.Contains(in newBounds))
                {
                    return false;
                }

                NodeIndex currentNodeIndex = item.HomeNode;
                ref OctreeNode currentNode = ref _nodes[currentNodeIndex.Value];

                if (currentNode.Bounds.Contains(in newBounds))
                {
                    if (!currentNode.ChildBaseIndex.IsValid || ClassifyOctant(currentNode.Center, in newBounds) == -1)
                    {
                        item.Bounds = newBounds;
                        item.Value = newValue;
                        return true;
                    }
                }

                UnlinkItem(handle.Slot, currentNodeIndex);
                PruneBranch(currentNodeIndex);

                item.Bounds = newBounds;
                item.Value = newValue;

                NodeIndex targetNode = NavigateTargetNode(new NodeIndex(0), in newBounds);
                LinkItem(handle.Slot, targetNode);
                return true;
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public unsafe void Clear()
    {
        if (!TryEnterOperation())
        {
            return;
        }

        try
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _state) != OctreeState.Active)
                {
                    return;
                }

                nuint nodeBytes = checked((nuint)_nodeCapacity * (nuint)sizeof(OctreeNode));
                NativeMemory.Clear(_nodes, nodeBytes);

                nuint itemBytes = checked((nuint)_itemCapacity * (nuint)sizeof(OctreeItem<T>));
                NativeMemory.Clear(_items, itemBytes);

                _freeNodeBlockHead = NodeIndex.Null;
                _freeItemHead = SlotIndex.Invalid;
                _itemWatermark = 0;
                _activeItemCount = 0;

                ref OctreeNode root = ref _nodes[0];
                root.Bounds = _rootBounds;
                root.Center = _rootBounds.Center;
                root.ChildBaseIndex = NodeIndex.Null;
                root.FirstItemIndex = SlotIndex.Invalid;
                root.ParentIndex = NodeIndex.Null;
                root.ItemCount = 0;
                root.ChildMask = 0;
                root.Depth = TreeDepth.Root;
                _nodeCount = 1;
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
            Interlocked.Exchange(ref _state, OctreeState.Faulted);
            return;
        }

        int current = Volatile.Read(ref _state);
        while (current == OctreeState.Active)
        {
            int witnessed = Interlocked.CompareExchange(ref _state, OctreeState.Draining, current);
            if (witnessed == current)
            {
                break;
            }

            current = witnessed;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        Complete();

        SpinWait spinner = default;
        while (Volatile.Read(ref _inFlightOperations) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinner.SpinOnce();
            if (spinner.NextSpinWillYield)
            {
                await Task.Yield();
            }
        }

        await DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public unsafe void Dispose()
    {
        if (Interlocked.Exchange(ref _state, OctreeState.Disposed) == OctreeState.Disposed)
        {
            return;
        }

        SpinWait spinner = default;
        while (Volatile.Read(ref _inFlightOperations) > 0)
        {
            spinner.SpinOnce();
        }

        lock (_gate)
        {
            FreeNative();
            _nodeCapacity = 0;
            _nodeCount = 0;
            _itemCapacity = 0;
            _itemWatermark = 0;
            _activeItemCount = 0;
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public unsafe ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    internal unsafe ref OctreeNode NodeAt(uint index) => ref _nodes[index];

    internal unsafe ref OctreeItem<T> ItemAt(uint slot) => ref _items[slot];

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsInvalidDomain(in Aabb3D bounds)
    {
        Vec3 min = bounds.Min;
        Vec3 max = bounds.Max;
        return float.IsNaN(min.X) || float.IsNaN(min.Y) || float.IsNaN(min.Z) ||
               float.IsNaN(max.X) || float.IsNaN(max.Y) || float.IsNaN(max.Z) ||
               min.X > max.X || min.Y > max.Y || min.Z > max.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int ClassifyOctant(Vec3 center, in Aabb3D itemBounds)
    {
        Vec3 min = itemBounds.Min;
        Vec3 max = itemBounds.Max;
        int octant = 0;

        if (min.X >= center.X)
        {
            octant |= 1;
        }
        else if (max.X > center.X)
        {
            return -1;
        }

        if (min.Y >= center.Y)
        {
            octant |= 2;
        }
        else if (max.Y > center.Y)
        {
            return -1;
        }

        if (min.Z >= center.Z)
        {
            octant |= 4;
        }
        else if (max.Z > center.Z)
        {
            return -1;
        }

        return octant;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Aabb3D CalculateChildBounds(in Aabb3D parentBounds, Vec3 center, int octant)
    {
        Vec3 parentMin = parentBounds.Min;
        Vec3 parentMax = parentBounds.Max;

        Vec3 min = new(
            (octant & 1) != 0 ? center.X : parentMin.X,
            (octant & 2) != 0 ? center.Y : parentMin.Y,
            (octant & 4) != 0 ? center.Z : parentMin.Z);

        Vec3 max = new(
            (octant & 1) != 0 ? parentMax.X : center.X,
            (octant & 2) != 0 ? parentMax.Y : center.Y,
            (octant & 4) != 0 ? parentMax.Z : center.Z);

        return new Aabb3D(min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe NodeIndex NavigateTargetNode(NodeIndex rootIndex, in Aabb3D bounds)
    {
        NodeIndex current = rootIndex;

        while (true)
        {
            ref OctreeNode node = ref _nodes[current.Value];

            if (node.ChildBaseIndex.IsValid)
            {
                int octant = ClassifyOctant(node.Center, in bounds);
                if (octant != -1)
                {
                    current = new NodeIndex(node.ChildBaseIndex.Value + octant);
                    continue;
                }

                return current;
            }

            if (!TPartition.ShouldSplit(node.ItemCount, node.Depth))
            {
                return current;
            }

            SplitNode(current);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void SplitNode(NodeIndex nodeIndex)
    {
        NodeIndex childBase = RentNodeBlock();
        ref OctreeNode parent = ref _nodes[nodeIndex.Value];
        Vec3 center = parent.Center;
        TreeDepth childDepth = parent.Depth.Increment();

        for (int i = 0; i < 8; i++)
        {
            ref OctreeNode child = ref _nodes[childBase.Value + i];
            child.Bounds = CalculateChildBounds(in parent.Bounds, center, i);
            child.Center = child.Bounds.Center;
            child.ChildBaseIndex = NodeIndex.Null;
            child.FirstItemIndex = SlotIndex.Invalid;
            child.ParentIndex = nodeIndex;
            child.ItemCount = 0;
            child.ChildMask = 0;
            child.Depth = childDepth;
        }

        parent.ChildBaseIndex = childBase;
        parent.ChildMask = 0xFF;
        TMetrics.OnNodeSplit(parent.Depth);

        SlotIndex currentItem = parent.FirstItemIndex;
        parent.FirstItemIndex = SlotIndex.Invalid;
        parent.ItemCount = 0;

        while (currentItem.IsValid)
        {
            ref OctreeItem<T> item = ref _items[currentItem.Value];
            SlotIndex nextItem = item.NextItemIndex;
            int octant = ClassifyOctant(center, in item.Bounds);

            if (octant != -1)
            {
                LinkItem(currentItem, new NodeIndex(childBase.Value + octant));
            }
            else
            {
                item.NextItemIndex = parent.FirstItemIndex;
                parent.FirstItemIndex = currentItem;
                parent.ItemCount++;
                item.HomeNode = nodeIndex;
            }

            currentItem = nextItem;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void LinkItem(SlotIndex itemSlot, NodeIndex nodeIndex)
    {
        ref OctreeNode node = ref _nodes[nodeIndex.Value];
        ref OctreeItem<T> item = ref _items[itemSlot.Value];

        item.NextItemIndex = node.FirstItemIndex;
        node.FirstItemIndex = itemSlot;
        node.ItemCount++;
        item.HomeNode = nodeIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void UnlinkItem(SlotIndex itemSlot, NodeIndex nodeIndex)
    {
        ref OctreeNode node = ref _nodes[nodeIndex.Value];
        SlotIndex current = node.FirstItemIndex;
        SlotIndex previous = SlotIndex.Invalid;

        while (current.IsValid)
        {
            if (current == itemSlot)
            {
                if (!previous.IsValid)
                {
                    node.FirstItemIndex = _items[current.Value].NextItemIndex;
                }
                else
                {
                    _items[previous.Value].NextItemIndex = _items[current.Value].NextItemIndex;
                }

                node.ItemCount--;
                _items[current.Value].NextItemIndex = SlotIndex.Invalid;
                return;
            }

            previous = current;
            current = _items[current.Value].NextItemIndex;
        }

        ThrowHelper.ThrowCorruptionDetected();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void PruneBranch(NodeIndex nodeIndex)
    {
        NodeIndex current = nodeIndex;

        while (current.IsValid)
        {
            ref OctreeNode node = ref _nodes[current.Value];
            if (!node.ChildBaseIndex.IsValid)
            {
                current = node.ParentIndex;
                continue;
            }

            if (node.ItemCount > 0)
            {
                return;
            }

            // True subtree total: direct counts miss items held by deeper
            // levels, and collapsing on those would orphan live branches.
            uint liveBelow = CountSubtreeItems(node.ChildBaseIndex);
            if (liveBelow > ushort.MaxValue || !TPartition.ShouldPrune((ushort)liveBelow, node.Depth))
            {
                return;
            }

            CollapseSubtree(node.ChildBaseIndex);
            node.ChildBaseIndex = NodeIndex.Null;
            node.ChildMask = 0;
            TMetrics.OnNodeMerged(node.Depth);

            current = node.ParentIndex;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe uint CountSubtreeItems(NodeIndex childBase)
    {
        uint total = 0;
        for (int i = 0; i < 8; i++)
        {
            ref OctreeNode child = ref _nodes[childBase.Value + i];
            total += child.ItemCount;
            if (total > ushort.MaxValue)
            {
                return total;
            }

            if (child.ChildBaseIndex.IsValid)
            {
                total += CountSubtreeItems(child.ChildBaseIndex);
                if (total > ushort.MaxValue)
                {
                    return total;
                }
            }
        }

        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void CollapseSubtree(NodeIndex childBase)
    {
        for (int i = 0; i < 8; i++)
        {
            ref OctreeNode child = ref _nodes[childBase.Value + i];
            if (child.ChildBaseIndex.IsValid)
            {
                CollapseSubtree(child.ChildBaseIndex);
                child.ChildBaseIndex = NodeIndex.Null;
                child.ChildMask = 0;
            }
        }

        ReturnNodeBlock(childBase);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe NodeIndex RentNodeBlock()
    {
        if (_freeNodeBlockHead.IsValid)
        {
            NodeIndex reused = _freeNodeBlockHead;
            _freeNodeBlockHead = _nodes[reused.Value].ChildBaseIndex;
            return reused;
        }

        if (_nodeCount + 8 > _nodeCapacity)
        {
            GrowNodeMemory();
        }

        NodeIndex allocated = new((int)_nodeCount);
        _nodeCount += 8;
        return allocated;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ReturnNodeBlock(NodeIndex childBase)
    {
        _nodes[childBase.Value].ChildBaseIndex = _freeNodeBlockHead;
        _freeNodeBlockHead = childBase;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void GrowNodeMemory()
    {
        uint newCap = checked(_nodeCapacity * 2);
        nuint newBytes = checked((nuint)newCap * (nuint)sizeof(OctreeNode));
        OctreeNode* newMem = (OctreeNode*)NativeMemory.AlignedAlloc(newBytes, 64);
        NativeMemory.Clear(newMem, newBytes);

        nuint oldBytes = checked((nuint)_nodeCapacity * (nuint)sizeof(OctreeNode));
        NativeMemory.Copy(_nodes, newMem, oldBytes);

        if (_nodes != null && _retiredCount < 32)
        {
            _retiredBlocks[_retiredCount++] = (nuint)_nodes;
        }

        // Plain publish: readers take plain reads and retired blocks stay mapped
        // until dispose, so a stale view still walks valid memory; the release
        // side needs no stronger primitive than the array copy above it.
        _nodes = newMem;
        _nodeCapacity = newCap;

        TMetrics.OnMemoryResized(newBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe SlotIndex RentItemSlot()
    {
        if (_freeItemHead.IsValid)
        {
            SlotIndex slot = _freeItemHead;
            _freeItemHead = _items[slot.Value].NextItemIndex;
            _items[slot.Value].Generation = new Generation(NextItemGeneration());
            return slot;
        }

        if (_itemWatermark >= _itemCapacity)
        {
            GrowItemMemory();
        }

        SlotIndex newSlot = new(_itemWatermark++);
        _items[newSlot.Value].Generation = new Generation(NextItemGeneration());
        return newSlot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint NextItemGeneration()
    {
        uint next = _itemGenCounter + 1;
        _itemGenCounter = next == 0 ? 1 : next;
        return _itemGenCounter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ReturnItemSlot(SlotIndex slot)
    {
        ref OctreeItem<T> item = ref _items[slot.Value];
        // Bump on return AND stamp on rent: the bump kills stale handles
        // while the slot sits free (double-remove, use-after-remove), the
        // rent stamp kills them across Clear (which memsets history away).
        // Either alone leaves a resurrection window.
        item.Generation = item.Generation.Next();
        item.NextItemIndex = _freeItemHead;
        _freeItemHead = slot;
        item.HomeNode = NodeIndex.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void GrowItemMemory()
    {
        uint newCap = checked(_itemCapacity * 2);
        nuint newBytes = checked((nuint)newCap * (nuint)sizeof(OctreeItem<T>));

        OctreeItem<T>* newMem = (OctreeItem<T>*)NativeMemory.AlignedAlloc(newBytes, 64);
        NativeMemory.Clear(newMem, newBytes);

        nuint oldBytes = checked((nuint)_itemCapacity * (nuint)sizeof(OctreeItem<T>));
        NativeMemory.Copy(_items, newMem, oldBytes);

        if (_items != null && _retiredCount < 32)
        {
            _retiredBlocks[_retiredCount++] = (nuint)_items;
        }

        // Plain publish: see GrowNodeMemory for why a stale view stays safe.
        _items = newMem;
        _itemCapacity = newCap;

        TMetrics.OnMemoryResized(newBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryEnterOperation()
    {
        while (true)
        {
            int state = Volatile.Read(ref _state);
            if (state != OctreeState.Active)
            {
                return false;
            }

            Interlocked.Increment(ref _inFlightOperations);
            if (Volatile.Read(ref _state) == OctreeState.Active)
            {
                return true;
            }

            Interlocked.Decrement(ref _inFlightOperations);
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExitOperation()
    {
        Interlocked.Decrement(ref _inFlightOperations);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateActiveState()
    {
        if (Volatile.Read(ref _state) != OctreeState.Active)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(NativeOctree<T, TPartition, TMetrics>));
        }
    }

    private unsafe void FreeNative()
    {
        for (int i = 0; i < _retiredCount; i++)
        {
            void* block = (void*)_retiredBlocks[i];
            if (block != null)
            {
                NativeMemory.AlignedFree(block);
                _retiredBlocks[i] = 0;
            }
        }

        _retiredCount = 0;

        if (_nodes != null)
        {
            NativeMemory.AlignedFree(_nodes);
            _nodes = null;
        }

        if (_items != null)
        {
            NativeMemory.AlignedFree(_items);
            _items = null;
        }
    }
}
