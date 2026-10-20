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

/// <summary>Channel-based async event dispatcher with per-subscriber fan-out.</summary>
internal sealed class EventBatchDispatcher : IAsyncDisposable
{
    private readonly Channel<AssetEvent> _channel;
    private readonly ConcurrentDictionary<int, Action<AssetEvent>> _subscribers = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _drainTask;
    private int _subIdCounter;

    /// <summary>Creates a dispatcher with a background drain loop.</summary>
    public EventBatchDispatcher()
    {
        _channel = Channel.CreateUnbounded<AssetEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        _drainTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>Enqueues an event.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in AssetEvent evt) => _channel.Writer.TryWrite(evt);

    /// <summary>Subscribes a listener.</summary>
    public IAssetSubscription Subscribe(Action<AssetEvent> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        int id = Interlocked.Increment(ref _subIdCounter);
        _subscribers[id] = listener;
        return new Subscription(() => _subscribers.TryRemove(id, out _));
    }

    private async Task ProcessQueueAsync()
    {
        ChannelReader<AssetEvent> reader = _channel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out AssetEvent evt))
                {
                    foreach (Action<AssetEvent> sub in _subscribers.Values)
                    {
                        try
                        {
                            sub(evt);
                        }
                        catch
                        {
                            // A failing listener must not stall the remaining subscribers.
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Teardown requested.
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);
        await _drainTask.ConfigureAwait(false);
        _cts.Dispose();
    }

    private sealed class Subscription(Action unsubscribe) : IAssetSubscription
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose()
        {
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
