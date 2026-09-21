namespace Axrone.Event;

/// <summary>Append-only event log with optimistic concurrency per stream.</summary>
/// <typeparam name="TId">Stream identity type.</typeparam>
/// <typeparam name="TEvent">Domain event type.</typeparam>
public interface IEventStore<TId, TEvent>
    where TId : notnull
{
    /// <summary>Appends events; throws when the stream version moved past <paramref name="expectedVersion"/>.</summary>
    ValueTask AppendAsync(TId streamId, ReadOnlyMemory<EventEnvelope<TEvent>> events, long expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Reads up to <paramref name="maxCount"/> envelopes from the version onward.</summary>
    ValueTask<IReadOnlyList<EventEnvelope<TEvent>>> ReadStreamAsync(TId streamId, long fromVersionInclusive, int maxCount, CancellationToken cancellationToken = default);

    /// <summary>Current stream version; zero when the stream does not exist.</summary>
    ValueTask<long> GetStreamVersionAsync(TId streamId, CancellationToken cancellationToken = default);
}
