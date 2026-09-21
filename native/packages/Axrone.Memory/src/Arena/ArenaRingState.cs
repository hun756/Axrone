using Axrone.Utility.Alignment;
using Axrone.Utility.Builders;

namespace Axrone.Memory.Arena;

/// <summary>
/// Mutable accumulator for <see cref="ArenaRingBuilder{T}"/>; materializes an
/// <see cref="ArenaMemoryRing{T, ProgressiveSpinBackoff}"/>.
/// </summary>
/// <remarks>
/// Capacity is structurally provided by the builder entry points, so the required mask is empty
/// and <see cref="TryValidate"/> is the authoritative gate. The capacity rule lives in
/// <see cref="ArenaCapacity"/> and is shared, never restated.
/// </remarks>
public struct ArenaRingState<T> : IAggregateDefinition<ArenaRingState<T>, ArenaMemoryRing<T, ProgressiveSpinBackoff>>,
    IEquatable<ArenaRingState<T>>
    where T : unmanaged
{
    /// <summary>Working defaults; capacity must still be provided.</summary>
    public static ArenaRingState<T> Default => new()
    {
        Topology = MemoryTopology.NativeAligned,
        Alignment = default,
        ZeroOnRecycle = false,
        MeterName = "Axrone.Memory.Arena",
        InstanceName = "default",
    };

    public nuint Capacity { get; set; }
    public MemoryTopology Topology { get; set; }
    public Alignment Alignment { get; set; }
    public bool ZeroOnRecycle { get; set; }
    public string MeterName { get; set; }
    public string InstanceName { get; set; }

    /// <inheritdoc/>
    public static ArenaMemoryRing<T, ProgressiveSpinBackoff> Materialize(in ArenaRingState<T> state) =>
        new(
            new ArenaCapacity(state.Capacity),
            state.Topology,
            state.Alignment,
            state.ZeroOnRecycle,
            state.MeterName,
            state.InstanceName);

    /// <inheritdoc/>
    public static bool TryValidate(in ArenaRingState<T> state, out BuilderDiagnostic diagnostic)
    {
        if (!ArenaCapacity.IsValid(state.Capacity))
        {
            diagnostic = BuilderDiagnostic.Fail(BuilderStatusCode.ValidationFailed, "Capacity must be a power of two >= 2.");
            return false;
        }

        diagnostic = BuilderDiagnostic.Ok;
        return true;
    }

    /// <inheritdoc/>
    public bool Equals(ArenaRingState<T> other) =>
        Capacity == other.Capacity &&
        Topology == other.Topology &&
        Alignment.Equals(other.Alignment) &&
        ZeroOnRecycle == other.ZeroOnRecycle &&
        MeterName == other.MeterName &&
        InstanceName == other.InstanceName;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ArenaRingState<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Capacity, Topology, Alignment, ZeroOnRecycle, MeterName, InstanceName);

    public static bool operator ==(ArenaRingState<T> left, ArenaRingState<T> right) => left.Equals(right);

    public static bool operator !=(ArenaRingState<T> left, ArenaRingState<T> right) => !left.Equals(right);
}
