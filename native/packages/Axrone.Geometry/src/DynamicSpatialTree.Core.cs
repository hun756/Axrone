namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// One node of a generational spatial tree: the fattened broad-phase bounds,
/// the exact tight bounds, the structural links, the subtree height, the
/// allocation epoch and the inline payload. Leaves carry items; internal nodes
/// carry merged fat bounds only. Properties rather than fields so the public
/// surface stays within the visible-field rules; the tree itself still reaches
/// slots by reference.
/// </summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DynamicTreeNode<T>
{
    /// <summary>Broad-phase bounds, grown by the expansion policy.</summary>
    public Aabb3D FatBox { get; set; }

    /// <summary>Left child slot; null on leaves.</summary>
    public NodeIndex Left { get; set; }

    /// <summary>Right child slot; null on leaves.</summary>
    public NodeIndex Right { get; set; }

    /// <summary>Parent slot; null on the root.</summary>
    public NodeIndex Parent { get; set; }

    /// <summary>Height of the subtree rooted here; zero on leaves, negative on free slots.</summary>
    public int Height { get; set; }

    /// <summary>Allocation epoch of this slot; bumped on every recycle.</summary>
    public NodeGeneration Generation { get; set; }

    /// <summary>Exact bounds last reported for the item; meaningful on leaves.</summary>
    public Aabb3D TightBox { get; set; }

    /// <summary>Payload carried by a leaf.</summary>
    public T Payload { get; set; }

    /// <summary>Whether this node is a leaf, which is exactly when it has no left child.</summary>
    public readonly bool IsLeaf
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Left.IsNull;
    }

    /// <summary>Whether this slot is on the free list.</summary>
    public readonly bool IsFree
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Height < 0;
    }
}

/// <summary>
/// Control word plus pool counters, isolated on separate cache lines so a
/// reader spinning on disposal never contends with the writer's counters.
/// Namespace-level because the CLR forbids explicit layout on types nested in
/// a generic class.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 192)]
internal struct SpatialTopologyState
{
    [FieldOffset(0)]
    public volatile int Disposed;

    [FieldOffset(64)]
    public NodeIndex Root;

    [FieldOffset(68)]
    public NodeIndex FreeList;

    [FieldOffset(72)]
    public int Capacity;

    [FieldOffset(76)]
    public int Count;

    [FieldOffset(128)]
    public float FatMargin;

    [FieldOffset(132)]
    public float VelocityMultiplier;
}

