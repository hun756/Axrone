using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

public sealed record ArenaRingConfiguration
{
    public int Capacity { get; init; } = 1024;

    public MemoryTopology Topology { get; init; } = MemoryTopology.NativeAligned;

    public Alignment StorageAlignment { get; init; } = Alignment.CacheLine64;

    public bool ZeroInitialize { get; init; } = true;

    public ArenaRingConfiguration Validate()
    {
        if (!BitOperations.IsPow2(Capacity))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(Capacity), "Capacity must be a power of 2.");
        }

        if (Capacity < 2 || Capacity > (1 << 30))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(Capacity), "Capacity must be between 2 and 1073741824.");
        }

        return this;
    }
}
