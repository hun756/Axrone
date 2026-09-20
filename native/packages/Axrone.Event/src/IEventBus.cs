namespace Axrone.Event;

/// <summary>
/// Typed publish/subscribe façade over per-type routers. Implementations own transport,
/// stamping (identity + sequence at the publish edge), fan-out, and dead letters.
/// </summary>
public interface IEventBus
{
    /// <summary>Publishes a payload; the bus wraps and stamps the envelope.</summary>
    void Publish<TEvent>(in TEvent message);

    /// <summary>Publishes a pre-built envelope (stamped or unstamped).</summary>
    void PublishEnvelope<TEvent>(in EventEnvelope<TEvent> envelope);

    /// <summary>Subscribes a synchronous handler.</summary>
    IEventSubscription Subscribe<TEvent>(Action<EventEnvelope<TEvent>, CancellationToken> handler);

    /// <summary>Subscribes an asynchronous handler.</summary>
    IEventSubscription SubscribeAsync<TEvent>(Func<EventEnvelope<TEvent>, CancellationToken, ValueTask> handler);
}
