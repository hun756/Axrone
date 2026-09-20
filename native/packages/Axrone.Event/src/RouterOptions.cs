namespace Axrone.Event;

/// <summary>
/// Immutable router configuration: transport capacity and dispatch batching.
/// </summary>
/// <param name="Capacity">Ring slots per event type; must be a power of two.</param>
/// <param name="DispatchBatchSize">Envelopes per dispatch iteration; 1..4096.</param>
public readonly record struct RouterOptions(int Capacity, int DispatchBatchSize)
{
    /// <summary>House tuning: 64k ring, 256-wide dispatch batches.</summary>
    public static RouterOptions Default => new(65536, 256);
}
