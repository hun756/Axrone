namespace Axrone.Collections;

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

/// <summary>
/// Fine-grained contract for batch and single-item enqueue operations.
/// </summary>
/// <typeparam name="T">The type of elements managed by the queue.</typeparam>
public interface IBatchEnqueue<T>
{
    int TryEnqueueBatch(ReadOnlySpan<T> items);
    bool TryEnqueue(in T item);
    QueueOperationStatus TryEnqueueBatchWithStatus(ReadOnlySpan<T> items, out int enqueuedCount);
    QueueOperationStatus TryEnqueueWithStatus(in T item);
    ValueTask<int> EnqueueBatchAsync(ReadOnlyMemory<T> items, CancellationToken cancellationToken = default);
    ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fine-grained contract for batch and single-item dequeue operations.
/// </summary>
/// <typeparam name="T">The type of elements managed by the queue.</typeparam>
public interface IBatchDequeue<T>
{
    int TryDequeueBatch(Span<T> destination);
    bool TryDequeue([MaybeNullWhen(false)] out T item);
    QueueOperationStatus TryDequeueBatchWithStatus(Span<T> destination, out int dequeuedCount);
    QueueOperationStatus TryDequeueWithStatus([MaybeNullWhen(false)] out T item);
    ValueTask<int> DequeueBatchAsync(Memory<T> destination, CancellationToken cancellationToken = default);
    ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Expresses non-blocking observability and bounded metrics.
/// </summary>
public interface IBoundedQueueInfo
{
    uint Capacity { get; }
    int Count { get; }
    bool IsEmpty { get; }
    bool IsFull { get; }
}

/// <summary>
/// Unified aggregate contract for an ultra-high performance bounded batch queue.
/// </summary>
/// <typeparam name="T">The type of elements managed by the queue.</typeparam>
public interface IBatchQueue<T> : IBatchEnqueue<T>, IBatchDequeue<T>, IBoundedQueueInfo, IDisposable
{
}

/// <summary>
/// Static-abstract policy abstraction defining backoff progression on hardware contention.
/// Eliminates vtables and virtual dispatch overhead via static generic specialization.
/// </summary>
public interface IBackoffPolicy
{
    static abstract void Initialize(out int state);
    static abstract void Step(ref int state);
    static abstract void Reset(ref int state);
}

/// <summary>
/// Zero-allocation visitor contract allowing stack-only ref structs to consume individual elements.
/// </summary>
public interface ISlotConsumer<T, in TState> where TState : allows ref struct
{
    void Consume(in T item, ref TState state);
}

/// <summary>
/// Zero-allocation visitor contract allowing stack-only ref structs to consume contiguous spans.
/// </summary>
public interface ISpanVisitor<T, in TState> where TState : allows ref struct
{
    void Visit(ReadOnlySpan<T> batch, ref TState state);
}

/// <summary>
/// Structured diagnostic status code for operational inspection without exceptions.
/// </summary>
public enum QueueOperationStatus : byte
{
    Success = 0,
    Empty = 1,
    Full = 2,
    Contended = 3,
    Disposed = 4
}
