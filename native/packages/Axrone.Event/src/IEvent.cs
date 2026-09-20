namespace Axrone.Event;

/// <summary>Self-identifying payload contract; opt-in for payloads that carry their own identity.</summary>
public interface IEvent
{
    /// <summary>Unique identity of this event.</summary>
    Guid EventId { get; }

    /// <summary>Conversation this event belongs to.</summary>
    Guid CorrelationId { get; }

    /// <summary>Event that caused this one, if any.</summary>
    Guid CausationId { get; }

    /// <summary>Schema version for upgraders.</summary>
    int Version { get; }
}