/// <summary>
/// Generational dynamic AABB tree: every stored box is fattened so an item can
/// creep without a structural update, insertion descends by the SAH cost model
/// and rebalances with local rotations, and handles pair a slot with its
/// allocation epoch so a recycled slot never answers to a stale handle.
/// </summary>
/// <remarks>
/// Broad-phase tests run against the fat boxes while leaf reports carry the
/// tight bounds, so queries stay exact even though the tree stores slack.
/// Ray traversal uses the same simple scalar slab test as the professional box;
/// the raycast overhaul reuses this shape with a richer primitive.
/// </remarks>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
/// <typeparam name="TExpansionPolicy">Fattening policy; a struct for dispatch without virtual calls.</typeparam>
/// <typeparam name="TSAHPolicy">Insertion cost model; a struct for the same reason.</typeparam>
public partial class DynamicSpatialTree<T, TExpansionPolicy, TSAHPolicy> : ISpatialIndex<T>
    where TExpansionPolicy : struct, IFatExpansionPolicy
    where TSAHPolicy : struct, ISurfaceAreaHeuristicPolicy
{
    private readonly Lock _gate = new();
    internal SpatialTopologyState _state;
    internal DynamicTreeNode<T>[] _nodes;

    /// <summary>Creates a tree with explicit tuning.</summary>
    public DynamicSpatialTree(TreeCapacity capacity, float fatMargin = 0.2f, float velocityMultiplier = 2.0f)
    {
        if (fatMargin < 0.0f || float.IsNaN(fatMargin))
        {
            ThrowHelper.ThrowInvalidFatMargin();
        }

        if (velocityMultiplier < 1.0f || float.IsNaN(velocityMultiplier))
        {
            ThrowHelper.ThrowInvalidVelocityMultiplier();
        }

        _state.FatMargin = fatMargin;
        _state.VelocityMultiplier = velocityMultiplier;
        _state.Capacity = capacity.Value;
        _state.Root = NodeIndex.Null;
        _state.FreeList = new NodeIndex(0);
        _state.Count = 0;
        _state.Disposed = 0;

        _nodes = GC.AllocateArray<DynamicTreeNode<T>>(_state.Capacity, pinned: true);
        InitializeFreeRange(_nodes, 0, _state.Capacity);
    }

    /// <summary>Creates a tree from an options object; defaults when omitted.</summary>
    public DynamicSpatialTree(SpatialTreeOptions? options = null)
        : this(options?.InitialCapacity ?? new TreeCapacity(1024), options?.FatMargin ?? 0.2f, options?.VelocityMultiplier ?? 2.0f)
    {
    }

    /// <summary>Number of live items in the tree.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            using (_gate.EnterScope())
            {
                return _state.Count;
            }
        }
    }

    /// <summary>Slots the node pool currently holds.</summary>
    public TreeCapacity Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            using (_gate.EnterScope())
            {
                return new TreeCapacity(_state.Capacity);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public SpatialHandle Insert(in Aabb3D bounds, T payload)
    {
        using (_gate.EnterScope())
        {
            EnsureNotDisposed();

            NodeIndex nodeId = AllocateNode();
            ref DynamicTreeNode<T> node = ref GetNodeRef(nodeId);

            node.TightBox = bounds;
            node.FatBox = TExpansionPolicy.Expand(in bounds, _state.FatMargin);
            node.Payload = payload;
            node.Height = 0;
            node.Left = NodeIndex.Null;
            node.Right = NodeIndex.Null;

            InsertLeaf(nodeId);

            _state.Count++;
            return SpatialHandle.Create(nodeId, node.Generation);
        }
    }

    /// <inheritdoc/>
    public int InsertRange(params ReadOnlySpan<SpatialEntry<T>> entries)
    {
        return InsertRange(entries, Span<SpatialHandle>.Empty);
    }

    /// <inheritdoc/>
    public int InsertRange(ReadOnlySpan<SpatialEntry<T>> entries, Span<SpatialHandle> handles)
    {
        bool writeHandles = !handles.IsEmpty;
        if (writeHandles && handles.Length < entries.Length)
        {
            ThrowHelper.ThrowHandlesSpanTooShort();
        }

        using (_gate.EnterScope())
        {
            EnsureNotDisposed();

            for (int i = 0; i < entries.Length; i++)
            {
                SpatialEntry<T> entry = entries[i];
                NodeIndex nodeId = AllocateNode();
                ref DynamicTreeNode<T> node = ref GetNodeRef(nodeId);

                Aabb3D entryBounds = entry.Bounds;
                node.TightBox = entryBounds;
                node.FatBox = TExpansionPolicy.Expand(in entryBounds, _state.FatMargin);
                node.Payload = entry.Payload;
                node.Height = 0;
                node.Left = NodeIndex.Null;
                node.Right = NodeIndex.Null;

                InsertLeaf(nodeId);

                _state.Count++;
                if (writeHandles)
                {
                    handles[i] = SpatialHandle.Create(nodeId, node.Generation);
                }
            }

            return entries.Length;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Update(SpatialHandle handle, in Aabb3D newBounds, Vec3 displacement = default)
    {
        return UpdateEx(handle, in newBounds, displacement).Outcome == TreeUpdateOutcome.Reinserted;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TreeUpdateResult UpdateEx(SpatialHandle handle, in Aabb3D newBounds, Vec3 displacement = default)
    {
        if (!handle.IsValid)
        {
            return new TreeUpdateResult(TreeUpdateOutcome.HandleNotFound, handle);
        }

        using (_gate.EnterScope())
        {
            if (_state.Disposed != 0)
            {
                return new TreeUpdateResult(TreeUpdateOutcome.Disposed, handle);
            }

            NodeIndex nodeId = handle.Index;
            if ((uint)nodeId.Value >= (uint)_state.Capacity)
            {
                return new TreeUpdateResult(TreeUpdateOutcome.HandleNotFound, handle);
            }

            ref DynamicTreeNode<T> node = ref GetNodeRef(nodeId);
            if (!node.IsLeaf || node.Generation != handle.Generation)
            {
                return new TreeUpdateResult(TreeUpdateOutcome.HandleNotFound, handle);
            }

            if (node.FatBox.Contains(in newBounds))
            {
                node.TightBox = newBounds;
                return new TreeUpdateResult(TreeUpdateOutcome.BoundsUnchanged, handle);
            }

            RemoveLeaf(nodeId);

            node.TightBox = newBounds;
            node.FatBox = TExpansionPolicy.ExpandWithDisplacement(in newBounds, displacement, _state.FatMargin, _state.VelocityMultiplier);

            InsertLeaf(nodeId);
            return new TreeUpdateResult(TreeUpdateOutcome.Reinserted, handle);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Remove(SpatialHandle handle)
    {
        if (!handle.IsValid)
        {
            return false;
        }

        using (_gate.EnterScope())
        {
            EnsureNotDisposed();

            NodeIndex nodeId = handle.Index;
            if ((uint)nodeId.Value >= (uint)_state.Capacity)
            {
                return false;
            }

            ref DynamicTreeNode<T> node = ref GetNodeRef(nodeId);
            if (!node.IsLeaf || node.Generation != handle.Generation)
            {
                return false;
            }

            RemoveLeaf(nodeId);
            FreeNode(nodeId);

            _state.Count--;
            return true;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetBounds(SpatialHandle handle, out Aabb3D bounds)
    {
        if (!handle.IsValid)
        {
            bounds = default;
            return false;
        }

        using (_gate.EnterScope())
        {
            EnsureNotDisposed();

            NodeIndex nodeId = handle.Index;
            if ((uint)nodeId.Value >= (uint)_state.Capacity)
            {
                bounds = default;
                return false;
            }

            ref readonly DynamicTreeNode<T> node = ref GetNodeRef(nodeId);
            if (!node.IsLeaf || node.Generation != handle.Generation)
            {
                bounds = default;
                return false;
            }

            bounds = node.TightBox;
            return true;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetPayload(SpatialHandle handle, [MaybeNullWhen(false)] out T payload)
    {
        if (!handle.IsValid)
        {
            payload = default;
            return false;
        }

        using (_gate.EnterScope())
        {
            EnsureNotDisposed();

            NodeIndex nodeId = handle.Index;
            if ((uint)nodeId.Value >= (uint)_state.Capacity)
            {
                payload = default;
                return false;
            }

            ref readonly DynamicTreeNode<T> node = ref GetNodeRef(nodeId);
            if (!node.IsLeaf || node.Generation != handle.Generation)
            {
                payload = default;
                return false;
            }

            payload = node.Payload;
            return true;
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        using (_gate.EnterScope())
        {
            EnsureNotDisposed();

            _state.Root = NodeIndex.Null;
            _state.FreeList = new NodeIndex(0);
            _state.Count = 0;

            InitializeFreeRange(_nodes, 0, _state.Capacity);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the pool; the tree answers disposed from then on.</summary>
    /// <param name="disposing">Whether managed state may be touched.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _state.Disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            using (_gate.EnterScope())
            {
                _state.Root = NodeIndex.Null;
                _state.FreeList = NodeIndex.Null;
                _state.Count = 0;
                Array.Clear(_nodes, 0, _state.Capacity);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref DynamicTreeNode<T> GetNodeRef(NodeIndex index) => ref _nodes[index.Value];

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float HalfArea(Aabb3D box) => box.SurfaceArea() * 0.5f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InitializeFreeRange(DynamicTreeNode<T>[] nodes, int start, int length)
    {
        int end = start + length;
        for (int i = start; i < end - 1; i++)
        {
            nodes[i].Parent = new NodeIndex(i + 1);
            nodes[i].Left = NodeIndex.Null;
            nodes[i].Right = NodeIndex.Null;
            nodes[i].Height = -1;
            nodes[i].Generation = NodeGeneration.Initial;
        }

        nodes[end - 1].Parent = NodeIndex.Null;
        nodes[end - 1].Left = NodeIndex.Null;
        nodes[end - 1].Right = NodeIndex.Null;
        nodes[end - 1].Height = -1;
        nodes[end - 1].Generation = NodeGeneration.Initial;
    }

    private void InsertLeaf(NodeIndex leaf)
    {
        if (_state.Root.IsNull)
        {
            _state.Root = leaf;
            GetNodeRef(leaf).Parent = NodeIndex.Null;
            return;
        }

        // Captured by value: the parent allocation below may grow the pool and
        // relocate the array, so no reference taken before it stays valid.
        Aabb3D leafFatBox = GetNodeRef(leaf).FatBox;
        float leafArea = HalfArea(leafFatBox);
        NodeIndex current = _state.Root;

        while (!GetNodeRef(current).IsLeaf)
        {
            Aabb3D currentFatBox = GetNodeRef(current).FatBox;
            NodeIndex child1 = GetNodeRef(current).Left;
            NodeIndex child2 = GetNodeRef(current).Right;

            float area = HalfArea(currentFatBox);
            float combinedArea = HalfArea(Aabb3D.CreateMerged(in currentFatBox, in leafFatBox));

            float cost = TSAHPolicy.ComputeCombinedCost(combinedArea);
            float inheritanceCost = TSAHPolicy.ComputeInheritanceCost(combinedArea, area);

            Aabb3D child1FatBox = GetNodeRef(child1).FatBox;
            float child1CombinedArea = HalfArea(Aabb3D.CreateMerged(in leafFatBox, in child1FatBox));
            float cost1;
            if (GetNodeRef(child1).IsLeaf)
            {
                cost1 = TSAHPolicy.ComputeDescentCost(leafArea, 0.0f, child1CombinedArea, true, inheritanceCost);
            }
            else
            {
                float oldArea = HalfArea(child1FatBox);
                cost1 = TSAHPolicy.ComputeDescentCost(leafArea, oldArea, child1CombinedArea, false, inheritanceCost);
            }

            Aabb3D child2FatBox = GetNodeRef(child2).FatBox;
            float child2CombinedArea = HalfArea(Aabb3D.CreateMerged(in leafFatBox, in child2FatBox));
            float cost2;
            if (GetNodeRef(child2).IsLeaf)
            {
                cost2 = TSAHPolicy.ComputeDescentCost(leafArea, 0.0f, child2CombinedArea, true, inheritanceCost);
            }
            else
            {
                float oldArea = HalfArea(child2FatBox);
                cost2 = TSAHPolicy.ComputeDescentCost(leafArea, oldArea, child2CombinedArea, false, inheritanceCost);
            }

            if (cost < cost1 && cost < cost2)
            {
                break;
            }

            current = cost1 < cost2 ? child1 : child2;
        }

        NodeIndex sibling = current;
        NodeIndex oldParent = GetNodeRef(sibling).Parent;
        NodeIndex newParent = AllocateNode();

        Aabb3D siblingFatBox = GetNodeRef(sibling).FatBox;
        int siblingHeight = GetNodeRef(sibling).Height;

        ref DynamicTreeNode<T> newParentNode = ref GetNodeRef(newParent);
        newParentNode.Parent = oldParent;
        newParentNode.FatBox = Aabb3D.CreateMerged(in leafFatBox, in siblingFatBox);
        newParentNode.Height = siblingHeight + 1;

        if (oldParent.IsValid)
        {
            if (GetNodeRef(oldParent).Left == sibling)
            {
                GetNodeRef(oldParent).Left = newParent;
            }
            else
            {
                GetNodeRef(oldParent).Right = newParent;
            }

            newParentNode.Left = sibling;
            newParentNode.Right = leaf;
            GetNodeRef(sibling).Parent = newParent;
            GetNodeRef(leaf).Parent = newParent;
        }
        else
        {
            newParentNode.Left = sibling;
            newParentNode.Right = leaf;
            GetNodeRef(sibling).Parent = newParent;
            GetNodeRef(leaf).Parent = newParent;
            _state.Root = newParent;
        }

        NodeIndex walk = GetNodeRef(leaf).Parent;
        while (walk.IsValid)
        {
            walk = Balance(walk);

            ref DynamicTreeNode<T> walkNode = ref GetNodeRef(walk);
            NodeIndex c1 = walkNode.Left;
            NodeIndex c2 = walkNode.Right;

            Aabb3D c1FatBox = GetNodeRef(c1).FatBox;
            Aabb3D c2FatBox = GetNodeRef(c2).FatBox;
            walkNode.Height = 1 + Math.Max(GetNodeRef(c1).Height, GetNodeRef(c2).Height);
            walkNode.FatBox = Aabb3D.CreateMerged(in c1FatBox, in c2FatBox);

            walk = walkNode.Parent;
        }
    }

    private void RemoveLeaf(NodeIndex leaf)
    {
        if (leaf == _state.Root)
        {
            _state.Root = NodeIndex.Null;
            return;
        }

        NodeIndex parent = GetNodeRef(leaf).Parent;
        NodeIndex grandParent = GetNodeRef(parent).Parent;
        NodeIndex sibling = GetNodeRef(parent).Left == leaf ? GetNodeRef(parent).Right : GetNodeRef(parent).Left;

        if (grandParent.IsValid)
        {
            if (GetNodeRef(grandParent).Left == parent)
            {
                GetNodeRef(grandParent).Left = sibling;
            }
            else
            {
                GetNodeRef(grandParent).Right = sibling;
            }

            GetNodeRef(sibling).Parent = grandParent;
            FreeNode(parent);

            NodeIndex walk = grandParent;
            while (walk.IsValid)
            {
                walk = Balance(walk);

                ref DynamicTreeNode<T> walkNode = ref GetNodeRef(walk);
                NodeIndex c1 = walkNode.Left;
                NodeIndex c2 = walkNode.Right;

                Aabb3D c1FatBox = GetNodeRef(c1).FatBox;
                Aabb3D c2FatBox = GetNodeRef(c2).FatBox;
                walkNode.FatBox = Aabb3D.CreateMerged(in c1FatBox, in c2FatBox);
                walkNode.Height = 1 + Math.Max(GetNodeRef(c1).Height, GetNodeRef(c2).Height);

                walk = walkNode.Parent;
            }
        }
        else
        {
            _state.Root = sibling;
            GetNodeRef(sibling).Parent = NodeIndex.Null;
            FreeNode(parent);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private NodeIndex Balance(NodeIndex indexA)
    {
        ref DynamicTreeNode<T> a = ref GetNodeRef(indexA);
        if (a.IsLeaf || a.Height < 2)
        {
            return indexA;
        }

        NodeIndex indexB = a.Left;
        NodeIndex indexC = a.Right;
        ref DynamicTreeNode<T> b = ref GetNodeRef(indexB);
        ref DynamicTreeNode<T> c = ref GetNodeRef(indexC);

        int balance = c.Height - b.Height;

        if (balance > 1)
        {
            NodeIndex indexF = c.Left;
            NodeIndex indexG = c.Right;
            ref DynamicTreeNode<T> f = ref GetNodeRef(indexF);
            ref DynamicTreeNode<T> g = ref GetNodeRef(indexG);

            c.Left = indexA;
            c.Parent = a.Parent;
            a.Parent = indexC;

            if (c.Parent.IsValid)
            {
                if (GetNodeRef(c.Parent).Left == indexA)
                {
                    GetNodeRef(c.Parent).Left = indexC;
                }
                else
                {
                    GetNodeRef(c.Parent).Right = indexC;
                }
            }
            else
            {
                _state.Root = indexC;
            }

            if (f.Height > g.Height)
            {
                c.Right = indexF;
                a.Right = indexG;
                g.Parent = indexA;

                Aabb3D bBox = b.FatBox;
                Aabb3D gBox = g.FatBox;
                Aabb3D fBox = f.FatBox;
                a.FatBox = Aabb3D.CreateMerged(in bBox, in gBox);
                Aabb3D aBox = a.FatBox;
                c.FatBox = Aabb3D.CreateMerged(in aBox, in fBox);

                a.Height = 1 + Math.Max(b.Height, g.Height);
                c.Height = 1 + Math.Max(a.Height, f.Height);
            }
            else
            {
                c.Right = indexG;
                a.Right = indexF;
                f.Parent = indexA;

                Aabb3D bBox = b.FatBox;
                Aabb3D fBox = f.FatBox;
                Aabb3D gBox = g.FatBox;
                a.FatBox = Aabb3D.CreateMerged(in bBox, in fBox);
                Aabb3D aBox = a.FatBox;
                c.FatBox = Aabb3D.CreateMerged(in aBox, in gBox);

                a.Height = 1 + Math.Max(b.Height, f.Height);
                c.Height = 1 + Math.Max(a.Height, g.Height);
            }

            return indexC;
        }

        if (balance < -1)
        {
            NodeIndex indexD = b.Left;
            NodeIndex indexE = b.Right;
            ref DynamicTreeNode<T> d = ref GetNodeRef(indexD);
            ref DynamicTreeNode<T> e = ref GetNodeRef(indexE);

            b.Left = indexA;
            b.Parent = a.Parent;
            a.Parent = indexB;

            if (b.Parent.IsValid)
            {
                if (GetNodeRef(b.Parent).Left == indexA)
                {
                    GetNodeRef(b.Parent).Left = indexB;
                }
                else
                {
                    GetNodeRef(b.Parent).Right = indexB;
                }
            }
            else
            {
                _state.Root = indexB;
            }

            if (d.Height > e.Height)
            {
                b.Right = indexD;
                a.Left = indexE;
                e.Parent = indexA;

                Aabb3D cBox = c.FatBox;
                Aabb3D eBox = e.FatBox;
                Aabb3D dBox = d.FatBox;
                a.FatBox = Aabb3D.CreateMerged(in cBox, in eBox);
                Aabb3D aBox = a.FatBox;
                b.FatBox = Aabb3D.CreateMerged(in aBox, in dBox);

                a.Height = 1 + Math.Max(c.Height, e.Height);
                b.Height = 1 + Math.Max(a.Height, d.Height);
            }
            else
            {
                b.Right = indexE;
                a.Left = indexD;
                d.Parent = indexA;

                Aabb3D cBox = c.FatBox;
                Aabb3D dBox = d.FatBox;
                Aabb3D eBox = e.FatBox;
                a.FatBox = Aabb3D.CreateMerged(in cBox, in dBox);
                Aabb3D aBox = a.FatBox;
                b.FatBox = Aabb3D.CreateMerged(in aBox, in eBox);

                a.Height = 1 + Math.Max(c.Height, d.Height);
                b.Height = 1 + Math.Max(a.Height, e.Height);
            }

            return indexB;
        }

        return indexA;
    }

    private NodeIndex AllocateNode()
    {
        if (_state.FreeList.IsNull)
        {
            GrowCapacity();
        }

        NodeIndex nodeId = _state.FreeList;
        _state.FreeList = GetNodeRef(nodeId).Parent;

        ref DynamicTreeNode<T> node = ref GetNodeRef(nodeId);
        node.Parent = NodeIndex.Null;
        node.Left = NodeIndex.Null;
        node.Right = NodeIndex.Null;
        node.Height = 0;

        return nodeId;
    }

    private void FreeNode(NodeIndex nodeId)
    {
        ref DynamicTreeNode<T> node = ref GetNodeRef(nodeId);
        node.Parent = _state.FreeList;
        node.Height = -1;
        node.Left = NodeIndex.Null;
        node.Right = NodeIndex.Null;
        node.Generation = node.Generation.Next();
        node.Payload = default!;

        _state.FreeList = nodeId;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowCapacity()
    {
        int oldCapacity = _state.Capacity;
        int newCapacity = oldCapacity * 2;
        DynamicTreeNode<T>[] newNodes = GC.AllocateArray<DynamicTreeNode<T>>(newCapacity, pinned: true);

        Array.Copy(_nodes, newNodes, oldCapacity);
        InitializeFreeRange(newNodes, oldCapacity, newCapacity - oldCapacity);

        _state.FreeList = new NodeIndex(oldCapacity);
        _nodes = newNodes;
        _state.Capacity = newCapacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureNotDisposed()
    {
        if (_state.Disposed != 0)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(DynamicSpatialTree<T, TExpansionPolicy, TSAHPolicy>));
        }
    }
}

/// <summary>
/// Generational dynamic AABB tree with the default fattening and SAH policies.
/// </summary>
/// <typeparam name="T">Payload type stored alongside each handle.</typeparam>
public class DynamicSpatialTree<T> : DynamicSpatialTree<T, DefaultExpansionPolicy, StandardSAHPolicy>
{
    /// <summary>Creates a tree from an options object; defaults when omitted.</summary>
    public DynamicSpatialTree(SpatialTreeOptions? options = null)
        : base(options)
    {
    }

    /// <summary>Creates a tree with explicit tuning.</summary>
    public DynamicSpatialTree(TreeCapacity capacity, float fatMargin = 0.2f, float velocityMultiplier = 2.0f)
        : base(capacity, fatMargin, velocityMultiplier)
    {
    }
}
