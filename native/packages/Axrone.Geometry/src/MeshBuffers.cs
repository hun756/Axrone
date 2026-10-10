namespace Axrone.Geometry;

/// <summary>
/// Open-addressing midpoint cache for icosphere subdivision: maps an unordered
/// edge to its midpoint vertex. Grows geometrically past 75% load so probes
/// stay short and the table can never fill.
/// </summary>
internal unsafe struct UnmanagedEdgeTable : IDisposable
{
    private struct Slot
    {
        public ulong Key;
        public uint Value;
    }

    private Slot* _slots;
    private uint _mask;
    private uint _capacity;
    private uint _count;

    /// <summary>Creates a table holding at least <paramref name="minimumCapacity"/> edges.</summary>
    public UnmanagedEdgeTable(uint minimumCapacity)
    {
        uint cap = 16;
        while (cap < minimumCapacity) cap <<= 1;
        nuint byteSize = (nuint)cap * (nuint)sizeof(Slot);
        _slots = (Slot*)NativeMemory.AlignedAlloc(byteSize, 64);
        NativeMemory.Clear(_slots, byteSize);
        _mask = cap - 1;
        _capacity = cap;
        _count = 0;
    }

    /// <summary>
    /// Returns true with the recorded midpoint when the edge is known,
    /// otherwise records <paramref name="candidateValue"/> and returns false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public bool TryGetOrAdd(uint v0, uint v1, uint candidateValue, out uint existingValue)
    {
        if ((_count + 1) * 4 > _capacity * 3)
        {
            Grow();
        }

        uint min = v0 < v1 ? v0 : v1;
        uint max = v0 < v1 ? v1 : v0;
        ulong key = (((ulong)min) << 32) | max | 0x8000000000000000UL;

        ulong x = key;
        x ^= x >> 33;
        x *= 0xff51afd7ed558ccdUL;
        x ^= x >> 33;
        x *= 0xc4ceb9fe1a85ec53UL;
        x ^= x >> 33;
        uint idx = (uint)x & _mask;

        while (true)
        {
            ref Slot slot = ref _slots[idx];
            if (slot.Key == 0)
            {
                slot.Key = key;
                slot.Value = candidateValue;
                existingValue = candidateValue;
                _count++;
                return false;
            }
            if (slot.Key == key)
            {
                existingValue = slot.Value;
                return true;
            }
            idx = (idx + 1) & _mask;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow()
    {
        uint newCap = _capacity * 2;
        nuint byteSize = (nuint)newCap * (nuint)sizeof(Slot);
        Slot* newSlots = (Slot*)NativeMemory.AlignedAlloc(byteSize, 64);
        NativeMemory.Clear(newSlots, byteSize);
        uint newMask = newCap - 1;

        for (uint i = 0; i < _capacity; i++)
        {
            ref Slot slot = ref _slots[i];
            if (slot.Key == 0) continue;

            ulong x = slot.Key;
            x ^= x >> 33;
            x *= 0xff51afd7ed558ccdUL;
            x ^= x >> 33;
            x *= 0xc4ceb9fe1a85ec53UL;
            x ^= x >> 33;
            uint idx = (uint)x & newMask;

            while (newSlots[idx].Key != 0)
            {
                idx = (idx + 1) & newMask;
            }
            newSlots[idx] = slot;
        }

        NativeMemory.AlignedFree(_slots);
        _slots = newSlots;
        _mask = newMask;
        _capacity = newCap;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_slots != null)
        {
            NativeMemory.AlignedFree(_slots);
            _slots = null;
        }
        _mask = 0;
        _capacity = 0;
        _count = 0;
    }
}
