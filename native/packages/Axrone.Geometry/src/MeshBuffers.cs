namespace Axrone.Geometry;

/// <summary>
/// Growable 64-byte-aligned native buffer: the staging area behind mesh
/// assembly. Append-only with geometric growth; length resets without freeing.
/// Not thread-safe; transfer ownership with <c>ref</c> like the pipeline does.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public unsafe struct NativeBuffer<T> : IDisposable where T : unmanaged
{
    private T* _pointer;
    private nuint _length;
    private nuint _capacity;

    /// <summary>Live elements.</summary>
    public readonly nuint Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length;
    }

    /// <summary>Allocated slots.</summary>
    public readonly nuint Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _capacity;
    }

    /// <summary>Raw base pointer.</summary>
    public readonly T* RawPointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pointer;
    }

    /// <summary>Creates a buffer with at least 16 slots.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeBuffer(nuint initialCapacity)
    {
        nuint alignedCap = (initialCapacity + 15) & ~((nuint)15);
        if (alignedCap < 16) alignedCap = 16;
        _pointer = (T*)NativeMemory.AlignedAlloc(alignedCap * (nuint)sizeof(T), 64);
        _capacity = alignedCap;
        _length = 0;
    }

    /// <summary>Appends one element, growing geometrically when full.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Append(in T item)
    {
        if (_length >= _capacity)
        {
            Grow();
        }
        _pointer[_length++] = item;
    }

    /// <summary>Sets the length, growing when beyond capacity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SetLength(nuint length)
    {
        if (length > _capacity)
        {
            EnsureCapacity(length);
        }
        _length = length;
    }

    /// <summary>Grows to at least <paramref name="requiredCapacity"/> slots.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void EnsureCapacity(nuint requiredCapacity)
    {
        if (requiredCapacity <= _capacity) return;

        nuint next = _capacity * 2;
        if (next < requiredCapacity)
        {
            next = (requiredCapacity + 15) & ~((nuint)15);
        }

        T* newPtr = (T*)NativeMemory.AlignedAlloc(next * (nuint)sizeof(T), 64);
        if (_length > 0)
        {
            Buffer.MemoryCopy(_pointer, newPtr, next * (nuint)sizeof(T), _length * (nuint)sizeof(T));
        }
        NativeMemory.AlignedFree(_pointer);
        _pointer = newPtr;
        _capacity = next;
    }

    /// <summary>Returns a reference to the element at <paramref name="index"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly ref T AsRef(nuint index)
    {
        if (index >= _length)
        {
            ThrowHelper.ThrowIndexOutOfRange();
        }
        return ref _pointer[index];
    }

    /// <summary>Exposes the live elements.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Span<T> AsSpan() => new(_pointer, checked((int)_length));

    /// <summary>Exposes the live elements read-only.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<T> AsReadOnlySpan() => new(_pointer, checked((int)_length));

    /// <summary>Doubles the capacity, preserving elements.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow()
    {
        nuint next = _capacity * 2;
        T* newPtr = (T*)NativeMemory.AlignedAlloc(next * (nuint)sizeof(T), 64);
        if (_length > 0)
        {
            Buffer.MemoryCopy(_pointer, newPtr, next * (nuint)sizeof(T), _length * (nuint)sizeof(T));
        }
        NativeMemory.AlignedFree(_pointer);
        _pointer = newPtr;
        _capacity = next;
    }

    /// <summary>Forgets all elements without freeing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _length = 0;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_pointer != null)
        {
            NativeMemory.AlignedFree(_pointer);
            _pointer = null;
        }
        _length = 0;
        _capacity = 0;
    }
}

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
