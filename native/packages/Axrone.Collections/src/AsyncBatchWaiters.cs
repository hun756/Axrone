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

    public AsyncBatchWaiter<T>? Next { get; set; }

    internal ReadOnlyMemory<T> MemoryIn;
    internal Memory<T> MemoryOut;
    internal bool IsEnqueue;

    public short Version => _core.Version;

    public AsyncBatchWaiter()
    {
        _core.RunContinuationsAsynchronously = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnRented(SpinLockStackPool<AsyncBatchWaiter<T>> pool, bool isEnqueue)
    {
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
                self._core.SetException(new OperationCanceledException(token));
            }, this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Complete(int result)
    {
        _registration.Dispose();
        _core.SetResult(result);
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

    public AsyncItemWaiter<T>? Next { get; set; }

    internal T Item = default!;
    internal bool IsEnqueue;

    public short Version => _core.Version;

    public AsyncItemWaiter()
    {
        _core.RunContinuationsAsynchronously = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnRented(SpinLockStackPool<AsyncItemWaiter<T>> pool, bool isEnqueue)
    {
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
                self._core.SetException(new OperationCanceledException(token));
            }, this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Complete(T result)
    {
        _registration.Dispose();
        _core.SetResult(result);
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
    public AsyncBatchWaiter<T> RentBatchWaiter(bool isEnqueue) => _batchPool.Rent(isEnqueue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AsyncItemWaiter<T> RentItemWaiter(bool isEnqueue) => _itemPool.Rent(isEnqueue);

    public void RegisterBatchEnqueue(AsyncBatchWaiter<T> node)
    {
        using (_syncLock.EnterScope())
        {
            if (_batchEnqTail == null)
            {
                _batchEnqHead = _batchEnqTail = node;
            }
            else
            {
                _batchEnqTail.Next = node;
                _batchEnqTail = node;
            }
        }
    }

    public void RegisterBatchDequeue(AsyncBatchWaiter<T> node)
    {
        using (_syncLock.EnterScope())
        {
            if (_batchDeqTail == null)
            {
                _batchDeqHead = _batchDeqTail = node;
            }
            else
            {
                _batchDeqTail.Next = node;
                _batchDeqTail = node;
            }
        }
    }

    public void RegisterItemEnqueue(AsyncItemWaiter<T> node)
    {
        using (_syncLock.EnterScope())
        {
            if (_itemEnqTail == null)
            {
                _itemEnqHead = _itemEnqTail = node;
            }
            else
            {
                _itemEnqTail.Next = node;
                _itemEnqTail = node;
            }
        }
    }

    public void RegisterItemDequeue(AsyncItemWaiter<T> node)
    {
        using (_syncLock.EnterScope())
        {
            if (_itemDeqTail == null)
            {
                _itemDeqHead = _itemDeqTail = node;
            }
            else
            {
                _itemDeqTail.Next = node;
                _itemDeqTail = node;
            }
        }
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
}
