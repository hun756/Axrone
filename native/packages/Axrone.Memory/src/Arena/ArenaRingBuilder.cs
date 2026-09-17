using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

public sealed class ArenaRingBuilder<T, TBackoff>
    where T : unmanaged
    where TBackoff : struct, IBackoffPolicy
{
    private ArenaRingConfiguration _configuration = new();

    public ArenaRingBuilder<T, TBackoff> WithCapacity(int capacity)
    {
        _configuration = _configuration with { Capacity = capacity };
        return this;
    }

    public ArenaRingBuilder<T, TBackoff> WithTopology(MemoryTopology topology)
    {
        _configuration = _configuration with { Topology = topology };
        return this;
    }

    public ArenaRingBuilder<T, TBackoff> WithAlignment(Alignment alignment)
    {
        _configuration = _configuration with { StorageAlignment = alignment };
        return this;
    }

    public ArenaRingBuilder<T, TBackoff> WithZeroInitialize(bool zeroInitialize = true)
    {
        _configuration = _configuration with { ZeroInitialize = zeroInitialize };
        return this;
    }

    public ArenaMemoryRing<T, TBackoff> Build()
    {
        _configuration.Validate();

        IArenaStorageBlock<T> storage;
        switch (_configuration.Topology)
        {
            case MemoryTopology.NativeAligned:
                storage = new NativeAlignedArenaStorageBlock<T>(_configuration.Capacity, _configuration.StorageAlignment);
                break;
            case MemoryTopology.PinnedHeap:
                storage = new PinnedHeapArenaStorageBlock<T>(_configuration.Capacity, _configuration.StorageAlignment);
                break;
            default:
                ThrowHelper.ThrowInvalidOperationException($"Unknown topology: {_configuration.Topology}");
                return null!;
        }

        return new ArenaMemoryRing<T, TBackoff>(storage);
    }
}
