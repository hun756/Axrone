namespace Axrone.ObjectPool;

/// <summary>
/// Abstracts the item-storage mechanism used by <see cref="ObjectPool{T}"/>.
/// Each implementation encapsulates enqueue, dequeue, clear, and query operations
/// for a specific <see cref="PoolingStrategy"/>, eliminating per-method strategy dispatch
/// inside the pool's hot paths.
/// </summary>
/// <typeparam name="T">The reference type of the pooled object.</typeparam>
internal interface IPoolStorage<T> : IDisposable where T : class
{
    /// <summary>Enqueues an item into the storage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Enqueue(in PooledItem<T> item);

    /// <summary>Attempts to dequeue an item. Returns <c>false</c> when no items are available.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryDequeue(out PooledItem<T> item);

    /// <summary>Enqueues an item asynchronously. Falls back to synchronous enqueue for non-blocking strategies.</summary>
    ValueTask EnqueueAsync(PooledItem<T> item, CancellationToken cancellationToken);

    /// <summary>Gets the approximate number of items currently in storage.</summary>
    int Count { get; }

    /// <summary>Drains all items from storage, invoking <paramref name="action"/> for each.</summary>
    void Clear(Action<PooledItem<T>> action);

    /// <summary>Asynchronously drains all items, invoking <paramref name="action"/> for each.</summary>
    ValueTask ClearAsync(Func<PooledItem<T>, ValueTask> action, CancellationToken cancellationToken);

    /// <summary>Returns <c>true</c> if an item wrapping <paramref name="instance"/> is found in storage.</summary>
    bool Contains(T instance);
}

// ─── CentralizedQueue ─────────────────────────────────────────────

/// <summary>
/// Single shared <see cref="ConcurrentQueue{T}"/> for all threads.
/// Corresponds to <see cref="PoolingStrategy.CentralizedQueue"/> and <see cref="PoolingStrategy.LockFree"/>.
/// </summary>
internal sealed class CentralizedQueuePoolStorage<T> : IPoolStorage<T> where T : class
{
    private readonly ConcurrentQueue<PooledItem<T>> _queue = new();

    public int Count => _queue.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in PooledItem<T> item) => _queue.Enqueue(item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out PooledItem<T> item) => _queue.TryDequeue(out item);

    public ValueTask EnqueueAsync(PooledItem<T> item, CancellationToken cancellationToken)
    {
        _queue.Enqueue(item);
        return ValueTask.CompletedTask;
    }

    public void Clear(Action<PooledItem<T>> action)
    {
        while (_queue.TryDequeue(out var item))
            action(item);
    }

    public async ValueTask ClearAsync(Func<PooledItem<T>, ValueTask> action, CancellationToken cancellationToken)
    {
        while (_queue.TryDequeue(out var item))
            await action(item).ConfigureAwait(false);
    }

    public bool Contains(T instance)
    {
        foreach (var pooled in _queue)
        {
            if (ReferenceEquals(pooled.Instance, instance))
                return true;
        }
        return false;
    }

    public void Dispose() { }
}

// ─── ThreadLocal ──────────────────────────────────────────────────

/// <summary>
/// Thread-local queues with a shared central fallback queue.
/// Items are enqueued to the calling thread's local queue; dequeue tries local first,
/// then falls back to the central queue. Corresponds to <see cref="PoolingStrategy.ThreadLocal"/>.
/// </summary>
internal sealed class ThreadLocalPoolStorage<T> : IPoolStorage<T> where T : class
{
    private readonly ThreadLocal<ConcurrentQueue<PooledItem<T>>> _tls;
    private readonly ConcurrentQueue<PooledItem<T>> _central = new();

    public ThreadLocalPoolStorage()
    {
        _tls = new ThreadLocal<ConcurrentQueue<PooledItem<T>>>(
            () => new ConcurrentQueue<PooledItem<T>>(), trackAllValues: true);
    }

