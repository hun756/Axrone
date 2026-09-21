using Axrone.Utility.Alignment;
using Axrone.Utility.Builders;

namespace Axrone.Memory.Arena;

/// <summary>
/// Fluent builder for <see cref="ArenaMemoryRing{T, TBackoff}"/> over a mutable accumulator state.
/// </summary>
/// <remarks>
/// Capacity is structurally required via the entry points, so the required mask is empty and
/// domain validation is authoritative. Supports fork/reset for template workflows.
/// </remarks>
public sealed class ArenaRingBuilder<T>
    : AggregateBuilder<ArenaRingBuilder<T>, ArenaRingState<T>, ArenaMemoryRing<T, ProgressiveSpinBackoff>>
    where T : unmanaged
{
    public ArenaRingBuilder(nuint capacity)
    {
        State = ArenaRingState<T>.Default;
        State.Capacity = capacity;
    }

    public static ArenaRingBuilder<T> Create(nuint capacity) => new(capacity);

    protected override ArenaRingBuilder<T> Self => this;

    protected override PropertyBitmask64 RequiredMask => PropertyBitmask64.None;

    public ArenaRingBuilder<T> WithCapacity(nuint capacity)
    {
        State.Capacity = capacity;
        return this;
    }

    public ArenaRingBuilder<T> WithTopology(MemoryTopology topology)
    {
        State.Topology = topology;
        return this;
    }

    public ArenaRingBuilder<T> WithNativeAlignedStorage()
    {
        State.Topology = MemoryTopology.NativeAligned;
        return this;
    }

    public ArenaRingBuilder<T> WithPinnedObjectHeapStorage()
    {
        State.Topology = MemoryTopology.PinnedObjectHeap;
        return this;
    }

    public ArenaRingBuilder<T> WithAlignment(Alignment alignment)
    {
        State.Alignment = alignment;
        return this;
    }

    public ArenaRingBuilder<T> WithZeroOnRecycle(bool value = true)
    {
        State.ZeroOnRecycle = value;
        return this;
    }

    public ArenaRingBuilder<T> WithTelemetry(string meterName, string instanceName = "default")
    {
        State.MeterName = meterName;
        State.InstanceName = instanceName;
        return this;
    }

    /// <summary>Builds the ring with an explicit backoff policy.</summary>
    public ArenaMemoryRing<T, TBackoff> Build<TBackoff>()
        where TBackoff : struct, ISpinBackoff
    {
        return new ArenaMemoryRing<T, TBackoff>(
            new ArenaCapacity(State.Capacity),
            State.Topology,
            State.Alignment,
            State.ZeroOnRecycle,
            State.MeterName,
            State.InstanceName);
    }

    /// <summary>Builds the ring; preserves the product's throw contract.</summary>
    public override ArenaMemoryRing<T, ProgressiveSpinBackoff> Build()
    {
        return Build<ProgressiveSpinBackoff>();
    }

    /// <inheritdoc/>
    public override void Reset() => State = ArenaRingState<T>.Default;

    /// <inheritdoc/>
    public override ArenaRingBuilder<T> Fork() => CopyTo(new ArenaRingBuilder<T>(State.Capacity));
}
