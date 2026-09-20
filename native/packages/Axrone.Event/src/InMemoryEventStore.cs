namespace Axrone.Event;

/// <summary>
/// Contiguous in-memory event log. Segments are per-stream locked; appends assign stream
/// sequences; concurrent writers on one stream serialize on optimistic version checks.
/// </summary>
/// <typeparam name="TId">Stream identity type.</typeparam>
/// <typeparam name="TEvent">Domain event type.</typeparam>
public sealed class InMemoryEventStore<TId, TEvent> : IEventStore<TId, TEvent>
    where TId : notnull
{
    private sealed class StreamSegment
    {
        public readonly Lock Gate = new();
        public readonly List<EventEnvelope<TEvent>> Events = new(128);
        public long CurrentVersion;
    }

    private readonly ConcurrentDictionary<TId, StreamSegment> _streams = new();

    /// <inheritdoc/>
    public ValueTask AppendAsync(TId streamId, ReadOnlyMemory<EventEnvelope<TEvent>> events, long expectedVersion, CancellationToken cancellationToken = default)
    {
        StreamSegment segment = _streams.GetOrAdd(streamId, static _ => new StreamSegment());

        lock (segment.Gate)
        {
            if (segment.CurrentVersion != expectedVersion)
            {
                ThrowHelper.ThrowConcurrencyConflict(expectedVersion, segment.CurrentVersion);
            }

            ReadOnlySpan<EventEnvelope<TEvent>> span = events.Span;
            for (int i = 0; i < span.Length; i++)
            {
                segment.CurrentVersion++;
                EventEnvelope<TEvent> envelope = span[i];
                segment.Events.Add(new EventEnvelope<TEvent>(
                    envelope.Metadata.WithSequence(segment.CurrentVersion),
                    envelope.Payload));
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<EventEnvelope<TEvent>>> ReadStreamAsync(TId streamId, long fromVersionInclusive, int maxCount, CancellationToken cancellationToken = default)
    {
        if (!_streams.TryGetValue(streamId, out StreamSegment? segment))
        {
            return ValueTask.FromResult<IReadOnlyList<EventEnvelope<TEvent>>>(Array.Empty<EventEnvelope<TEvent>>());
        }

        lock (segment.Gate)
        {
            var results = new List<EventEnvelope<TEvent>>();
            List<EventEnvelope<TEvent>> events = segment.Events;
            for (int i = 0; i < events.Count && results.Count < maxCount; i++)
            {
                if (events[i].Metadata.Sequence >= fromVersionInclusive)
                {
                    results.Add(events[i]);
                }
            }

            return ValueTask.FromResult<IReadOnlyList<EventEnvelope<TEvent>>>(results);
        }
    }

    /// <inheritdoc/>
    public ValueTask<long> GetStreamVersionAsync(TId streamId, CancellationToken cancellationToken = default)
    {
        if (_streams.TryGetValue(streamId, out StreamSegment? segment))
        {
            lock (segment.Gate)
            {
                return ValueTask.FromResult(segment.CurrentVersion);
            }
        }

        return ValueTask.FromResult(0L);
    }
}
