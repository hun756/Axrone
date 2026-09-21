namespace Axrone.Event;

/// <summary>Publish/consumption observer; must never throw or block.</summary>
/// <typeparam name="TEvent">Payload type.</typeparam>
public interface IEventInterceptor<TEvent>
{
    /// <summary>Invoked after filters pass, before the envelope enters transport.</summary>
    void OnPublishing(in EventEnvelope<TEvent> envelope);

    /// <summary>Invoked after the envelope entered transport.</summary>
    void OnPublished(in EventEnvelope<TEvent> envelope);

    /// <summary>Invoked when a subscriber throws for the envelope.</summary>
    void OnConsumptionError(in EventEnvelope<TEvent> envelope, Exception exception);
}
