namespace Axrone.Collections;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

/// <summary>
/// Ultra-high-performance, bounded, batch-oriented MPMC queue with static policy specialization.
/// Features: SIMD-accelerated sequence validation, cache-line isolated barriers,
/// dual-topology storage (off-heap for value types, managed for reference types),
/// pooled async waiters for zero-allocation async operations.
/// </summary>
/// <typeparam name="T">The type of items managed by the queue.</typeparam>
/// <typeparam name="TBackoff">Static backoff strategy configuring contention mechanics.</typeparam>
public sealed class VyukovBoundedBatchQueue<T, TBackoff> : IBatchQueue<T>
    where TBackoff : struct, IBackoffPolicy
{
    private readonly BufferCapacity _capacity;
    private readonly BoundedSlotStorage<T> _storage;
    private readonly AsyncBatchQueueCoordinator<T> _asyncCoordinator;
    private SequenceBarrierCoordinator _barrier;
    private int _isDisposed;

    public uint Capacity => _capacity.Value;
    public int Count => _barrier.ComputeCount(_capacity.Value);
    public bool IsEmpty => _barrier.ComputeCount(_capacity.Value) == 0;
    public bool IsFull => _barrier.ComputeCount(_capacity.Value) >= (int)_capacity.Value;

    public VyukovBoundedBatchQueue(BufferCapacity capacity)
    {
        _capacity = capacity;
        _storage = new BoundedSlotStorage<T>(capacity);
        _asyncCoordinator = new AsyncBatchQueueCoordinator<T>();
        _barrier = default;
    }

    // ========================================================================
    // Enqueue Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int TryEnqueueBatch(ReadOnlySpan<T> items)
    {
        _ = TryEnqueueBatchInternal(items, out int enqueued);
        return enqueued;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryEnqueue(in T item)
        => TryEnqueueInternal(in item) == QueueOperationStatus.Success;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public QueueOperationStatus TryEnqueueBatchWithStatus(ReadOnlySpan<T> items, out int enqueuedCount)
        => TryEnqueueBatchInternal(items, out enqueuedCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public QueueOperationStatus TryEnqueueWithStatus(in T item)
        => TryEnqueueInternal(in item);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private QueueOperationStatus TryEnqueueBatchInternal(ReadOnlySpan<T> items, out int enqueuedCount)
    {
        enqueuedCount = 0;
        if (items.IsEmpty) return QueueOperationStatus.Success;
        if (Volatile.Read(ref _isDisposed) != 0) return QueueOperationStatus.Disposed;

        uint requested = (uint)items.Length;
        if (requested > _capacity.Value) requested = _capacity.Value;

        TBackoff.Initialize(out int backoffState);
        nuint mask = _storage.Mask;
        unsafe
        {
            nuint* sequences = _storage.Sequences;

            while (true)
            {
                SequenceNumber enqPos = _barrier.EnqueuePos;
                SequenceNumber deqPos = _barrier.DequeuePos;

                nuint inFlight = enqPos.Value - deqPos.Value;
                if (inFlight >= _capacity.Value)
                {
                    return QueueOperationStatus.Full;
                }

                nuint available = _capacity.Value - inFlight;
                nuint maxPossible = Math.Min((nuint)requested, available);

                nuint ready = BatchSimdAccelerator.CountAvailableSequences(sequences, enqPos, mask, enqPos, maxPossible);
                if (ready == 0)
                {
                    nuint seq = Volatile.Read(ref sequences[enqPos.Value & mask]);
                    if ((nint)seq - (nint)enqPos.Value < 0)
                    {
                        return QueueOperationStatus.Full;
                    }

                    TBackoff.Step(ref backoffState);
                    continue;
                }

                if (_barrier.TryAdvanceEnqueue(enqPos, enqPos.Advance((uint)ready)))
                {
                    _storage.WriteBatch(enqPos, items.Slice(0, (int)ready));
                    _storage.PublishEnqueueSequences(enqPos, ready);

                    _asyncCoordinator.SignalDequeueWaiters(this);
                    enqueuedCount = (int)ready;
                    return QueueOperationStatus.Success;
                }

                TBackoff.Step(ref backoffState);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private QueueOperationStatus TryEnqueueInternal(in T item)
    {
        if (Volatile.Read(ref _isDisposed) != 0) return QueueOperationStatus.Disposed;

        TBackoff.Initialize(out int backoffState);
        nuint mask = _storage.Mask;
        unsafe
        {
            nuint* sequences = _storage.Sequences;

            while (true)
            {
                SequenceNumber enqPos = _barrier.EnqueuePos;
                nuint seq = Volatile.Read(ref sequences[enqPos.Value & mask]);
                nint diff = (nint)seq - (nint)enqPos.Value;

                if (diff == 0)
                {
                    if (_barrier.TryAdvanceEnqueue(enqPos, enqPos.Next()))
                    {
                        _storage.GetItemRef(enqPos) = item;
                        Volatile.Write(ref sequences[enqPos.Value & mask], enqPos.Value + 1);

                        _asyncCoordinator.SignalDequeueWaiters(this);
                        return QueueOperationStatus.Success;
                    }
                }
                else if (diff < 0)
                {
                    return QueueOperationStatus.Full;
                }
                else
                {
                    TBackoff.Step(ref backoffState);
                }
            }
        }
    }

    // ========================================================================
    // Dequeue Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int TryDequeueBatch(Span<T> destination)
    {
        _ = TryDequeueBatchInternal(destination, out int dequeued);
        return dequeued;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryDequeue([MaybeNullWhen(false)] out T item)
        => TryDequeueInternal(out item) == QueueOperationStatus.Success;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public QueueOperationStatus TryDequeueBatchWithStatus(Span<T> destination, out int dequeuedCount)
        => TryDequeueBatchInternal(destination, out dequeuedCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public QueueOperationStatus TryDequeueWithStatus([MaybeNullWhen(false)] out T item)
        => TryDequeueInternal(out item);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private QueueOperationStatus TryDequeueBatchInternal(Span<T> destination, out int dequeuedCount)
    {
        dequeuedCount = 0;
        if (destination.IsEmpty) return QueueOperationStatus.Success;
        if (Volatile.Read(ref _isDisposed) != 0) return QueueOperationStatus.Disposed;

        uint requested = (uint)destination.Length;
        if (requested > _capacity.Value) requested = _capacity.Value;

        TBackoff.Initialize(out int backoffState);
        nuint mask = _storage.Mask;
        unsafe
        {
            nuint* sequences = _storage.Sequences;

            while (true)
            {
                SequenceNumber deqPos = _barrier.DequeuePos;
                SequenceNumber enqPos = _barrier.EnqueuePos;

                if (deqPos.Value >= enqPos.Value)
                {
                    return QueueOperationStatus.Empty;
                }

                nuint inFlight = enqPos.Value - deqPos.Value;
                nuint maxPossible = Math.Min((nuint)requested, inFlight);

                nuint ready = BatchSimdAccelerator.CountAvailableSequences(sequences, deqPos, mask, deqPos.Next(), maxPossible);
                if (ready == 0)
                {
                    nuint seq = Volatile.Read(ref sequences[deqPos.Value & mask]);
                    if ((nint)seq - (nint)(deqPos.Value + 1) < 0)
                    {
                        return QueueOperationStatus.Empty;
                    }

                    TBackoff.Step(ref backoffState);
                    continue;
                }

                if (_barrier.TryAdvanceDequeue(deqPos, deqPos.Advance((uint)ready)))
                {
                    _storage.ReadBatch(deqPos, destination.Slice(0, (int)ready));
                    _storage.PublishDequeueSequences(deqPos, ready);

                    _asyncCoordinator.SignalEnqueueWaiters(this);
                    dequeuedCount = (int)ready;
                    return QueueOperationStatus.Success;
                }

                TBackoff.Step(ref backoffState);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private QueueOperationStatus TryDequeueInternal([MaybeNullWhen(false)] out T item)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            item = default;
            return QueueOperationStatus.Disposed;
        }

        TBackoff.Initialize(out int backoffState);
        nuint mask = _storage.Mask;
        unsafe
        {
            nuint* sequences = _storage.Sequences;

            while (true)
            {
                SequenceNumber deqPos = _barrier.DequeuePos;
                nuint seq = Volatile.Read(ref sequences[deqPos.Value & mask]);
                nint diff = (nint)seq - (nint)(deqPos.Value + 1);

                if (diff == 0)
                {
                    if (_barrier.TryAdvanceDequeue(deqPos, deqPos.Next()))
                    {
                        ref T source = ref _storage.GetItemRef(deqPos);
                        item = source;

                        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                        {
                            source = default!;
                        }

                        Volatile.Write(ref sequences[deqPos.Value & mask], deqPos.Value + _capacity.Value);

                        _asyncCoordinator.SignalEnqueueWaiters(this);
                        return QueueOperationStatus.Success;
                    }
                }
                else if (diff < 0)
                {
                    item = default;
                    return QueueOperationStatus.Empty;
                }
                else
                {
                    TBackoff.Step(ref backoffState);
                }
            }
        }
    }

    // ========================================================================
    // Zero-Allocation Visitor Patterns
    // ========================================================================

    /// <summary>
    /// Consumes elements directly into a stack-only visitor without boxing or array allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int DrainTo<TState, TConsumer>(Span<T> scratchBuffer, ref TState state, TConsumer consumer)
        where TState : allows ref struct
        where TConsumer : struct, ISlotConsumer<T, TState>
    {
        int dequeued = TryDequeueBatch(scratchBuffer);
        for (int i = 0; i < dequeued; i++)
        {
            consumer.Consume(in scratchBuffer[i], ref state);
        }
        return dequeued;
    }

    /// <summary>
    /// Consumes contiguous spans directly into a batch visitor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int DrainBatchesTo<TState, TVisitor>(Span<T> scratchBuffer, ref TState state, TVisitor visitor)
        where TState : allows ref struct
        where TVisitor : struct, ISpanVisitor<T, TState>
    {
        int dequeued = TryDequeueBatch(scratchBuffer);
        if (dequeued > 0)
        {
            visitor.Visit(scratchBuffer.Slice(0, dequeued), ref state);
        }
        return dequeued;
    }

    // ========================================================================
    // Async Operations
    // ========================================================================

    public ValueTask<int> EnqueueBatchAsync(ReadOnlyMemory<T> items, CancellationToken cancellationToken = default)
    {
        int enqueued = TryEnqueueBatch(items.Span);
        if (enqueued > 0)
        {
            return new ValueTask<int>(enqueued);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        AsyncBatchWaiter<T> waiter = _asyncCoordinator.RentBatchWaiter(isEnqueue: true);
        waiter.MemoryIn = items;
        waiter.HookCancellation(cancellationToken);
        _asyncCoordinator.RegisterBatchEnqueue(waiter);

        return new ValueTask<int>(waiter, waiter.Version);
    }

    public ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        if (TryEnqueue(in item))
        {
            return ValueTask.CompletedTask;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        AsyncItemWaiter<T> waiter = _asyncCoordinator.RentItemWaiter(isEnqueue: true);
        waiter.Item = item;
        waiter.HookCancellation(cancellationToken);
        _asyncCoordinator.RegisterItemEnqueue(waiter);

        return new ValueTask(waiter, waiter.Version);
    }

    public ValueTask<int> DequeueBatchAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        int dequeued = TryDequeueBatch(destination.Span);
        if (dequeued > 0)
        {
            return new ValueTask<int>(dequeued);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        AsyncBatchWaiter<T> waiter = _asyncCoordinator.RentBatchWaiter(isEnqueue: false);
        waiter.MemoryOut = destination;
        waiter.HookCancellation(cancellationToken);
        _asyncCoordinator.RegisterBatchDequeue(waiter);

        return new ValueTask<int>(waiter, waiter.Version);
    }

    public ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
    {
        if (TryDequeue(out T? item))
        {
            return new ValueTask<T>(item);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<T>(cancellationToken);
        }

        AsyncItemWaiter<T> waiter = _asyncCoordinator.RentItemWaiter(isEnqueue: false);
        waiter.HookCancellation(cancellationToken);
        _asyncCoordinator.RegisterItemDequeue(waiter);

        return new ValueTask<T>(waiter, waiter.Version);
    }

    // ========================================================================
    // Disposal
    // ========================================================================

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;
        _storage.Dispose();
    }
}

/// <summary>
/// Default production-ready queue specializing with <see cref="AdaptiveSpinBackoff"/>.
/// Provides a simplified API surface without requiring backoff type parameter.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class VyukovBoundedBatchQueue<T> : IBatchQueue<T>
{
    private readonly VyukovBoundedBatchQueue<T, AdaptiveSpinBackoff> _inner;

    public uint Capacity => _inner.Capacity;
    public int Count => _inner.Count;
    public bool IsEmpty => _inner.IsEmpty;
    public bool IsFull => _inner.IsFull;

    public VyukovBoundedBatchQueue(BufferCapacity capacity)
    {
        _inner = new VyukovBoundedBatchQueue<T, AdaptiveSpinBackoff>(capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TryEnqueueBatch(ReadOnlySpan<T> items) => _inner.TryEnqueueBatch(items);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(in T item) => _inner.TryEnqueue(in item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueueOperationStatus TryEnqueueBatchWithStatus(ReadOnlySpan<T> items, out int enqueuedCount)
        => _inner.TryEnqueueBatchWithStatus(items, out enqueuedCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueueOperationStatus TryEnqueueWithStatus(in T item)
        => _inner.TryEnqueueWithStatus(in item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TryDequeueBatch(Span<T> destination) => _inner.TryDequeueBatch(destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue([MaybeNullWhen(false)] out T item) => _inner.TryDequeue(out item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueueOperationStatus TryDequeueBatchWithStatus(Span<T> destination, out int dequeuedCount)
        => _inner.TryDequeueBatchWithStatus(destination, out dequeuedCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueueOperationStatus TryDequeueWithStatus([MaybeNullWhen(false)] out T item)
        => _inner.TryDequeueWithStatus(out item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> EnqueueBatchAsync(ReadOnlyMemory<T> items, CancellationToken cancellationToken = default)
        => _inner.EnqueueBatchAsync(items, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default)
        => _inner.EnqueueAsync(item, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> DequeueBatchAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        => _inner.DequeueBatchAsync(destination, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
        => _inner.DequeueAsync(cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DrainTo<TState, TConsumer>(Span<T> scratchBuffer, ref TState state, TConsumer consumer)
        where TState : allows ref struct
        where TConsumer : struct, ISlotConsumer<T, TState>
        => _inner.DrainTo(scratchBuffer, ref state, consumer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DrainBatchesTo<TState, TVisitor>(Span<T> scratchBuffer, ref TState state, TVisitor visitor)
        where TState : allows ref struct
        where TVisitor : struct, ISpanVisitor<T, TState>
        => _inner.DrainBatchesTo(scratchBuffer, ref state, visitor);

    public void Dispose() => _inner.Dispose();
}
