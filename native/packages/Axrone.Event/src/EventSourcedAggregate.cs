namespace Axrone.Event;

using System.Runtime.InteropServices;

/// <summary>
/// Base for aggregates rebuilt from event history. Raises versioned envelopes into an
/// uncommitted buffer; the repository appends, publishes, and snapshots them.
/// </summary>
/// <remarks>
/// Identity is assigned at hydration by the repository — a loaded aggregate always carries the
/// stream ID it was read from. Timestamps are stamped at raise (cheap clock read, cold path);
/// identity and sequence are stamped later by the store and the bus.
/// </remarks>
/// <typeparam name="TId">Stream identity type.</typeparam>
/// <typeparam name="TEvent">Domain event type.</typeparam>
public abstract class EventSourcedAggregate<TId, TEvent>
    where TId : notnull
{
    private readonly List<EventEnvelope<TEvent>> _uncommitted = new();
    private TId _id = default!;
    private long _version;

    /// <summary>Stream identity; default until hydrated or set.</summary>
    public TId Id => _id;

    /// <summary>Events applied (history + raised).</summary>
    public long Version => _version;

    /// <summary>Assigns identity; call from constructors for new aggregates.</summary>
    protected void SetIdentity(TId id) => _id = id;

    /// <summary>Applies one event to state; must be deterministic for replays.</summary>
    protected abstract void Apply(in TEvent @event);

    /// <summary>Applies and buffers a version-1 event for the next save.</summary>
    protected void Emit(in TEvent @event) => Emit(in @event, version: 1);

    /// <summary>Applies and buffers a versioned event for the next save.</summary>
    protected void Emit(in TEvent @event, int version)
    {
        Apply(in @event);
        _version++;
        _uncommitted.Add(new EventEnvelope<TEvent>(
            new EventMetadata(Guid.Empty, Guid.Empty, Guid.Empty, 0L, version, 0, Stopwatch.GetTimestamp()),
            @event));
    }

    /// <summary>Buffered events not yet saved.</summary>
    public ReadOnlySpan<EventEnvelope<TEvent>> GetUncommittedEvents() =>
        CollectionsMarshal.AsSpan(_uncommitted);

    /// <summary>Clears the buffer after a successful save.</summary>
    public void ClearUncommittedEvents() => _uncommitted.Clear();

    /// <summary>Rebuilds state from history under the given stream identity.</summary>
    public void HydrateFromHistory(TId id, ReadOnlySpan<EventEnvelope<TEvent>> history)
    {
        _id = id;
        for (int i = 0; i < history.Length; i++)
        {
            ref readonly EventEnvelope<TEvent> envelope = ref history[i];
            TEvent payload = envelope.Payload;
            Apply(in payload);
            long sequence = envelope.Metadata.Sequence;
            _version = sequence > 0 ? sequence : _version + 1;
        }
    }
}
