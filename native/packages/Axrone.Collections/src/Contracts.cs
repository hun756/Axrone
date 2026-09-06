namespace Axrone.Collections;

public interface IProducer<T>
{
    int Capacity { get; }
    int Count { get; }
    bool IsFull { get; }
    bool TryEnqueue(in T item);
    RingBufferOperationStatus Enqueue(in T item, TimeSpan timeout, CancellationToken cancellationToken = default);
    void Enqueue(in T item, CancellationToken cancellationToken = default);
    int EnqueueRange(ReadOnlySpan<T> source);
}

public interface IConsumer<T>
{
    int Capacity { get; }
    int Count { get; }
    bool IsEmpty { get; }
    bool TryDequeue([MaybeNullWhen(false)] out T item);
    RingBufferResult<T> Dequeue(TimeSpan timeout, CancellationToken cancellationToken = default);
    T Dequeue(CancellationToken cancellationToken = default);
    int DequeueRange(Span<T> destination);
    int DrainTo(Span<T> destination);
}

public interface IRingBuffer<T> : IProducer<T>, IConsumer<T>, IDisposable
{
    bool IsDisposed { get; }
    IProducer<T> Producer { get; }
    IConsumer<T> Consumer { get; }
    void Clear();
}
