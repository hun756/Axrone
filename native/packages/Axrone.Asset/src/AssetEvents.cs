using Axrone.Event;

namespace Axrone.Asset;

/// <summary>Asset event type.</summary>
public enum AssetEventType : byte
{
    /// <summary>No event.</summary>
    None = 0,

    /// <summary>Asset inserted or updated.</summary>
    Upsert = 1,

    /// <summary>Asset deleted.</summary>
    Delete = 2,

    /// <summary>Asset imported.</summary>
    Import = 3,
}

/// <summary>One asset change notice.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetEvent(AssetEventType Type, AssetId Id, AssetRevision Revision, ulong KindHash);

/// <summary>Asset subscription handle.</summary>
public interface IAssetSubscription : IDisposable
{
}

/// <summary>
/// Asset fan-out over <see cref="EventBus"/>: publishes are non-blocking and
/// a failing listener lands in dead letters instead of stalling the
/// remaining subscribers.
/// </summary>
internal sealed class EventBatchDispatcher : IAsyncDisposable
{
    private readonly EventBus _bus = new();

    /// <summary>Enqueues an event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in AssetEvent evt) => _bus.Publish(in evt);

    /// <summary>Subscribes a listener.</summary>
    public IAssetSubscription Subscribe(Action<AssetEvent> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        IEventSubscription subscription = _bus.Subscribe<AssetEvent>(
            (envelope, _) => listener(envelope.Payload));
        return new Subscription(subscription);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _bus.DisposeAsync();

    private sealed class Subscription : IAssetSubscription
    {
        private readonly IEventSubscription _inner;
        private int _disposed;

        public Subscription(IEventSubscription inner) => _inner = inner;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _inner.Dispose();
            }
        }
    }
}
