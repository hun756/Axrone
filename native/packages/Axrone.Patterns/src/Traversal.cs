namespace Axrone.Patterns;

/// <summary>
/// Allocation-free pre-order walk over a sealed native arena.
/// </summary>
/// <typeparam name="TNode">The node type stored in the arena.</typeparam>
/// <typeparam name="THierarchy">Maps a node to its child handles.</typeparam>
public readonly struct PreOrderEnumerable<TNode, THierarchy>
    where TNode : unmanaged
    where THierarchy : INodeHierarchy<TNode>
{
    private readonly NativeNodeArena<TNode, SealedPhase> _arena;
    private readonly NodeHandle _root;

    /// <summary>Creates a walk rooted at <paramref name="root"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PreOrderEnumerable(NativeNodeArena<TNode, SealedPhase> arena, NodeHandle root)
    {
        _arena = arena;
        _root = root;
    }

    /// <summary>Returns the stack-backed enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DepthFirstPreOrderEnumerator<TNode, THierarchy> GetEnumerator() => new(_arena, _root);
}

/// <summary>Stack-backed pre-order enumerator; dispose to release nothing (no-op by design).</summary>
public ref struct DepthFirstPreOrderEnumerator<TNode, THierarchy>
    where TNode : unmanaged
    where THierarchy : INodeHierarchy<TNode>
{
    private readonly NativeNodeArena<TNode, SealedPhase> _arena;
    private InlineBuffer256<NodeHandle> _stack;
    private int _top;
    private NodeHandle _current;

    /// <summary>Creates an enumerator; prefer the enumerable.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal DepthFirstPreOrderEnumerator(NativeNodeArena<TNode, SealedPhase> arena, NodeHandle root)
    {
        _arena = arena;
        _stack = default;
        _top = 0;
        _current = NodeHandle.Invalid;
        if (root.IsValid)
        {
            _stack[_top++] = root;
        }
    }

    /// <summary>Advances to the next node in pre-order, if any.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext()
    {
        if (_top == 0) return false;

        _current = _stack[--_top];
        ref readonly TNode node = ref _arena.Get(_current);
        ReadOnlySpan<NodeHandle> children = THierarchy.GetChildren(in node);
        nuint count = (nuint)children.Length;
        if (count > 0)
        {
            if ((uint)(_top + children.Length) > 256)
            {
                ThrowHelper.ThrowStackOverflowException();
            }

            ref NodeHandle childRef = ref MemoryMarshal.GetReference(children);
            for (nuint i = count; i > 0; i--)
            {
                _stack[_top++] = Unsafe.Add(ref childRef, i - 1);
            }
        }

        return true;
    }

    /// <summary>The node at the current position.</summary>
    public ref readonly TNode Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _arena.Get(_current);
    }

    /// <summary>The handle at the current position.</summary>
    public NodeHandle CurrentHandle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }
}

