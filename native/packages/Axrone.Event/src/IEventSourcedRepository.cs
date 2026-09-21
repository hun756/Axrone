namespace Axrone.Event;

/// <summary>Load/save façade coordinating snapshots, history, and publication.</summary>
/// <typeparam name="TAggregate">Aggregate type.</typeparam>
/// <typeparam name="TId">Stream identity type.</typeparam>
/// <typeparam name="TEvent">Domain event type.</typeparam>
public interface IEventSourcedRepository<TAggregate, TId, TEvent>
    where TAggregate : EventSourcedAggregate<TId, TEvent>, new()
    where TId : notnull
{
    /// <summary>Rebuilds the aggregate; null when the stream does not exist.</summary>
    ValueTask<TAggregate?> LoadAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Appends uncommitted events, publishes them, and snapshots on threshold.</summary>
    ValueTask SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}
