namespace Axrone.Event;

/// <summary>Versioned aggregate snapshots skipping history replays on load.</summary>
/// <typeparam name="TAggregate">Aggregate type.</typeparam>
/// <typeparam name="TId">Stream identity type.</typeparam>
public interface ISnapshotStore<TAggregate, TId>
    where TAggregate : class
    where TId : notnull
{
    /// <summary>Saves the aggregate at the given version.</summary>
    ValueTask SaveSnapshotAsync(TId id, TAggregate aggregate, long version, CancellationToken cancellationToken = default);

    /// <summary>Latest snapshot; null when none exists.</summary>
    ValueTask<(TAggregate? Snapshot, long Version)> TryGetSnapshotAsync(TId id, CancellationToken cancellationToken = default);
}
