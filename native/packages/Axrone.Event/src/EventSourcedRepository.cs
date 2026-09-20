namespace Axrone.Event;

using System.Runtime.InteropServices;

/// <summary>
/// Default repository: snapshot-first load, optimistic-concurrency save, publish-on-save with
/// version-threshold snapshots.
/// </summary>
/// <typeparam name="TAggregate">Aggregate type.</typeparam>
/// <typeparam name="TId">Stream identity type.</typeparam>
/// <typeparam name="TEvent">Domain event type.</typeparam>
public sealed class EventSourcedRepository<TAggregate, TId, TEvent> : IEventSourcedRepository<TAggregate, TId, TEvent>
    where TAggregate : EventSourcedAggregate<TId, TEvent>, new()
    where TId : notnull
{
    private readonly IEventStore<TId, TEvent> _eventStore;
    private readonly IEventBus _eventBus;
    private readonly ISnapshotStore<TAggregate, TId>? _snapshotStore;
    private readonly EventUpgradePipeline<TEvent>? _upgrades;
    private readonly int _snapshotThreshold;

    /// <summary>Creates a repository over the given store, bus, optional snapshots, and optional upgraders.</summary>
    public EventSourcedRepository(
        IEventStore<TId, TEvent> eventStore,
        IEventBus eventBus,
        ISnapshotStore<TAggregate, TId>? snapshotStore = null,
        int snapshotThreshold = 100,
        EventUpgradePipeline<TEvent>? upgrades = null)
    {
        _eventStore = eventStore;
        _eventBus = eventBus;
        _snapshotStore = snapshotStore;
        _snapshotThreshold = snapshotThreshold;
        _upgrades = upgrades;
    }

    /// <inheritdoc/>
    public async ValueTask<TAggregate?> LoadAsync(TId id, CancellationToken cancellationToken = default)
    {
        TAggregate aggregate = new();
        long fromVersion = 1L;

        if (_snapshotStore is not null)
        {
            (TAggregate? snapshot, long version) = await _snapshotStore.TryGetSnapshotAsync(id, cancellationToken).ConfigureAwait(false);
            if (snapshot is not null)
            {
                aggregate = snapshot;
                fromVersion = version + 1;
            }
        }

        IReadOnlyList<EventEnvelope<TEvent>> history =
            await _eventStore.ReadStreamAsync(id, fromVersion, int.MaxValue, cancellationToken).ConfigureAwait(false);
        if (history.Count == 0 && fromVersion == 1L)
        {
            return null;
        }

        if (history.Count > 0)
        {
            if (_upgrades is null && history is List<EventEnvelope<TEvent>> list)
            {
                aggregate.HydrateFromHistory(id, CollectionsMarshal.AsSpan(list));
            }
            else
            {
                var materialized = new EventEnvelope<TEvent>[history.Count];
                for (int i = 0; i < history.Count; i++)
                {
                    materialized[i] = Migrate(history[i]);
                }

                aggregate.HydrateFromHistory(id, materialized);
            }
        }

        return aggregate;
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        // Ownership transfer: the store may retain the memory past the call, so hand over a copy.
        // Save is a cold path; one array per save is noise next to persistence and publication.
        // The span never crosses an await; only the owned array does.
        ReadOnlySpan<EventEnvelope<TEvent>> uncommitted = aggregate.GetUncommittedEvents();
        if (uncommitted.IsEmpty)
        {
            return;
        }

        var owned = new EventEnvelope<TEvent>[uncommitted.Length];
        uncommitted.CopyTo(owned);

        long originalVersion = aggregate.Version - owned.Length;
        await _eventStore.AppendAsync(aggregate.Id, owned, originalVersion, cancellationToken).ConfigureAwait(false);

        for (int i = 0; i < owned.Length; i++)
        {
            _eventBus.PublishEnvelope(in owned[i]);
        }

        if (_snapshotStore is not null && _snapshotThreshold > 0 && aggregate.Version % _snapshotThreshold == 0)
        {
            await _snapshotStore.SaveSnapshotAsync(aggregate.Id, aggregate, aggregate.Version, cancellationToken).ConfigureAwait(false);
        }

        aggregate.ClearUncommittedEvents();
    }

    /// <summary>
    /// Migrates a stored envelope through the upgrade pipeline. Metadata keeps the stored
    /// version as provenance; only the hydrated payload moves forward.
    /// </summary>
    private EventEnvelope<TEvent> Migrate(EventEnvelope<TEvent> envelope)
    {
        if (_upgrades is null)
        {
            return envelope;
        }

        TEvent payload = envelope.Payload;
        TEvent migrated = _upgrades.Transform(in payload, envelope.Metadata.Version);
        return new EventEnvelope<TEvent>(envelope.Metadata, migrated);
    }
}
