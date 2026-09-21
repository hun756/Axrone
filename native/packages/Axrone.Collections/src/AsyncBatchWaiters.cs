namespace Axrone.Collections;

using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

/// <summary>
/// Specialized pooled state machine for asynchronous batch operations.
/// Implements IValueTaskSource&lt;int&gt; for zero-allocation async batch enqueue/dequeue.
/// </summary>
internal sealed class AsyncBatchWaiter<T> : IValueTaskSource<int>, IPooledWaiterNode<AsyncBatchWaiter<T>>
{
    private ManualResetValueTaskSourceCore<int> _core;
    private CancellationTokenRegistration _registration;
    private SpinLockStackPool<AsyncBatchWaiter<T>>? _pool;
    private AsyncBatchQueueCoordinator<T>? _coordinator;

    public AsyncBatchWaiter<T>? Next { get; set; }

    internal ReadOnlyMemory<T> MemoryIn;
    internal Memory<T> MemoryOut;
    internal bool IsEnqueue;

    public short Version => _core.Version;

    internal AsyncBatchQueueCoordinator<T>? Coordinator
    {
        get => _coordinator;
        set => _coordinator = value;
    }

    public AsyncBatchWaiter()
    {
        _core.RunContinuationsAsynchronously = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnRented(SpinLockStackPool<AsyncBatchWaiter<T>> pool, bool isEnqueue)
    {
        _registration.Dispose();
        _registration = default;
        _core.Reset();
        _pool = pool;
        IsEnqueue = isEnqueue;
        Next = null;
        MemoryIn = default;
        MemoryOut = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void HookCancellation(CancellationToken ct)
    {
        if (ct.CanBeCanceled)
        {
            _registration = ct.UnsafeRegister(static (state, token) =>
            {
                var self = (AsyncBatchWaiter<T>)state!;
                self._coordinator?.TryCancelBatchWaiter(self, token);
            }, this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Complete(int result)
    {
        _core.SetResult(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fail(Exception exception)
    {
        _core.SetException(exception);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetResult(short token)
    {
        try
        {
            return _core.GetResult(token);
        }
        finally
        {
            _registration.Dispose();
            _registration = default;
            _pool?.Return(this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}

/// <summary>
/// Specialized pooled state machine for asynchronous single-item operations.
/// Implements both IValueTaskSource&lt;T&gt; (dequeue) and IValueTaskSource (enqueue).
/// </summary>
internal sealed class AsyncItemWaiter<T> : IValueTaskSource<T>, IValueTaskSource, IPooledWaiterNode<AsyncItemWaiter<T>>
{
    private ManualResetValueTaskSourceCore<T> _core;
    private CancellationTokenRegistration _registration;
    private SpinLockStackPool<AsyncItemWaiter<T>>? _pool;
    private AsyncBatchQueueCoordinator<T>? _coordinator;

    public AsyncItemWaiter<T>? Next { get; set; }

    internal T Item = default!;
    internal bool IsEnqueue;

    public short Version => _core.Version;

    internal AsyncBatchQueueCoordinator<T>? Coordinator
    {
        get => _coordinator;
        set => _coordinator = value;
    }

    public AsyncItemWaiter()
    {
        _core.RunContinuationsAsynchronously = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnRented(SpinLockStackPool<AsyncItemWaiter<T>> pool, bool isEnqueue)
    {
        _registration.Dispose();
        _registration = default;
        _core.Reset();
        _pool = pool;
        IsEnqueue = isEnqueue;
        Next = null;
        Item = default!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void HookCancellation(CancellationToken ct)
    {
        if (ct.CanBeCanceled)
        {
            _registration = ct.UnsafeRegister(static (state, token) =>
            {
                var self = (AsyncItemWaiter<T>)state!;
                self._coordinator?.TryCancelItemWaiter(self, token);
            }, this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Complete(T result)
    {
        _core.SetResult(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fail(Exception exception)
    {
        _core.SetException(exception);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetResult(short token)
    {
        try
        {
            return _core.GetResult(token);
        }
        finally
        {
            _registration.Dispose();
            _registration = default;
            _pool?.Return(this);
        }
    }

    void IValueTaskSource.GetResult(short token)
    {
        try
        {
            _core.GetResult(token);
        }
        finally
        {
            _registration.Dispose();
            _registration = default;
            _pool?.Return(this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}

/// <summary>
/// Decoupled, lock-re-entrancy-free asynchronous coordinator.
/// Never executes CAS loops or payload transfers while holding the synchronization primitive.
/// Drain-under-lock → fulfill-outside-lock → re-queue-unfulfilled pattern.
/// </summary>
internal sealed class AsyncBatchQueueCoordinator<T>
{
    private readonly Lock _syncLock = new();
    private readonly SpinLockStackPool<AsyncBatchWaiter<T>> _batchPool = new();
    private readonly SpinLockStackPool<AsyncItemWaiter<T>> _itemPool = new();

    private AsyncBatchWaiter<T>? _batchEnqHead, _batchEnqTail;
    private AsyncBatchWaiter<T>? _batchDeqHead, _batchDeqTail;
    private AsyncItemWaiter<T>? _itemEnqHead, _itemEnqTail;
    private AsyncItemWaiter<T>? _itemDeqHead, _itemDeqTail;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AsyncBatchWaiter<T> RentBatchWaiter(bool isEnqueue)
    {
        AsyncBatchWaiter<T> node = _batchPool.Rent(isEnqueue);
        node.Coordinator = this;
        return node;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AsyncItemWaiter<T> RentItemWaiter(bool isEnqueue)
    {
        AsyncItemWaiter<T> node = _itemPool.Rent(isEnqueue);
        node.Coordinator = this;
        return node;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleaseBatchWaiter(AsyncBatchWaiter<T> node) => _batchPool.Return(node);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleaseItemWaiter(AsyncItemWaiter<T> node) => _itemPool.Return(node);

    /// <summary>
    /// Atomically re-checks transport and links the waiter only when still empty. Closes the
    /// lost-wakeup window between the fast-path attempt and registration: a signal fired in that
    /// window either lands before the re-check (items taken here) or after the link (waiter
    /// detached by the signal), never in between.
    /// </summary>
    /// <returns>Items taken synchronously (waiter released); zero when the waiter was linked.</returns>
    public int RegisterOrTakeBatchEnqueue<TQueue>(TQueue queue, ReadOnlyMemory<T> items, AsyncBatchWaiter<T> node)
        where TQueue : IBatchEnqueue<T>
    {
        using (_syncLock.EnterScope())
        {
            int taken = queue.TryEnqueueBatch(items.Span);
            if (taken > 0)
            {
                ReleaseBatchWaiter(node);
                return taken;
            }

            LinkBatch(ref _batchEnqHead, ref _batchEnqTail, node);
            return 0;
        }
    }

    /// <summary>Dequeue variant of <see cref="RegisterOrTakeBatchEnqueue{TQueue}(TQueue, ReadOnlyMemory{T}, AsyncBatchWaiter{T})"/>.</summary>
    /// <returns>Items taken synchronously (waiter released); zero when the waiter was linked.</returns>
    public int RegisterOrTakeBatchDequeue<TQueue>(TQueue queue, Memory<T> destination, AsyncBatchWaiter<T> node)
        where TQueue : IBatchDequeue<T>
    {
        using (_syncLock.EnterScope())
        {
            int taken = queue.TryDequeueBatch(destination.Span);
            if (taken > 0)
            {
                ReleaseBatchWaiter(node);
                return taken;
            }

            LinkBatch(ref _batchDeqHead, ref _batchDeqTail, node);
            return 0;
        }
    }

    /// <summary>
    /// Item variant of <see cref="RegisterOrTakeBatchEnqueue{TQueue}(TQueue, ReadOnlyMemory{T}, AsyncBatchWaiter{T})"/>.
    /// </summary>
    /// <returns>True when the item was taken synchronously (waiter released).</returns>
    public bool RegisterOrTakeItemEnqueue<TQueue>(TQueue queue, AsyncItemWaiter<T> node)
        where TQueue : IBatchEnqueue<T>
    {
        using (_syncLock.EnterScope())
        {
            if (queue.TryEnqueue(node.Item))
            {
                ReleaseItemWaiter(node);
                return true;
            }

            LinkItem(ref _itemEnqHead, ref _itemEnqTail, node);
            return false;
        }
    }

    /// <summary>
    /// Item variant of <see cref="RegisterOrTakeBatchDequeue{TQueue}(TQueue, Memory{T}, AsyncBatchWaiter{T})"/>.
    /// </summary>
    /// <returns>True when an item was taken synchronously (waiter released).</returns>
    public bool RegisterOrTakeItemDequeue<TQueue>(TQueue queue, AsyncItemWaiter<T> node, out T? item)
        where TQueue : IBatchDequeue<T>
    {
        using (_syncLock.EnterScope())
        {
            if (queue.TryDequeue(out T? taken) && taken is not null)
            {
                item = taken;
                ReleaseItemWaiter(node);
                return true;
            }

            item = default!;
            LinkItem(ref _itemDeqHead, ref _itemDeqTail, node);
            return false;
        }
    }

    /// <summary>
    /// Cancels a waiter by unlinking it under the same lock the signal and dispose paths detach
    /// with: exactly one side wins, so the core completes exactly once. A waiter already
    /// detached is owned by the signal/dispose path and left alone.
    /// </summary>
    /// <returns>True when this call unlinked and completed the waiter.</returns>
    public bool TryCancelBatchWaiter(AsyncBatchWaiter<T> node, CancellationToken token)
    {
        using (_syncLock.EnterScope())
        {
            if (UnlinkBatch(ref _batchEnqHead, ref _batchEnqTail, node) ||
                UnlinkBatch(ref _batchDeqHead, ref _batchDeqTail, node))
            {
                node.Fail(new OperationCanceledException(token));
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc cref="TryCancelBatchWaiter(AsyncBatchWaiter{T}, CancellationToken)"/>
    public bool TryCancelItemWaiter(AsyncItemWaiter<T> node, CancellationToken token)
    {
        using (_syncLock.EnterScope())
        {
            if (UnlinkItem(ref _itemEnqHead, ref _itemEnqTail, node) ||
                UnlinkItem(ref _itemDeqHead, ref _itemDeqTail, node))
            {
                node.Fail(new OperationCanceledException(token));
                return true;
            }

            return false;
        }
    }

    private static void LinkBatch(ref AsyncBatchWaiter<T>? head, ref AsyncBatchWaiter<T>? tail, AsyncBatchWaiter<T> node)
    {
        node.Next = null;
        if (tail == null)
        {
            head = tail = node;
        }
        else
        {
            tail.Next = node;
            tail = node;
        }
    }

    private static void LinkItem(ref AsyncItemWaiter<T>? head, ref AsyncItemWaiter<T>? tail, AsyncItemWaiter<T> node)
    {
        node.Next = null;
        if (tail == null)
        {
            head = tail = node;
        }
        else
        {
            tail.Next = node;
            tail = node;
        }
    }

    private static bool UnlinkBatch(ref AsyncBatchWaiter<T>? head, ref AsyncBatchWaiter<T>? tail, AsyncBatchWaiter<T> node)
    {
        AsyncBatchWaiter<T>? current = head;
        AsyncBatchWaiter<T>? previous = null;
        while (current != null)
        {
            if (ReferenceEquals(current, node))
            {
                if (previous == null)
                {
                    head = node.Next;
                }
                else
                {
                    previous.Next = node.Next;
                }

                if (ReferenceEquals(tail, node))
                {
                    tail = previous;
                }

                node.Next = null;
                return true;
            }

            previous = current;
            current = current.Next;
        }

        return false;
    }

    private static bool UnlinkItem(ref AsyncItemWaiter<T>? head, ref AsyncItemWaiter<T>? tail, AsyncItemWaiter<T> node)
    {
        AsyncItemWaiter<T>? current = head;
        AsyncItemWaiter<T>? previous = null;
        while (current != null)
        {
            if (ReferenceEquals(current, node))
            {
                if (previous == null)
                {
                    head = node.Next;
                }
                else
                {
                    previous.Next = node.Next;
                }

                if (ReferenceEquals(tail, node))
                {
                    tail = previous;
                }

                node.Next = null;
                return true;
            }

            previous = current;
            current = current.Next;
        }

        return false;
    }

    public void SignalEnqueueWaiters<TQueue>(TQueue queue) where TQueue : IBatchEnqueue<T>
    {
        if (Volatile.Read(ref _batchEnqHead) == null && Volatile.Read(ref _itemEnqHead) == null)
        {
            return;
        }

        AsyncBatchWaiter<T>? batchWaiters;
        AsyncItemWaiter<T>? itemWaiters;
        using (_syncLock.EnterScope())
        {
            batchWaiters = _batchEnqHead;
            _batchEnqHead = _batchEnqTail = null;
            itemWaiters = _itemEnqHead;
            _itemEnqHead = _itemEnqTail = null;
        }

        AsyncBatchWaiter<T>? unfulfilledBatchHead = null, unfulfilledBatchTail = null;
        bool batchSaturated = false;
        while (batchWaiters != null)
        {
            AsyncBatchWaiter<T> current = batchWaiters;
            batchWaiters = batchWaiters.Next;
            current.Next = null;

            if (!batchSaturated)
            {
                int enqueued = queue.TryEnqueueBatch(current.MemoryIn.Span);
                if (enqueued > 0)
                {
                    current.Complete(enqueued);
                    continue;
                }
                batchSaturated = true;
            }
            AppendUnfulfilled(ref unfulfilledBatchHead, ref unfulfilledBatchTail, current);
        }

        AsyncItemWaiter<T>? unfulfilledItemHead = null, unfulfilledItemTail = null;
        bool itemSaturated = false;
        while (itemWaiters != null)
        {
            AsyncItemWaiter<T> current = itemWaiters;
            itemWaiters = itemWaiters.Next;
            current.Next = null;

            if (!itemSaturated)
            {
                if (queue.TryEnqueue(current.Item))
                {
                    current.Complete(default!);
                    continue;
                }
                itemSaturated = true;
            }
            AppendUnfulfilled(ref unfulfilledItemHead, ref unfulfilledItemTail, current);
        }

        RequeueUnfulfilled(
            ref _batchEnqHead, ref _batchEnqTail, unfulfilledBatchHead, unfulfilledBatchTail,
            ref _itemEnqHead, ref _itemEnqTail, unfulfilledItemHead, unfulfilledItemTail);
    }

    public void SignalDequeueWaiters<TQueue>(TQueue queue) where TQueue : IBatchDequeue<T>
    {
        if (Volatile.Read(ref _batchDeqHead) == null && Volatile.Read(ref _itemDeqHead) == null)
        {
            return;
        }

        AsyncBatchWaiter<T>? batchWaiters;
        AsyncItemWaiter<T>? itemWaiters;
        using (_syncLock.EnterScope())
        {
            batchWaiters = _batchDeqHead;
            _batchDeqHead = _batchDeqTail = null;
            itemWaiters = _itemDeqHead;
            _itemDeqHead = _itemDeqTail = null;
        }

        AsyncBatchWaiter<T>? unfulfilledBatchHead = null, unfulfilledBatchTail = null;
        bool batchSaturated = false;
        while (batchWaiters != null)
        {
            AsyncBatchWaiter<T> current = batchWaiters;
            batchWaiters = batchWaiters.Next;
            current.Next = null;

            if (!batchSaturated)
            {
                int dequeued = queue.TryDequeueBatch(current.MemoryOut.Span);
                if (dequeued > 0)
                {
                    current.Complete(dequeued);
                    continue;
                }
                batchSaturated = true;
            }
            AppendUnfulfilled(ref unfulfilledBatchHead, ref unfulfilledBatchTail, current);
        }

        AsyncItemWaiter<T>? unfulfilledItemHead = null, unfulfilledItemTail = null;
        bool itemSaturated = false;
        while (itemWaiters != null)
        {
            AsyncItemWaiter<T> current = itemWaiters;
            itemWaiters = itemWaiters.Next;
            current.Next = null;

            if (!itemSaturated)
            {
                if (queue.TryDequeue(out T? item))
                {
                    current.Complete(item);
                    continue;
                }
                itemSaturated = true;
            }
            AppendUnfulfilled(ref unfulfilledItemHead, ref unfulfilledItemTail, current);
        }

        RequeueUnfulfilled(
            ref _batchDeqHead, ref _batchDeqTail, unfulfilledBatchHead, unfulfilledBatchTail,
            ref _itemDeqHead, ref _itemDeqTail, unfulfilledItemHead, unfulfilledItemTail);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendUnfulfilled<TNode>(ref TNode? head, ref TNode? tail, TNode node)
        where TNode : class, IPooledWaiterNode<TNode>, new()
    {
        if (tail == null)
            head = tail = node;
        else
        {
            tail.Next = node;
            tail = node;
        }
    }

    private void RequeueUnfulfilled<TBatch, TItem>(
        ref TBatch? batchHead, ref TBatch? batchTail, TBatch? unfulfilledBatchHead, TBatch? unfulfilledBatchTail,
        ref TItem? itemHead, ref TItem? itemTail, TItem? unfulfilledItemHead, TItem? unfulfilledItemTail)
        where TBatch : class, IPooledWaiterNode<TBatch>, new()
        where TItem : class, IPooledWaiterNode<TItem>, new()
    {
        if (unfulfilledBatchHead == null && unfulfilledItemHead == null)
            return;

        using (_syncLock.EnterScope())
        {
            if (unfulfilledBatchHead != null)
            {
                unfulfilledBatchTail!.Next = batchHead;
                batchHead = unfulfilledBatchHead;
                batchTail ??= unfulfilledBatchTail;
            }

            if (unfulfilledItemHead != null)
            {
                unfulfilledItemTail!.Next = itemHead;
                itemHead = unfulfilledItemHead;
                itemTail ??= unfulfilledItemTail;
            }
        }
    }

    public void Dispose()
    {
        AsyncBatchWaiter<T>? batchEnq;
        AsyncBatchWaiter<T>? batchDeq;
        AsyncItemWaiter<T>? itemEnq;
        AsyncItemWaiter<T>? itemDeq;

        using (_syncLock.EnterScope())
        {
            batchEnq = _batchEnqHead;
            batchDeq = _batchDeqHead;
            itemEnq = _itemEnqHead;
            itemDeq = _itemDeqHead;
            _batchEnqHead = _batchEnqTail = null;
            _batchDeqHead = _batchDeqTail = null;
            _itemEnqHead = _itemEnqTail = null;
            _itemDeqHead = _itemDeqTail = null;
        }

        var disposed = new ObjectDisposedException(typeof(AsyncBatchQueueCoordinator<T>).Name);
        FailBatchChain(batchEnq, disposed);
        FailBatchChain(batchDeq, disposed);
        FailItemChain(itemEnq, disposed);
        FailItemChain(itemDeq, disposed);
    }

    private static void FailBatchChain(AsyncBatchWaiter<T>? head, Exception exception)
    {
        while (head != null)
        {
            var next = head.Next;
            head.Fail(exception);
            head = next;
        }
    }

    private static void FailItemChain(AsyncItemWaiter<T>? head, Exception exception)
    {
        while (head != null)
        {
            var next = head.Next;
            head.Fail(exception);
            head = next;
        }
    }
}
