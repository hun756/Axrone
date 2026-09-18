using Axrone.Utility.Result;

namespace Axrone.Collections;

/// <summary>Produces items into a bounded buffer with blocking and timeout support.</summary>
public interface IProducer<T>
{
    int Capacity { get; }
    int Count { get; }
    bool IsFull { get; }
    bool TryEnqueue(in T item);
    Result Enqueue(in T item, TimeSpan timeout, CancellationToken cancellationToken = default);
    void Enqueue(in T item, CancellationToken cancellationToken = default);
    int EnqueueRange(ReadOnlySpan<T> source);
}

/// <summary>Consumes items from a bounded buffer with blocking and timeout support.</summary>
public interface IConsumer<T>
{
    int Capacity { get; }
    int Count { get; }
    bool IsEmpty { get; }
    bool TryDequeue([MaybeNullWhen(false)] out T item);
    Result<T> Dequeue(TimeSpan timeout, CancellationToken cancellationToken = default);
    T Dequeue(CancellationToken cancellationToken = default);
    int DequeueRange(Span<T> destination);
    int DrainTo(Span<T> destination);
}

/// <summary>Fixed-capacity ring buffer supporting concurrent production and consumption.</summary>
public interface IRingBuffer<T> : IProducer<T>, IConsumer<T>, IDisposable
{
    bool IsDisposed { get; }
    IProducer<T> Producer { get; }
    IConsumer<T> Consumer { get; }
    void Clear();
}
