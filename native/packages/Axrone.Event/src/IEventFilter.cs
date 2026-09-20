namespace Axrone.Event;

/// <summary>Publish-gate deciding whether an envelope reaches any subscriber.</summary>
/// <typeparam name="TEvent">Payload type.</typeparam>
public interface IEventFilter<TEvent>
{
    /// <summary>Whether the envelope should be processed.</summary>
    bool ShouldProcess(in EventEnvelope<TEvent> envelope);
}
