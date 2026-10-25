namespace Axrone.Lighting;

/// <summary>Flat dense slot storage with an open-addressing index table.</summary>
internal sealed class LightStorageTable
{
    private struct HashEntry
    {
        public LightId Id;
        public int DenseIndex;
        public uint HashCode;
    }

    private LightData[] _dense;
    private HashEntry[] _lookup;
    private int _count;
    private int _mask;

    /// <summary>Stored light count.</summary>
    public int Count => _count;

    /// <summary>Creates storage.</summary>
    public LightStorageTable(int initialCapacity = 32)
    {
        int cap = Math.Max(16, (int)BitOperations.RoundUpToPowerOf2((uint)initialCapacity));
        _dense = new LightData[cap];
        _lookup = new HashEntry[cap * 2];
        _mask = (cap * 2) - 1;
        _count = 0;
    }

    /// <summary>Dense lights as a read-only span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<LightData> AsReadOnlySpan() => _dense.AsSpan(0, _count);

    /// <summary>Zero-alloc enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LightSpanEnumerator GetEnumerator() => new(AsReadOnlySpan());

    /// <summary>Whether a light exists.</summary>
    public bool Contains(LightId id) => FindLookupIndex(id) >= 0;

    /// <summary>Tries to get a light.</summary>
    public bool TryGet(LightId id, out LightData light)
    {
        int entry = FindLookupIndex(id);
        if (entry >= 0)
        {
            light = _dense[_lookup[entry].DenseIndex];
            return true;
        }

        light = default;
        return false;
    }

    /// <summary>Adds a light.</summary>
    public void Add(in LightData light)
    {
        if (FindLookupIndex(light.Id) >= 0)
        {
            ThrowHelper.ThrowDuplicateId(light.Id);
        }

        if (_count >= _dense.Length)
        {
            Array.Resize(ref _dense, _dense.Length * 2);
        }

        if ((_count + 1) * 2 >= _lookup.Length)
        {
            Rehash(_lookup.Length * 2);
        }

        int denseIdx = _count++;
        _dense[denseIdx] = light;
        InsertLookup(light.Id, denseIdx);
    }

    /// <summary>Removes a light.</summary>
    public bool Remove(LightId id)
    {
        int lookupIdx = FindLookupIndex(id);
        if (lookupIdx < 0)
        {
            return false;
        }

        int denseIdx = _lookup[lookupIdx].DenseIndex;
        int lastDenseIdx = --_count;

        if (denseIdx < lastDenseIdx)
        {
            LightData moved = _dense[lastDenseIdx];
            _dense[denseIdx] = moved;
            UpdateLookup(moved.Id, denseIdx);
        }

        _dense[lastDenseIdx] = default;
        DeleteLookup(lookupIdx);
        return true;
    }

    /// <summary>Updates a light.</summary>
    public void Update(in LightData light)
    {
        int lookupIdx = FindLookupIndex(light.Id);
        if (lookupIdx < 0)
        {
            ThrowHelper.ThrowNotFound(light.Id);
        }

        _dense[_lookup[lookupIdx].DenseIndex] = light;
    }

    /// <summary>Clears all lights.</summary>
    public void Clear()
    {
        if (_count > 0)
        {
            Array.Clear(_dense, 0, _count);
            Array.Clear(_lookup, 0, _lookup.Length);
            _count = 0;
        }
    }

    /// <summary>Copies dense lights into a target.</summary>
    public void CopyTo(LightData[] target, ref int targetCount)
    {
        Array.Copy(_dense, 0, target, 0, _count);
        targetCount = _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindLookupIndex(LightId id)
    {
        uint h = (uint)id.Value.GetHashCode();
        int step = 0;
        int idx = (int)(h & (uint)_mask);

        while (_lookup[idx].Id != default)
        {
            if (_lookup[idx].Id == id)
            {
                return idx;
            }

            step++;
            idx = (int)((h + (uint)(step * (step + 1)) / 2) & (uint)_mask);
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InsertLookup(LightId id, int denseIdx)
    {
        uint h = (uint)id.Value.GetHashCode();
        int step = 0;
        int idx = (int)(h & (uint)_mask);

        while (_lookup[idx].Id != default)
        {
            step++;
            idx = (int)((h + (uint)(step * (step + 1)) / 2) & (uint)_mask);
        }

        _lookup[idx] = new HashEntry { Id = id, DenseIndex = denseIdx, HashCode = h };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateLookup(LightId id, int newDenseIdx)
    {
        int idx = FindLookupIndex(id);
        if (idx >= 0)
        {
            _lookup[idx].DenseIndex = newDenseIdx;
        }
    }

    private void DeleteLookup(int lookupIdx)
    {
        _lookup[lookupIdx] = default;
        int step = 1;
        int nextIdx = (lookupIdx + step) & _mask;

        while (_lookup[nextIdx].Id != default)
        {
            HashEntry reinsert = _lookup[nextIdx];
            _lookup[nextIdx] = default;
            InsertLookup(reinsert.Id, reinsert.DenseIndex);
            step++;
            nextIdx = (lookupIdx + step) & _mask;
        }
    }

    private void Rehash(int newCapacity)
    {
        _lookup = new HashEntry[newCapacity];
        _mask = newCapacity - 1;

        for (int i = 0; i < _count; i++)
        {
            InsertLookup(_dense[i].Id, i);
        }
    }
}
