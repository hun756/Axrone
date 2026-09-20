namespace Axrone.Event;

/// <summary>Dictionary-backed snapshot store; latest version wins.</summary>
/// <typeparam name="TAggregate">Aggregate type.</typeparam>
/// <typeparam name="TId">Stream identity type.</typeparam>
public sealed class InMemorySnapshotStore<TAggregate, TId> : ISnapshotStore<TAggregate, TId>
    where TAggregate : class
    where TId : notnull
{
    private readonly ConcurrentDictionary<TId, (TAggregate Snapshot, long Version)> _snapshots = new();

    /// <inheritdoc/>
    public ValueTask SaveSnapshotAsync(TId id, TAggregate aggregate, long version, CancellationToken cancellationToken = default)
    {
        _snapshots[id] = (aggregate, version);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<(TAggregate? Snapshot, long Version)> TryGetSnapshotAsync(TId id, CancellationToken cancellationToken = default)
    {
        if (_snapshots.TryGetValue(id, out (TAggregate Snapshot, long Version) tuple))
        {
            return ValueTask.FromResult<(TAggregate?, long)>((tuple.Snapshot, tuple.Version));
        }

        return ValueTask.FromResult<(TAggregate?, long)>((null, 0L));
    }
}
