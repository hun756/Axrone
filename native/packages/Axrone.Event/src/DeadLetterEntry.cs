namespace Axrone.Event;

/// <summary>
/// Immutable dead-letter record: the failed envelope plus a flat error string.
/// </summary>
/// <remarks>
/// Deliberately carries <em>no</em> <see cref="Exception"/> reference — retaining dispatch info
/// would pin exception stacks in a long-lived queue. Type name and message are enough to route.
/// <see cref="DeadLetteredAt"/> carries raw <see cref="Stopwatch"/> ticks.
/// </remarks>
/// <typeparam name="TEvent">Payload type.</typeparam>
public readonly record struct DeadLetterEntry<TEvent>(
    EventEnvelope<TEvent> Envelope,
    DeadLetterReason Reason,
    string? Error,
    long DeadLetteredAt)
{
    /// <summary>Captures a dead letter, stamping the clock and flattening the exception.</summary>
    public static DeadLetterEntry<TEvent> Capture(
        in EventEnvelope<TEvent> envelope,
        DeadLetterReason reason,
        Exception? exception) => new(
            envelope,
            reason,
            exception is null ? null : $"{exception.GetType().Name}: {exception.Message}",
            Stopwatch.GetTimestamp());
}