    public int Count
    {
        get
        {
            int count = _central.Count;
            foreach (var q in _tls.Values)
                count += q.Count;
            return count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in PooledItem<T> item)
    {
        _tls.Value!.Enqueue(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out PooledItem<T> item)
    {
        var local = _tls.Value!;
        if (local.TryDequeue(out item))
            return true;
        return _central.TryDequeue(out item);
    }

    public ValueTask EnqueueAsync(PooledItem<T> item, CancellationToken cancellationToken)
    {
        Enqueue(item);
        return ValueTask.CompletedTask;
    }

    public void Clear(Action<PooledItem<T>> action)
    {
        foreach (var q in _tls.Values)
            while (q.TryDequeue(out var item))
                action(item);
        while (_central.TryDequeue(out var item))
            action(item);
    }

    public async ValueTask ClearAsync(Func<PooledItem<T>, ValueTask> action, CancellationToken cancellationToken)
    {
        foreach (var q in _tls.Values)
            while (q.TryDequeue(out var item))
                await action(item).ConfigureAwait(false);
        while (_central.TryDequeue(out var item))
            await action(item).ConfigureAwait(false);
    }

    public bool Contains(T instance)
    {
        foreach (var q in _tls.Values)
            foreach (var pooled in q)
                if (ReferenceEquals(pooled.Instance, instance))
                    return true;
        foreach (var pooled in _central)
            if (ReferenceEquals(pooled.Instance, instance))
                return true;
        return false;
    }

    public void Dispose() => _tls.Dispose();
}

// ─── Sharded ──────────────────────────────────────────────────────

/// <summary>
/// A single shard bucket backed by a <see cref="ConcurrentQueue{T}"/> with an atomic count.
/// Used by <see cref="ShardedPoolStorage{T}"/>.
/// </summary>
internal sealed class ShardedBucket<T> where T : class
{
    private readonly ConcurrentQueue<PooledItem<T>> _items;
    private int _localCount;

    public ShardedBucket()
    {
        _items = new ConcurrentQueue<PooledItem<T>>();
        _localCount = 0;
    }

    public int Count => Volatile.Read(ref _localCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in PooledItem<T> item)
    {
        _items.Enqueue(item);
        Interlocked.Increment(ref _localCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out PooledItem<T> item)
    {
        if (_items.TryDequeue(out item))
        {
            Interlocked.Decrement(ref _localCount);
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsInstance(T instance)
    {
        foreach (var pooledItem in _items)
        {
            if (ReferenceEquals(pooledItem.Instance, instance))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Fixed-count sharded buckets indexed by <c>ThreadId % shardCount</c>.
/// Corresponds to <see cref="PoolingStrategy.ThreadLocalWithPartitioning"/>
/// and <see cref="PoolingStrategy.ShardedByThread"/>.
/// </summary>
internal sealed class ShardedPoolStorage<T> : IPoolStorage<T> where T : class
{
    private readonly ShardedBucket<T>[] _shards;

    public ShardedPoolStorage(int shardCount)
    {
        _shards = new ShardedBucket<T>[shardCount];
        for (int i = 0; i < shardCount; i++)
            _shards[i] = new ShardedBucket<T>();
    }

    public int Count
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _shards.Length; i++)
                count += _shards[i].Count;
            return count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in PooledItem<T> item)
    {
        int idx = Math.Abs(Environment.CurrentManagedThreadId % _shards.Length);
        _shards[idx].Enqueue(in item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out PooledItem<T> item)
    {
        int idx = Math.Abs(Environment.CurrentManagedThreadId % _shards.Length);
        if (_shards[idx].TryDequeue(out item))
            return true;
        for (int i = 0; i < _shards.Length; i++)
        {
            if (i == idx) continue;
            if (_shards[i].TryDequeue(out item))
                return true;
        }
        return false;
    }

    public ValueTask EnqueueAsync(PooledItem<T> item, CancellationToken cancellationToken)
    {
        Enqueue(item);
        return ValueTask.CompletedTask;
    }

    public void Clear(Action<PooledItem<T>> action)
    {
        for (int i = 0; i < _shards.Length; i++)
            while (_shards[i].TryDequeue(out var item))
                action(item);
    }

    public async ValueTask ClearAsync(Func<PooledItem<T>, ValueTask> action, CancellationToken cancellationToken)
    {
        for (int i = 0; i < _shards.Length; i++)
            while (_shards[i].TryDequeue(out var item))
                await action(item).ConfigureAwait(false);
    }

    public bool Contains(T instance)
    {
        for (int i = 0; i < _shards.Length; i++)
            if (_shards[i].ContainsInstance(instance))
                return true;
        return false;
    }

    public void Dispose() { }
}

// ─── BoundedChannel ───────────────────────────────────────────────

/// <summary>
/// <see cref="Channel{T}"/>-backed storage with bounded capacity and backpressure.
/// Corresponds to <see cref="PoolingStrategy.BoundedChannel"/>.
/// </summary>
internal sealed class BoundedChannelPoolStorage<T> : IPoolStorage<T> where T : class
{
    private readonly Channel<PooledItem<T>> _channel;
    private int _count;

    public BoundedChannelPoolStorage(int capacity)
    {
        var options = new BoundedChannelOptions(capacity > 0 ? capacity : Environment.ProcessorCount * 32)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = true,
        };
        _channel = Channel.CreateBounded<PooledItem<T>>(options);
    }

    public int Count => Volatile.Read(ref _count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in PooledItem<T> item)
    {
        if (_channel.Writer.TryWrite(item))
            Interlocked.Increment(ref _count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out PooledItem<T> item)
    {
        if (_channel.Reader.TryRead(out item))
        {
            Interlocked.Decrement(ref _count);
            return true;
        }
        item = default;
        return false;
    }

    public async ValueTask EnqueueAsync(PooledItem<T> item, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _count);
    }

    public void Clear(Action<PooledItem<T>> action)
    {
        while (_channel.Reader.TryRead(out var item))
        {
            Interlocked.Decrement(ref _count);
            action(item);
        }
    }

    public async ValueTask ClearAsync(Func<PooledItem<T>, ValueTask> action, CancellationToken cancellationToken)
    {
        while (_channel.Reader.TryRead(out var item))
        {
            Interlocked.Decrement(ref _count);
            await action(item).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public bool Contains(T instance)
    {
        var items = new List<PooledItem<T>>();
        while (_channel.Reader.TryRead(out var pooled))
        {
            Interlocked.Decrement(ref _count);
            items.Add(pooled);
        }

        bool found = false;
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i].Instance, instance))
                found = true;

            SpinWait spin = new SpinWait();
            while (!_channel.Writer.TryWrite(items[i]))
            {
                spin.SpinOnce();
            }

            Interlocked.Increment(ref _count);
        }
        return found;
    }

    public void Dispose() { }
}

// ─── LockFree ─────────────────────────────────────────────────────

/// <summary>
/// Lock-free storage backed by a <see cref="ConcurrentQueue{T}"/>.
/// Capacity enforcement is done atomically at the pool level using <see cref="Interlocked"/>
/// rather than at the storage level. Corresponds to <see cref="PoolingStrategy.LockFree"/>.
/// </summary>
internal sealed class LockFreePoolStorage<T> : IPoolStorage<T> where T : class
{
    private readonly ConcurrentQueue<PooledItem<T>> _queue = new();

    public int Count => _queue.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in PooledItem<T> item) => _queue.Enqueue(item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out PooledItem<T> item) => _queue.TryDequeue(out item);

    public ValueTask EnqueueAsync(PooledItem<T> item, CancellationToken cancellationToken)
    {
        _queue.Enqueue(item);
        return ValueTask.CompletedTask;
    }

    public void Clear(Action<PooledItem<T>> action)
    {
        while (_queue.TryDequeue(out var item))
            action(item);
    }

    public async ValueTask ClearAsync(Func<PooledItem<T>, ValueTask> action, CancellationToken cancellationToken)
    {
        while (_queue.TryDequeue(out var item))
            await action(item).ConfigureAwait(false);
    }

    public bool Contains(T instance)
    {
        foreach (var pooled in _queue)
            if (ReferenceEquals(pooled.Instance, instance))
                return true;
        return false;
    }

    public void Dispose() { }
}
