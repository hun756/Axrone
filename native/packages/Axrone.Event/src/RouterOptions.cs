namespace Axrone.Event;

/// <summary>
/// Immutable router configuration: transport capacity, dispatch batching, and dead-letter bound.
/// </summary>
/// <param name="Capacity">Ring slots per event type; must be a power of two.</param>
/// <param name="DispatchBatchSize">Envelopes per dispatch iteration; 1..4096.</param>
/// <param name="DeadLetterCapacity">Retained failures; oldest drop first when full.</param>
/// <param name="MeterName">OpenTelemetry meter name.</param>
public readonly record struct RouterOptions(int Capacity, int DispatchBatchSize, int DeadLetterCapacity, string MeterName)
{
    /// <summary>House tuning: 64k ring, 256-wide dispatch batches, 1k dead letters.</summary>
    public static RouterOptions Default => new(65536, 256, 1024, "Axrone.Event");
}
