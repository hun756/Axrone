namespace Axrone.Event;

/// <summary>
/// Immutable transport wrapper pairing routing metadata with a payload.
/// </summary>
/// <remarks>
/// Construct with just a payload for the unstamped form; the bus stamps it at publish. Payload
/// is copied into the envelope once — large payloads should travel by handle, not by value.
/// </remarks>
/// <typeparam name="TEvent">Payload type.</typeparam>
public readonly record struct EventEnvelope<TEvent>(EventMetadata Metadata, TEvent Payload)
{
    /// <summary>Wraps a payload without a stamp; for hand-off to the bus.</summary>
    public EventEnvelope(TEvent payload)
        : this(default, payload)
    {
    }

    /// <summary>Whether the bus stamped this envelope.</summary>
    public bool IsStamped => Metadata.IsStamped;
}
