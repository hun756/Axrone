namespace Axrone.Geometry;

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Dynamic AABB tree with fattened bounds, strategy-driven insertion and a
/// bulk-drain lifecycle. Every stored box is grown by a fattening margin so an
/// item can creep without forcing a structural update, and a moving item buys
/// extra slack proportional to its speed. Insertion descends by the
/// <typeparamref name="TStrategy"/> cost model and rebalances with local
/// rotations, so the hot path stays allocation-free once the node pool grows.
/// </summary>
/// <remarks>
/// The tree follows a single-writer discipline: mutations are expected from one
/// worker thread at a time, while reads (<see cref="ISpatialReader{TUserData}"/>)
/// stay lock-free and never invalidate an identity, a fat bound, or the tree
/// shape. <see cref="Complete"/> closes the submission stream and
/// <see cref="DrainAsync"/> awaits the queued work, so a caller never has to
/// expose its own synchronization primitives.
/// </remarks>
/// <typeparam name="TUserData">Payload type stored alongside each item identity.</typeparam>
/// <typeparam name="TStrategy">
/// Insertion policy; a struct so the hot path calls straight into the generic
/// instantiation with no closure and no virtual dispatch.
/// </typeparam>
/// <typeparam name="TMetrics">
/// Traversal instrumentation sink; a struct for the same reason.
/// </typeparam>
public sealed partial class DynamicAabbTree<TUserData, TStrategy, TMetrics>
    : ISpatialReader<TUserData>, ISpatialWriter<TUserData>, ISpatialLifecycle, IDisposable, IAsyncDisposable
    where TStrategy : struct, ISpatialPartitionStrategy
    where TMetrics : struct, ISpatialMetricsSink
{
    // Lifecycle states. The tree starts open, moves to completing when
    // <see cref="Complete"/> is called while operations are in flight, and
    // settles on complete (clean close) or faulted (the stream ended with an
    // error). Disposed is terminal for the object itself.
    private const int LifecycleOpen = 0;
    private const int LifecycleCompleting = 1;
    private const int LifecycleComplete = 2;
    private const int LifecycleFaulted = 3;
    private const int LifecycleDisposed = 4;

    /// <summary>
    /// Two node slots handled together, used where a structural change groups a
    /// fresh parent around an existing pair of nodes.
    /// </summary>
    private readonly record struct NodePair(NodeIndex A, NodeIndex B);

    private CachePaddedState _state;
    private readonly Lock _gate;
    private ExceptionDispatchInfo? _faultException;
    private Node[] _nodes;
    private TUserData?[] _userData;
    private int[] _itemToNode;
    // NodeIndex defaults to the valid slot zero rather than the null sentinel,
    // so both links start explicitly null; the ctor's free-list init then
    // publishes the real heads.
    private NodeIndex _rootIndex = NodeIndex.Null;
    private NodeIndex _freeListHead = NodeIndex.Null;
    private int _itemFreeListHead = -1;
    private int _count;

    /// <summary>
    /// Creates a tree with the given tuning. The default reserves 256 node slots
    /// and 128 item slots, grows both geometrically, and stores boxes with a
    /// tenth of a unit of slack per side, doubled for moving items.
    /// </summary>
    /// <param name="options">Tuning knobs; defaults when omitted.</param>
    public DynamicAabbTree(TreeOptions options = default)
    {
        // A default(TreeOptions) is a zeroed struct rather than the default
        // tuning: field initializers only run for new TreeOptions(), so an
        // omitted argument is recognized by its sub-minimum capacity (the
        // TreeCapacity guard makes any constructed value at least the minimum)
        // and replaced by the default tuning.
        TreeOptions tuning = options.InitialCapacity >= TreeCapacity.MinimumCapacity ? options : new TreeOptions();
        int capacity = tuning.InitialCapacity;
        FatteningMargin = tuning.FatteningMargin;
        VelocityMultiplier = tuning.VelocityMultiplier;
        _gate = new Lock();

        _nodes = GC.AllocateArray<Node>(capacity, pinned: true);
        _userData = new TUserData?[capacity / 2];
        _itemToNode = GC.AllocateArray<int>(capacity / 2, pinned: true);
        InitializeFreeNodes(0, capacity);
        InitializeFreeItemSlots(0, capacity / 2);
    }

    /// <summary>Number of live items in the tree.</summary>
    public int Count
    {
        get
        {
            _gate.Enter();
            try
            {
                return Volatile.Read(ref _count);
            }
            finally
            {
                _gate.Exit();
            }
        }
    }

    /// <summary>Number of items the current node layout can hold before it grows.</summary>
    public int Capacity
    {
        get
        {
            _gate.Enter();
            try
            {
                return _nodes.Length;
            }
            finally
            {
                _gate.Exit();
            }
        }
    }

    /// <summary>Per-side slack grown onto every stored box.</summary>
    public float FatteningMargin { get; }

    /// <summary>Scale applied to the margin for moving items.</summary>
    public float VelocityMultiplier { get; }

    /// <summary>Slot of the root node; <see cref="NodeIndex.Null"/> while the tree is empty.</summary>
    internal NodeIndex RootIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _rootIndex;
    }

    /// <summary>Returns a reference to the node stored at <paramref name="index"/>.</summary>
    /// <param name="index">The slot to address.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref Node GetNodeRef(NodeIndex index) => ref _nodes[(uint)index.Value];

    /// <summary>
    /// Adds an item and returns its identity. The world bounds are grown by the
    /// fattening margin before they are stored, so the item can creep a little
    /// without forcing a structural update.
    /// </summary>
    /// <param name="box">World bounds of the item; they are grown by the fattening margin.</param>
    /// <param name="userData">Payload to carry alongside the identity.</param>
    /// <returns>The identity of the inserted item.</returns>
    public SpatialItemId Insert(in Aabb3D box, TUserData userData)
    {
        EnterOperation();
        try
        {
            EnsureNodeCapacity();
            EnsureItemSlotCapacity();

            NodeIndex nodeIndex = AllocateNode();
            int itemSlot = AllocateItemSlot();

            Aabb3D fatBox = FatteningMargin > 0f ? box.Expanded(FatteningMargin) : box;

            ref Node node = ref GetNodeRef(nodeIndex);
            node.Box = fatBox;
            node.ItemId = new SpatialItemId((uint)itemSlot + 1U);
            node.Height = 0;
            node.ParentIndex = NodeIndex.Null;
            node.LeftChild = NodeIndex.Null;
            node.RightChild = NodeIndex.Null;

            _userData[itemSlot] = userData;
            _itemToNode[itemSlot] = nodeIndex;

            InsertLeaf(nodeIndex);

            Volatile.Write(ref _count, Volatile.Read(ref _count) + 1);
            TMetrics.OnItemInserted();

            return node.ItemId;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Replaces the bounds of an item, budgeting for its travel so the margin
    /// absorbs the motion. The stored box covers where the item is heading, so
    /// a steady mover is not re-inserted every frame.
    /// </summary>
    /// <param name="itemId">The item to move.</param>
    /// <param name="box">New world bounds.</param>
    /// <param name="displacement">Motion since the last update; it feeds the fattening budget.</param>
    /// <returns><see langword="true"/> when the item was re-inserted; <see langword="false"/> when it stayed in place.</returns>
    public bool Move(SpatialItemId itemId, in Aabb3D box, in Vec3 displacement)
    {
        EnterOperation();
        try
        {
            ValidateItem(itemId);
            int itemSlot = (int)itemId.Value - 1;
            NodeIndex nodeIndex = (NodeIndex)_itemToNode[itemSlot];
            ref Node node = ref GetNodeRef(nodeIndex);

            // Budget the new bounds by the motion: a fast body buys slack
            // proportional to its speed, grown on the side it is moving toward.
            Vec3 velocity = displacement * VelocityMultiplier;
            Aabb3D fatBox = FatteningMargin > 0f ? box.Expanded(FatteningMargin) : box;
            float minX = fatBox.Min.X;
            float minY = fatBox.Min.Y;
            float minZ = fatBox.Min.Z;
            float maxX = fatBox.Max.X;
            float maxY = fatBox.Max.Y;
            float maxZ = fatBox.Max.Z;
            if (velocity.X < 0f)
            {
                minX += velocity.X;
            }
            else
            {
                maxX += velocity.X;
            }

            if (velocity.Y < 0f)
            {
                minY += velocity.Y;
            }
            else
            {
                maxY += velocity.Y;
            }

            if (velocity.Z < 0f)
            {
                minZ += velocity.Z;
            }
            else
            {
                maxZ += velocity.Z;
            }

            fatBox = new Aabb3D(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ));

            // The stored box still covers the motion-budgeted bounds: the margin
            // absorbed the travel, so the item stays where it is.
            if (node.Box.Contains(in fatBox))
            {
                return false;
            }

            RemoveLeaf(nodeIndex);
            node.Box = fatBox;
            InsertLeaf(nodeIndex);
            return true;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Replaces the bounds of an item without a motion budget. When the stored
    /// box still covers the new bounds the call is a no-op; otherwise the item
    /// is re-inserted under its refreshed box.
    /// </summary>
    /// <param name="itemId">The item to update.</param>
    /// <param name="box">New world bounds.</param>
    public void Update(SpatialItemId itemId, in Aabb3D box)
    {
        EnterOperation();
        try
        {
            ValidateItem(itemId);
            int itemSlot = (int)itemId.Value - 1;
            NodeIndex nodeIndex = (NodeIndex)_itemToNode[itemSlot];
            ref Node node = ref GetNodeRef(nodeIndex);

            Aabb3D fatBox = FatteningMargin > 0f ? box.Expanded(FatteningMargin) : box;

            // The stored box still covers the new bounds: nothing to do.
            if (node.Box.Contains(in fatBox))
            {
                return;
            }

            RemoveLeaf(nodeIndex);
            node.Box = fatBox;
            InsertLeaf(nodeIndex);
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Drops an item and frees its slot for reuse. The identity stops being
    /// valid: reads of it throw, and the slot is returned to the free list.
    /// </summary>
    /// <param name="itemId">The item to remove.</param>
    public void Remove(SpatialItemId itemId)
    {
        EnterOperation();
        try
        {
            ValidateItem(itemId);
            int itemSlot = (int)itemId.Value - 1;
            NodeIndex nodeIndex = (NodeIndex)_itemToNode[itemSlot];

            RemoveLeaf(nodeIndex);
            FreeNode(nodeIndex);
            FreeItemSlot(itemSlot);
            _userData[itemSlot] = default;

            Volatile.Write(ref _count, Volatile.Read(ref _count) - 1);
            TMetrics.OnItemRemoved();
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>Returns the payload an item was inserted with.</summary>
    /// <param name="itemId">The item to read.</param>
    public TUserData GetUserData(SpatialItemId itemId)
    {
        ValidateItem(itemId);
        return _userData[(int)itemId.Value - 1]!;
    }

    /// <summary>Returns the padded bounds stored for an item, already grown by the fattening margin.</summary>
    /// <param name="itemId">The item to read.</param>
    public Aabb3D GetFatAabb(SpatialItemId itemId)
    {
        ValidateItem(itemId);
        return GetNodeRef((NodeIndex)_itemToNode[(int)itemId.Value - 1]).Box;
    }

    /// <summary>
    /// Closes the tree to further work, optionally recording the failure that
    /// ended it. When operations are still in flight the tree moves to
    /// completing and the last one out settles it on complete; a fault settles
    /// it at once. Calling it from inside an operation on the same thread
    /// deadlocks the gate, so the caller closes the stream between operations.
    /// </summary>
    /// <param name="error">The failure that ended the stream, or <see langword="null"/> when it ended cleanly.</param>
    public void Complete(Exception? error = null)
    {
        _gate.Enter();
        try
        {
            int state = Volatile.Read(ref _state._lifecycleState);
            if (state == LifecycleComplete || state == LifecycleFaulted || state == LifecycleDisposed)
            {
                return;
            }

            if (error != null)
            {
                _faultException = ExceptionDispatchInfo.Capture(error);
                Volatile.Write(ref _state._lifecycleState, LifecycleFaulted);
            }
            else if (Volatile.Read(ref _state._activeOperationCount) == 0)
            {
                Volatile.Write(ref _state._lifecycleState, LifecycleComplete);
            }
            else
            {
                Volatile.Write(ref _state._lifecycleState, LifecycleCompleting);
            }
        }
        finally
        {
            _gate.Exit();
        }
    }

    /// <summary>
    /// Awaits every queued update. The wait spins while the tree is completing
    /// and yields between checks, so it never blocks a worker thread; it
    /// returns at once when the stream was never closed or already settled.
    /// </summary>
    /// <param name="cancellationToken">Token that abandons the wait; the queued work still runs.</param>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        while (Volatile.Read(ref _state._lifecycleState) == LifecycleCompleting)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    /// <summary>
    /// Closes the tree, waits for the queued work and releases the gate. After
    /// the call the object is disposed: further mutations throw
    /// <see cref="ObjectDisposedException"/>.
    /// </summary>
    public void Dispose()
    {
        Complete();
        DrainAsync().AsTask().GetAwaiter().GetResult();

        _gate.Enter();
        try
        {
            Volatile.Write(ref _state._lifecycleState, LifecycleDisposed);
        }
        finally
        {
            _gate.Exit();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Closes the tree, awaits the queued work and releases the gate. After
    /// the call the object is disposed: further mutations throw
    /// <see cref="ObjectDisposedException"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Complete();
        await DrainAsync();

        _gate.Enter();
        try
        {
            Volatile.Write(ref _state._lifecycleState, LifecycleDisposed);
        }
        finally
        {
            _gate.Exit();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Registers an in-flight operation. The state is read once outside the gate
    /// for the steady state and re-checked under it, so a tree that is closing
    /// or closed rejects the operation without taking the lock in the common
    /// case.
    /// </summary>
    internal void EnterOperation()
    {
        int state = Volatile.Read(ref _state._lifecycleState);
        if (state != LifecycleOpen)
        {
            ThrowForState(state);
        }

        _gate.Enter();
        int currentState = Volatile.Read(ref _state._lifecycleState);
        if (currentState != LifecycleOpen)
        {
            _gate.Exit();
            ThrowForState(currentState);
        }

        Volatile.Write(ref _state._activeOperationCount, Volatile.Read(ref _state._activeOperationCount) + 1);
    }

    /// <summary>
    /// Releases an in-flight operation. The last one out of a completing tree
    /// settles it on complete, which is what <see cref="DrainAsync"/> waits for.
    /// </summary>
    internal void ExitOperation()
    {
        _gate.Enter();
        try
        {
            int remaining = Volatile.Read(ref _state._activeOperationCount) - 1;
            Volatile.Write(ref _state._activeOperationCount, remaining);
            if (remaining == 0 && Volatile.Read(ref _state._lifecycleState) == LifecycleCompleting)
            {
                Volatile.Write(ref _state._lifecycleState, LifecycleComplete);
            }
        }
        finally
        {
            _gate.Exit();
        }
    }

    /// <summary>Maps a non-open lifecycle state onto the exception it implies.</summary>
    /// <param name="state">The observed lifecycle state.</param>
    private void ThrowForState(int state)
    {
        switch (state)
        {
            case LifecycleFaulted:
                _faultException?.Throw();
                throw new InvalidOperationException("The tree has faulted.");
            case LifecycleDisposed:
                throw new ObjectDisposedException(nameof(DynamicAabbTree<TUserData, TStrategy, TMetrics>));
            default:
                throw new InvalidOperationException("The tree is closed.");
        }
    }

    /// <summary>
    /// Rejects an identity that names no live item: the invalid sentinel, a slot
    /// past the end of the table, or a slot on the free list. Identities are
    /// stored as slot plus one, so slot zero is never a valid identity.
    /// </summary>
    /// <param name="itemId">The identity to validate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateItem(SpatialItemId itemId)
    {
        if (itemId.Value == 0)
        {
            throw new InvalidOperationException("The item identity is invalid.");
        }

        int itemSlot = (int)itemId.Value - 1;
        if (itemSlot >= _itemToNode.Length || _itemToNode[itemSlot] < 0)
        {
            ThrowHelper.ThrowItemNotFound(itemId);
        }
    }

    /// <summary>
    /// Links a leaf into the tree: it descends from the root by the strategy's
    /// least-growth cost to find the best sibling, groups the pair under a
    /// fresh parent, and walks back up refitting bounds and rebalancing.
    /// </summary>
    /// <param name="leafIndex">The leaf slot to insert.</param>
    private void InsertLeaf(NodeIndex leafIndex)
    {
        if (_rootIndex.IsNull)
        {
            _rootIndex = leafIndex;
            GetNodeRef(leafIndex).ParentIndex = NodeIndex.Null;
            return;
        }

        // Find the best sibling for the leaf: descend by least-growth cost.
        NodeIndex siblingIndex = _rootIndex;
        while (!GetNodeRef(siblingIndex).IsLeaf())
        {
            ref Node current = ref GetNodeRef(siblingIndex);
            NodeIndex left = current.LeftChild;
            NodeIndex right = current.RightChild;
            int child = TStrategy.ChooseSubtree(in GetNodeRef(left).Box, in GetNodeRef(right).Box, in GetNodeRef(leafIndex).Box);
            siblingIndex = child == 0 ? left : right;
        }

        // Group the leaf and its sibling under a fresh parent. The allocation
        // may grow the pool, so no reference taken before it is still valid.
        NodeIndex oldParent = GetNodeRef(siblingIndex).ParentIndex;
        NodeIndex newParentIndex = AllocateNode();
        ref Node leaf = ref GetNodeRef(leafIndex);
        ref Node sibling = ref GetNodeRef(siblingIndex);
        ref Node newParent = ref GetNodeRef(newParentIndex);

        NodePair children = new(siblingIndex, leafIndex);
        newParent.ParentIndex = oldParent;
        newParent.Height = sibling.Height + 1;
        newParent.Box = Aabb3D.CreateMerged(in leaf.Box, in sibling.Box);
        newParent.LeftChild = children.A;
        newParent.RightChild = children.B;
        leaf.ParentIndex = newParentIndex;
        sibling.ParentIndex = newParentIndex;

        if (oldParent.IsNull)
        {
            _rootIndex = newParentIndex;
        }
        else
        {
            ref Node oldParentNode = ref GetNodeRef(oldParent);
            if (oldParentNode.LeftChild == siblingIndex)
            {
                oldParentNode.LeftChild = newParentIndex;
            }
            else
            {
                oldParentNode.RightChild = newParentIndex;
            }
        }

        // Walk back up the tree, refitting bounds and restoring balance.
        WalkBackRefit(newParentIndex);
    }

    /// <summary>
    /// Unlinks a leaf from the tree: the parent is spliced out and its sibling
    /// takes its place, or the sibling is promoted when the parent was the root.
    /// The leaf node itself stays allocated so a re-insertion can reuse the slot.
    /// </summary>
    /// <param name="leafIndex">The leaf slot to remove.</param>
    private void RemoveLeaf(NodeIndex leafIndex)
    {
        if (leafIndex == _rootIndex)
        {
            // The leaf is the root: the tree becomes empty.
            _rootIndex = NodeIndex.Null;
            return;
        }

        ref Node leaf = ref GetNodeRef(leafIndex);
        NodeIndex parentIndex = leaf.ParentIndex;
        ref Node parent = ref GetNodeRef(parentIndex);
        NodeIndex grandparentIndex = parent.ParentIndex;
        NodeIndex siblingIndex = parent.LeftChild == leafIndex ? parent.RightChild : parent.LeftChild;

        if (grandparentIndex.IsNull)
        {
            // The parent is the root: promote the sibling.
            GetNodeRef(siblingIndex).ParentIndex = NodeIndex.Null;
            _rootIndex = siblingIndex;
            FreeNode(parentIndex);
            return;
        }

        // Splice the sibling into the parent's place.
        ref Node grandparent = ref GetNodeRef(grandparentIndex);
        if (grandparent.LeftChild == parentIndex)
        {
            grandparent.LeftChild = siblingIndex;
        }
        else
        {
            grandparent.RightChild = siblingIndex;
        }

        GetNodeRef(siblingIndex).ParentIndex = grandparentIndex;
        FreeNode(parentIndex);
        WalkBackRefit(grandparentIndex);
    }

    /// <summary>
    /// Walks from <paramref name="index"/> to the root, balancing each node on
    /// the way and refitting its bound and height from its children.
    /// </summary>
    /// <param name="index">The node to start from.</param>
    private void WalkBackRefit(NodeIndex index)
    {
        while (!index.IsNull)
        {
            index = Balance(index);
            ref Node node = ref GetNodeRef(index);
            ref Node left = ref GetNodeRef(node.LeftChild);
            ref Node right = ref GetNodeRef(node.RightChild);
            node.Box = Aabb3D.CreateMerged(in left.Box, in right.Box);
            node.Height = 1 + Math.Max(left.Height, right.Height);
            index = node.ParentIndex;
        }
    }

    /// <summary>
    /// Restores the height invariant at one node with a single rotation when the
    /// children differ by more than one, returning the slot that now tops the
    /// local subtree. A leaf or a node whose children are balanced is returned
    /// unchanged.
    /// </summary>
    /// <param name="index">The node to balance.</param>
    /// <returns>The slot now topping the local subtree.</returns>
    private NodeIndex Balance(NodeIndex index)
    {
        ref Node node = ref GetNodeRef(index);
        if (node.IsLeaf() || node.Height < 2)
        {
            return index;
        }

        NodeIndex leftIndex = node.LeftChild;
        NodeIndex rightIndex = node.RightChild;
        ref Node left = ref GetNodeRef(leftIndex);
        ref Node right = ref GetNodeRef(rightIndex);

        int balance = right.Height - left.Height;

        // Rotate the right child up.
        if (balance > 1)
        {
            NodeIndex f = right.LeftChild;
            NodeIndex g = right.RightChild;
            ref Node fNode = ref GetNodeRef(f);
            ref Node gNode = ref GetNodeRef(g);

            // Swap node and right: right takes node's parent link.
            right.ParentIndex = node.ParentIndex;
            right.LeftChild = index;
            node.ParentIndex = rightIndex;

            // Node's old parent now points at right, or right is the root.
            NodeIndex rightParent = right.ParentIndex;
            if (!rightParent.IsNull)
            {
                ref Node rightParentNode = ref GetNodeRef(rightParent);
                if (rightParentNode.LeftChild == index)
                {
                    rightParentNode.LeftChild = rightIndex;
                }
                else
                {
                    rightParentNode.RightChild = rightIndex;
                }
            }
            else
            {
                _rootIndex = rightIndex;
            }

            // Rotate: the taller of right's grandchildren moves under node.
            if (fNode.Height > gNode.Height)
            {
                right.RightChild = f;
                node.RightChild = g;
                gNode.ParentIndex = index;
                node.Box = Aabb3D.CreateMerged(in left.Box, in gNode.Box);
                right.Box = Aabb3D.CreateMerged(in node.Box, in fNode.Box);
                node.Height = 1 + Math.Max(left.Height, gNode.Height);
                right.Height = 1 + Math.Max(node.Height, fNode.Height);
            }
            else
            {
                right.RightChild = g;
                node.RightChild = f;
                fNode.ParentIndex = index;
                node.Box = Aabb3D.CreateMerged(in left.Box, in fNode.Box);
                right.Box = Aabb3D.CreateMerged(in node.Box, in gNode.Box);
                node.Height = 1 + Math.Max(left.Height, fNode.Height);
                right.Height = 1 + Math.Max(node.Height, gNode.Height);
            }

            return rightIndex;
        }

        // Rotate the left child up.
        if (balance < -1)
        {
            NodeIndex f = left.LeftChild;
            NodeIndex g = left.RightChild;
            ref Node fNode = ref GetNodeRef(f);
            ref Node gNode = ref GetNodeRef(g);

            // Swap node and left: left takes node's parent link.
            left.ParentIndex = node.ParentIndex;
            left.RightChild = index;
            node.ParentIndex = leftIndex;

            // Node's old parent now points at left, or left is the root.
            NodeIndex leftParent = left.ParentIndex;
            if (!leftParent.IsNull)
            {
                ref Node leftParentNode = ref GetNodeRef(leftParent);
                if (leftParentNode.LeftChild == index)
                {
                    leftParentNode.LeftChild = leftIndex;
                }
                else
                {
                    leftParentNode.RightChild = leftIndex;
                }
            }
            else
            {
                _rootIndex = leftIndex;
            }

            // Rotate: the taller of left's grandchildren moves under node.
            if (fNode.Height > gNode.Height)
            {
                left.LeftChild = f;
                node.LeftChild = g;
                gNode.ParentIndex = index;
                node.Box = Aabb3D.CreateMerged(in right.Box, in gNode.Box);
                left.Box = Aabb3D.CreateMerged(in node.Box, in fNode.Box);
                node.Height = 1 + Math.Max(right.Height, gNode.Height);
                left.Height = 1 + Math.Max(node.Height, fNode.Height);
            }
            else
            {
                left.LeftChild = g;
                node.LeftChild = f;
                fNode.ParentIndex = index;
                node.Box = Aabb3D.CreateMerged(in right.Box, in fNode.Box);
                left.Box = Aabb3D.CreateMerged(in node.Box, in gNode.Box);
                node.Height = 1 + Math.Max(right.Height, fNode.Height);
                left.Height = 1 + Math.Max(node.Height, gNode.Height);
            }

            return leftIndex;
        }

        return index;
    }

    /// <summary>
    /// Grows the node pool when the free list is empty, copying the live nodes
    /// into the new pinned array and linking the fresh tail onto the free list.
    /// </summary>
    private void EnsureNodeCapacity()
    {
        if (!_freeListHead.IsNull)
        {
            return;
        }

        int oldCapacity = _nodes.Length;
        int newCapacity = oldCapacity * 2;
        Node[] newNodes = GC.AllocateArray<Node>(newCapacity, pinned: true);
        Array.Copy(_nodes, newNodes, oldCapacity);
        _nodes = newNodes;
        InitializeFreeNodes(oldCapacity, newCapacity);
    }

    /// <summary>
    /// Grows the item tables when the free list is empty, copying the live
    /// payloads and links into the new arrays and linking the fresh tail onto
    /// the free list.
    /// </summary>
    private void EnsureItemSlotCapacity()
    {
        if (_itemFreeListHead >= 0)
        {
            return;
        }

        int oldCapacity = _itemToNode.Length;
        int newCapacity = oldCapacity * 2;
        TUserData?[] newUserData = GC.AllocateArray<TUserData?>(newCapacity, pinned: true);
        int[] newItemToNode = GC.AllocateArray<int>(newCapacity, pinned: true);
        Array.Copy(_userData, newUserData, oldCapacity);
        Array.Copy(_itemToNode, newItemToNode, oldCapacity);
        _userData = newUserData;
        _itemToNode = newItemToNode;
        InitializeFreeItemSlots(oldCapacity, newCapacity);
    }

    /// <summary>
    /// Links the node slots in <paramref name="start"/>..<paramref name="end"/>
    /// into a free chain and makes <paramref name="start"/> the list head.
    /// </summary>
    /// <param name="start">The first slot of the range.</param>
    /// <param name="end">One past the last slot of the range.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeFreeNodes(int start, int end)
    {
        for (int i = start; i < end; ++i)
        {
            _nodes[i].NextFree = i + 1 < end ? new NodeIndex(i + 1) : NodeIndex.Null;
        }

        _freeListHead = start < end ? new NodeIndex(start) : NodeIndex.Null;
    }

    /// <summary>
    /// Links the item slots in <paramref name="start"/>..<paramref name="end"/>
    /// into a free chain and makes <paramref name="start"/> the list head. A free
    /// slot stores the complement of the next slot plus one, so every free entry
    /// stays negative and never collides with the node index of a live slot.
    /// </summary>
    /// <param name="start">The first slot of the range.</param>
    /// <param name="end">One past the last slot of the range.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeFreeItemSlots(int start, int end)
    {
        for (int i = start; i < end; ++i)
        {
            _itemToNode[i] = i + 1 < end ? ~(i + 2) : -1;
        }

        _itemFreeListHead = start < end ? start : -1;
    }

    /// <summary>Takes a node slot off the free list, growing the pool when it is empty.</summary>
    /// <returns>The allocated slot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NodeIndex AllocateNode()
    {
        if (_freeListHead.IsNull)
        {
            EnsureNodeCapacity();
        }

        NodeIndex index = _freeListHead;
        _freeListHead = GetNodeRef(index).NextFree;
        return index;
    }

    /// <summary>Returns a node slot to the free list.</summary>
    /// <param name="index">The slot to free.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FreeNode(NodeIndex index)
    {
        ref Node node = ref GetNodeRef(index);
        node.NextFree = _freeListHead;
        _freeListHead = index;
    }

    /// <summary>Takes an item slot off the free list, growing the tables when it is empty.</summary>
    /// <returns>The allocated slot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocateItemSlot()
    {
        if (_itemFreeListHead < 0)
        {
            EnsureItemSlotCapacity();
        }

        int slot = _itemFreeListHead;
        _itemFreeListHead = ~_itemToNode[slot] - 1;
        return slot;
    }

    /// <summary>Returns an item slot to the free list.</summary>
    /// <param name="slot">The slot to free.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FreeItemSlot(int slot)
    {
        _itemToNode[slot] = ~(_itemFreeListHead + 1);
        _itemFreeListHead = slot;
    }
}

/// <summary>
/// One node of the tree: a fattened bound, the item identity it carries, the
/// structural links, the subtree height, and the free-list link used while
/// the slot is not allocated. The size is pinned to 64 bytes so a node
/// occupies a dense, cache-friendly slot in the pool. The type is top level
/// rather than nested because the CLR forbids explicit layout on the nested
/// types of a generic class.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 64)]
internal struct Node
{
    /// <summary>Fattened bounds stored for the item.</summary>
    public Aabb3D Box;

    /// <summary>Identity of the item this leaf carries; default on internal nodes.</summary>
    public SpatialItemId ItemId;

    /// <summary>Parent slot; <see cref="NodeIndex.Null"/> on the root.</summary>
    public NodeIndex ParentIndex;

    /// <summary>Left child slot; <see cref="NodeIndex.Null"/> on leaves.</summary>
    public NodeIndex LeftChild;

    /// <summary>Right child slot; <see cref="NodeIndex.Null"/> on leaves.</summary>
    public NodeIndex RightChild;

    /// <summary>Height of the subtree rooted at this node; zero on leaves.</summary>
    public int Height;

    /// <summary>Next free slot while this node is on the free list.</summary>
    public NodeIndex NextFree;

    private readonly long _pad;

    /// <summary>Whether this node is a leaf, which is exactly when it has no left child.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsLeaf() => LeftChild.IsNull;
}

/// <summary>
/// Lifecycle state and the in-flight operation count, laid out on two
/// cache lines so a reader spinning on the state never contends with the
/// counter the writer bumps under the gate. The type is top level rather than
/// nested because the CLR forbids explicit layout on the nested types of a
/// generic class.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct CachePaddedState
{
    [FieldOffset(0)]
    internal int _lifecycleState;

    [FieldOffset(64)]
    internal int _activeOperationCount;
}