/// <summary>
/// Iterative traversals over a native arena: depth-first pre/post-order and
/// breadth-first, each in a policy (static) and a visitor (stateful) flavor.
/// Stacks start inline (256 slots) and spill to a rented array past that.
/// </summary>
/// <typeparam name="TNode">The node type stored in the arena.</typeparam>
/// <typeparam name="THierarchy">Maps a node to its child handles.</typeparam>
public static class IterativeTraversal<TNode, THierarchy>
    where TNode : unmanaged
    where THierarchy : INodeHierarchy<TNode>
{
    /// <summary>Pre-order walk driving a static policy through a filter.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void TraverseDepthFirstPreOrder<TPolicy, TFilter, TContext, TState>(
        NativeNodeArena<TNode, TState> arena,
        NodeHandle root,
        ref TContext context)
        where TPolicy : struct, IVisitorPolicy<TNode, TContext, Unit>
        where TFilter : struct, ITraversalFilter<TNode, TContext>
        where TContext : allows ref struct
        where TState : struct, IArenaPhase
    {
        ArgumentNullException.ThrowIfNull(arena);
        if (!root.IsValid) ThrowHelper.ThrowArgumentOutOfRange(nameof(root));

        InlineBuffer256<NodeHandle> inlineStack = default;
        Span<NodeHandle> stack = inlineStack;
        NodeHandle[]? rented = null;
        int top = 0;
        stack[top++] = root;

        try
        {
            while (top > 0)
            {
                NodeHandle current = stack[--top];
                ref readonly TNode node = ref arena.Get(current);

                if (!TFilter.ShouldTraverse(in node, ref context))
                {
                    continue;
                }

                TPolicy.Visit(in node, ref context);

                if (!TFilter.ShouldDescend(in node, ref context))
                {
                    continue;
                }

                ReadOnlySpan<NodeHandle> children = THierarchy.GetChildren(in node);
                nuint childCount = (nuint)children.Length;
                if (childCount == 0) continue;

                if (top + children.Length > stack.Length)
                {
                    GrowStack(ref stack, ref rented, top + children.Length);
                }

                ref NodeHandle childRef = ref MemoryMarshal.GetReference(children);
                for (nuint i = childCount; i > 0; i--)
                {
                    stack[top++] = Unsafe.Add(ref childRef, i - 1);
                }
            }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<NodeHandle>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Pre-order walk driving a stateful visitor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void TraverseDepthFirstPreOrder<TVisitor, TContext, TState>(
        NativeNodeArena<TNode, TState> arena,
        NodeHandle root,
        ref TVisitor visitor,
        ref TContext context)
        where TVisitor : IVisitor<TNode, TContext, Unit>, allows ref struct
        where TContext : allows ref struct
        where TState : struct, IArenaPhase
    {
        ArgumentNullException.ThrowIfNull(arena);
        if (!root.IsValid) ThrowHelper.ThrowArgumentOutOfRange(nameof(root));

        InlineBuffer256<NodeHandle> inlineStack = default;
        Span<NodeHandle> stack = inlineStack;
        NodeHandle[]? rented = null;
        int top = 0;
        stack[top++] = root;

        try
        {
            while (top > 0)
            {
                NodeHandle current = stack[--top];
                ref readonly TNode node = ref arena.Get(current);

                visitor.Visit(in node, ref context);

                ReadOnlySpan<NodeHandle> children = THierarchy.GetChildren(in node);
                nuint childCount = (nuint)children.Length;
                if (childCount == 0) continue;

                if (top + children.Length > stack.Length)
                {
                    GrowStack(ref stack, ref rented, top + children.Length);
                }

                ref NodeHandle childRef = ref MemoryMarshal.GetReference(children);
                for (nuint i = childCount; i > 0; i--)
                {
                    stack[top++] = Unsafe.Add(ref childRef, i - 1);
                }
            }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<NodeHandle>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Post-order walk driving a stateful visitor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void TraverseDepthFirstPostOrder<TVisitor, TContext, TState>(
        NativeNodeArena<TNode, TState> arena,
        NodeHandle root,
        ref TVisitor visitor,
        ref TContext context)
        where TVisitor : IVisitor<TNode, TContext, Unit>, allows ref struct
        where TContext : allows ref struct
        where TState : struct, IArenaPhase
    {
        ArgumentNullException.ThrowIfNull(arena);
        if (!root.IsValid) ThrowHelper.ThrowArgumentOutOfRange(nameof(root));

        InlineBuffer256<NodeHandle> primaryStackStorage = default;
        InlineBuffer256<NodeHandle> outputStackStorage = default;

        Span<NodeHandle> primaryStack = primaryStackStorage;
        Span<NodeHandle> outputStack = outputStackStorage;

        NodeHandle[]? primaryRented = null;
        NodeHandle[]? outputRented = null;

        int primaryTop = 0;
        int outputTop = 0;

        primaryStack[primaryTop++] = root;

        try
        {
            while (primaryTop > 0)
            {
                NodeHandle current = primaryStack[--primaryTop];

                if (outputTop >= outputStack.Length)
                {
                    GrowStack(ref outputStack, ref outputRented, outputTop + 1);
                }
                outputStack[outputTop++] = current;

                ref readonly TNode node = ref arena.Get(current);
                ReadOnlySpan<NodeHandle> children = THierarchy.GetChildren(in node);
                nuint childCount = (nuint)children.Length;
                if (childCount == 0) continue;

                if (primaryTop + children.Length > primaryStack.Length)
                {
                    GrowStack(ref primaryStack, ref primaryRented, primaryTop + children.Length);
                }

                ref NodeHandle childRef = ref MemoryMarshal.GetReference(children);
                for (nuint i = 0; i < childCount; i++)
                {
                    primaryStack[primaryTop++] = Unsafe.Add(ref childRef, i);
                }
            }

            while (outputTop > 0)
            {
                NodeHandle current = outputStack[--outputTop];
                ref readonly TNode node = ref arena.Get(current);
                visitor.Visit(in node, ref context);
            }
        }
        finally
        {
            if (primaryRented != null) ArrayPool<NodeHandle>.Shared.Return(primaryRented);
            if (outputRented != null) ArrayPool<NodeHandle>.Shared.Return(outputRented);
        }
    }

    /// <summary>Breadth-first walk driving a stateful visitor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void TraverseBreadthFirst<TVisitor, TContext, TState>(
        NativeNodeArena<TNode, TState> arena,
        NodeHandle root,
        ref TVisitor visitor,
        ref TContext context)
        where TVisitor : IVisitor<TNode, TContext, Unit>, allows ref struct
        where TContext : allows ref struct
        where TState : struct, IArenaPhase
    {
        ArgumentNullException.ThrowIfNull(arena);
        if (!root.IsValid) ThrowHelper.ThrowArgumentOutOfRange(nameof(root));

        InlineBuffer256<NodeHandle> inlineQueue = default;
        Span<NodeHandle> queue = inlineQueue;
        NodeHandle[]? rented = null;
        int head = 0;
        int tail = 0;

        queue[tail++] = root;

        try
        {
            while (head < tail)
            {
                NodeHandle current = queue[head++];
                ref readonly TNode node = ref arena.Get(current);

                visitor.Visit(in node, ref context);

                ReadOnlySpan<NodeHandle> children = THierarchy.GetChildren(in node);
                nuint childCount = (nuint)children.Length;
                if (childCount == 0) continue;

                if (tail + children.Length > queue.Length)
                {
                    int remaining = tail - head;
                    if (remaining > 0 && head > 0)
                    {
                        queue.Slice(head, remaining).CopyTo(queue);
                        head = 0;
                        tail = remaining;
                    }

                    if (tail + children.Length > queue.Length)
                    {
                        GrowQueue(ref queue, ref rented, ref head, ref tail, tail + children.Length);
                    }
                }

                ref NodeHandle childRef = ref MemoryMarshal.GetReference(children);
                for (nuint i = 0; i < childCount; i++)
                {
                    queue[tail++] = Unsafe.Add(ref childRef, i);
                }
            }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<NodeHandle>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowStack(ref Span<NodeHandle> stack, ref NodeHandle[]? rented, int requiredCapacity)
    {
        int newCap = Math.Max(stack.Length * 2, requiredCapacity);
        NodeHandle[] newRented = ArrayPool<NodeHandle>.Shared.Rent(newCap);
        stack.CopyTo(newRented);
        if (rented != null)
        {
            ArrayPool<NodeHandle>.Shared.Return(rented);
        }
        rented = newRented;
        stack = newRented;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowQueue(
        ref Span<NodeHandle> queue,
        ref NodeHandle[]? rented,
        ref int head,
        ref int tail,
        int requiredCapacity)
    {
        int count = tail - head;
        int newCap = Math.Max(queue.Length * 2, requiredCapacity);
        NodeHandle[] newRented = ArrayPool<NodeHandle>.Shared.Rent(newCap);
        if (count > 0)
        {
            queue.Slice(head, count).CopyTo(newRented);
        }
        if (rented != null)
        {
            ArrayPool<NodeHandle>.Shared.Return(rented);
        }
        rented = newRented;
        queue = newRented;
        head = 0;
        tail = count;
    }
}
