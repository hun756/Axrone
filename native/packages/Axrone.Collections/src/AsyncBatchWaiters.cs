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
    private LockFreeStackPool<AsyncBatchWaiter<T>>? _pool;

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
    public void OnRented(LockFreeStackPool<AsyncBatchWaiter<T>> pool, bool isEnqueue)
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
    private LockFreeStackPool<AsyncItemWaiter<T>>? _pool;

    public AsyncItemWaiter<T>? Next { get; set; }

    internal T Item = default!;
    internal bool IsEnqueue;

    public short Version => _core.Version;

    public AsyncItemWaiter()
    {
        _core.RunContinuationsAsynchronously = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnRented(LockFreeStackPool<AsyncItemWaiter<T>> pool, bool isEnqueue)
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
    private readonly LockFreeStackPool<AsyncBatchWaiter<T>> _batchPool = new();
    private readonly LockFreeStackPool<AsyncItemWaiter<T>> _itemPool = new();

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

        // 1. Drain under lock (sub-nanosecond pointer swaps)
        AsyncBatchWaiter<T>? batchWaiters;
        AsyncItemWaiter<T>? itemWaiters;
        using (_syncLock.EnterScope())
        {
            batchWaiters = _batchEnqHead;
            _batchEnqHead = _batchEnqTail = null;

            itemWaiters = _itemEnqHead;
            _itemEnqHead = _itemEnqTail = null;
        }

        // 2. Fulfill OUTSIDE the lock (no contention amplification)
        AsyncBatchWaiter<T>? unfulfilledBatchHead = null, unfulfilledBatchTail = null;
        while (batchWaiters != null)
        {
            AsyncBatchWaiter<T> current = batchWaiters;
            batchWaiters = batchWaiters.Next;
            current.Next = null;

            int enqueued = queue.TryEnqueueBatch(current.MemoryIn.Span);
            if (enqueued > 0)
            {
                current.Complete(enqueued);
            }
            else
            {
                if (unfulfilledBatchTail == null)
                {
                    unfulfilledBatchHead = unfulfilledBatchTail = current;
                }
                else
                {
                    unfulfilledBatchTail.Next = current;
                    unfulfilledBatchTail = current;
                }
            }
        }

        AsyncItemWaiter<T>? unfulfilledItemHead = null, unfulfilledItemTail = null;
        while (itemWaiters != null)
        {
            AsyncItemWaiter<T> current = itemWaiters;
            itemWaiters = itemWaiters.Next;
            current.Next = null;

            if (queue.TryEnqueue(current.Item))
            {
                current.Complete(default!);
            }
            else
            {
                if (unfulfilledItemTail == null)
                {
                    unfulfilledItemHead = unfulfilledItemTail = current;
                }
                else
                {
                    unfulfilledItemTail.Next = current;
                    unfulfilledItemTail = current;
                }
            }
        }

        // 3. Re-queue unfulfilled nodes preserving order
        if (unfulfilledBatchHead != null || unfulfilledItemHead != null)
        {
            using (_syncLock.EnterScope())
            {
                if (unfulfilledBatchHead != null)
                {
                    unfulfilledBatchTail!.Next = _batchEnqHead;
                    _batchEnqHead = unfulfilledBatchHead;
                    _batchEnqTail ??= unfulfilledBatchTail;
                }

                if (unfulfilledItemHead != null)
                {
                    unfulfilledItemTail!.Next = _itemEnqHead;
                    _itemEnqHead = unfulfilledItemHead;
                    _itemEnqTail ??= unfulfilledItemTail;
                }
            }
        }
    }

    public void SignalDequeueWaiters<TQueue>(TQueue queue) where TQueue : IBatchDequeue<T>
    {
        if (Volatile.Read(ref _batchDeqHead) == null && Volatile.Read(ref _itemDeqHead) == null)
        {
            return;
        }

        // 1. Drain under lock
        AsyncBatchWaiter<T>? batchWaiters;
        AsyncItemWaiter<T>? itemWaiters;
        using (_syncLock.EnterScope())
        {
            batchWaiters = _batchDeqHead;
            _batchDeqHead = _batchDeqTail = null;

            itemWaiters = _itemDeqHead;
            _itemDeqHead = _itemDeqTail = null;
        }

        // 2. Fulfill OUTSIDE the lock
        AsyncBatchWaiter<T>? unfulfilledBatchHead = null, unfulfilledBatchTail = null;
        while (batchWaiters != null)
        {
            AsyncBatchWaiter<T> current = batchWaiters;
            batchWaiters = batchWaiters.Next;
            current.Next = null;

            int dequeued = queue.TryDequeueBatch(current.MemoryOut.Span);
            if (dequeued > 0)
            {
                current.Complete(dequeued);
            }
            else
            {
                if (unfulfilledBatchTail == null)
                {
                    unfulfilledBatchHead = unfulfilledBatchTail = current;
                }
                else
                {
                    unfulfilledBatchTail.Next = current;
                    unfulfilledBatchTail = current;
                }
            }
        }

        AsyncItemWaiter<T>? unfulfilledItemHead = null, unfulfilledItemTail = null;
        while (itemWaiters != null)
        {
            AsyncItemWaiter<T> current = itemWaiters;
            itemWaiters = itemWaiters.Next;
            current.Next = null;

            if (queue.TryDequeue(out T? item))
            {
                current.Complete(item);
            }
            else
            {
                if (unfulfilledItemTail == null)
                {
                    unfulfilledItemHead = unfulfilledItemTail = current;
                }
                else
                {
                    unfulfilledItemTail.Next = current;
                    unfulfilledItemTail = current;
                }
            }
        }

        // 3. Re-queue unfulfilled nodes
        if (unfulfilledBatchHead != null || unfulfilledItemHead != null)
        {
            using (_syncLock.EnterScope())
            {
                if (unfulfilledBatchHead != null)
                {
                    unfulfilledBatchTail!.Next = _batchDeqHead;
                    _batchDeqHead = unfulfilledBatchHead;
                    _batchDeqTail ??= unfulfilledBatchTail;
                }

                if (unfulfilledItemHead != null)
                {
                    unfulfilledItemTail!.Next = _itemDeqHead;
                    _itemDeqHead = unfulfilledItemHead;
                    _itemDeqTail ??= unfulfilledItemTail;
                }
            }
        }
    }
}
