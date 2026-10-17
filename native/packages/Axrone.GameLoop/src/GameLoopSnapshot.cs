namespace Axrone.GameLoop;

/// <summary>
/// Versioned loop snapshot.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct GameLoopSnapshot<TState>(
    SnapshotVersion Version,
    GameLoopStatus Status,
    TState State,
    FrameIndex Frame,
    GameTime Elapsed,
    GameTime Accumulator,
    FixedStep FixedDelta,
    GameTime MaxDelta,
    uint MaxSubSteps,
    TimeScale TimeScale,
    long CapturedAtEpochMs);

/// <summary>
/// State serializer contract.
/// </summary>
/// <typeparam name="TState">State type.</typeparam>
/// <typeparam name="TSerialized">Serialized form.</typeparam>
public interface IStateSerializer<TState, TSerialized>
{
    /// <summary>Serializes state.</summary>
    TSerialized Serialize(in TState state);

    /// <summary>Deserializes state.</summary>
    TState Deserialize(in TSerialized serialized);
}
