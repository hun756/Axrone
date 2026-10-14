namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Minimal storage role: live count plus slot teardown.</summary>
public interface IJointStorage
{
    /// <summary>Live constraints.</summary>
    int Count { get; }

    /// <summary>Tears down one slot by sparse index; unknown slots are ignored.</summary>
    /// <param name="sparseIndex">The sparse index to release.</param>
    void DestroySlot(uint sparseIndex);
}

/// <summary>
/// Dense slot storage for one constraint archetype: swap-remove keeps the
/// live set packed, sparse indices recycle, and sweeps walk raw refs with no
/// per-item dispatch.
/// </summary>
/// <typeparam name="TPolicy">The monomorphic solver archetype.</typeparam>
/// <typeparam name="TData">The constraint storage layout.</typeparam>
[StructLayout(LayoutKind.Sequential)]
public sealed class JointStorage<TPolicy, TData> : IJointStorage
    where TPolicy : struct, IJointPolicy<TPolicy, TData>
    where TData : struct
{
    private TData[] _dense;
    private uint[] _denseToSparse;
    private uint[] _sparse;
    private uint[] _freeStack;
    private int _count;
    private int _freeCount;
    private int _sparseCapacity;

    /// <summary>Creates storage with room for sixteen constraints.</summary>
    /// <param name="capacity">The initial capacity.</param>
    public JointStorage(int capacity = 16)
    {
        _sparseCapacity = Math.Max(16, capacity);
        _dense = new TData[_sparseCapacity];
        _denseToSparse = new uint[_sparseCapacity];
        _sparse = new uint[_sparseCapacity];
        _freeStack = new uint[_sparseCapacity];

        Array.Fill(_sparse, uint.MaxValue);
        _count = 0;
        _freeCount = 0;
    }

    /// <inheritdoc/>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Live data packed front to back.</summary>
    public ReadOnlySpan<TData> DataSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _dense.AsSpan(0, _count);
    }

    /// <summary>Maps a dense position back to its sparse index.</summary>
    /// <param name="denseIndex">The dense position.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint SparseOfDense(int denseIndex) => _denseToSparse[denseIndex];

    /// <summary>Reads live data by sparse index; dead slots throw.</summary>
    /// <param name="sparseIndex">The sparse index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TData GetBySparseIndex(uint sparseIndex)
    {
        if (sparseIndex >= (uint)_sparseCapacity)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(sparseIndex));
        }

        uint denseIdx = _sparse[sparseIndex];
        if (denseIdx >= (uint)_count)
        {
            ThrowHelper.ThrowInvalidOperation("Accessing dead dense joint slot.");
        }

        return ref _dense[denseIdx];
    }

    /// <summary>Probes liveness by sparse index without throwing.</summary>
    /// <param name="sparseIndex">The sparse index.</param>
    /// <param name="denseIndex">The dense position on success.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetBySparseIndex(uint sparseIndex, out uint denseIndex)
    {
        if (sparseIndex < (uint)_sparseCapacity)
        {
            denseIndex = _sparse[sparseIndex];
            return denseIndex < (uint)_count;
        }

        denseIndex = uint.MaxValue;
        return false;
    }

    /// <summary>Appends an item and returns its sparse index.</summary>
    /// <param name="item">The constraint data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Allocate(in TData item)
    {
        if (_count >= _dense.Length)
        {
            int newCap = _dense.Length * 2;
            Array.Resize(ref _dense, newCap);
            Array.Resize(ref _denseToSparse, newCap);
        }

        uint sparseIndex;
        if (_freeCount > 0)
        {
            sparseIndex = _freeStack[--_freeCount];
        }
        else
        {
            sparseIndex = (uint)_count;
            if (sparseIndex >= (uint)_sparseCapacity)
            {
                int newSparseCap = _sparseCapacity * 2;
                Array.Resize(ref _sparse, newSparseCap);
                Array.Resize(ref _freeStack, newSparseCap);
                for (int i = _sparseCapacity; i < newSparseCap; i++)
                {
                    _sparse[i] = uint.MaxValue;
                }

                _sparseCapacity = newSparseCap;
            }
        }

        uint denseIndex = (uint)_count++;
        _dense[denseIndex] = item;
        _denseToSparse[denseIndex] = sparseIndex;
        _sparse[sparseIndex] = denseIndex;

        return sparseIndex;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DestroySlot(uint sparseIndex)
    {
        if (sparseIndex >= (uint)_sparseCapacity)
        {
            return;
        }

        uint denseIndex = _sparse[sparseIndex];
        if (denseIndex >= (uint)_count)
        {
            return;
        }

        int lastDenseIndex = _count - 1;
        if ((int)denseIndex != lastDenseIndex)
        {
            TData lastItem = _dense[lastDenseIndex];
            uint lastSparseIndex = _denseToSparse[lastDenseIndex];

            _dense[denseIndex] = lastItem;
            _denseToSparse[denseIndex] = lastSparseIndex;
            _sparse[lastSparseIndex] = denseIndex;
        }

        _sparse[sparseIndex] = uint.MaxValue;
        _count--;

        if (_freeCount >= _freeStack.Length)
        {
            Array.Resize(ref _freeStack, _freeStack.Length * 2);
        }

        _freeStack[_freeCount++] = sparseIndex;
    }

    /// <summary>Runs the policy init over the packed set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void InitVelocity(BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref TData start = ref MemoryMarshal.GetArrayDataReference(_dense);
        uint count = (uint)_count;
        for (uint i = 0; i < count; i++)
        {
            TPolicy.InitVelocity(ref Unsafe.Add(ref start, i), bodies, store, wakeTolerance, dt);
        }
    }

    /// <summary>Runs the policy velocity sweep over the packed set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SolveVelocity(BodyStore bodies, JointStore store)
    {
        ref TData start = ref MemoryMarshal.GetArrayDataReference(_dense);
        uint count = (uint)_count;
        for (uint i = 0; i < count; i++)
        {
            TPolicy.SolveVelocity(ref Unsafe.Add(ref start, i), bodies, store);
        }
    }

    /// <summary>Runs the policy position sweep over the packed set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SolvePosition(BodyStore bodies, JointStore store)
    {
        ref TData start = ref MemoryMarshal.GetArrayDataReference(_dense);
        uint count = (uint)_count;
        for (uint i = 0; i < count; i++)
        {
            TPolicy.SolvePosition(ref Unsafe.Add(ref start, i), bodies, store);
        }
    }

    /// <summary>Enumerates the packed set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DenseEnumerator GetEnumerator() => new(this);

    /// <summary>Zero-allocation enumerator over the packed set.</summary>
#pragma warning disable CA1034 // The enumerator rides its storage: it borrows the dense array and the live count, so it cannot meaningfully live outside the generic storage type.
    public ref struct DenseEnumerator
    {
        private readonly TData[] _array;
        private readonly int _length;
        private int _index;

        /// <summary>Binds the enumerator to storage.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DenseEnumerator(JointStorage<TPolicy, TData> storage)
        {
            _array = storage._dense;
            _length = storage._count;
            _index = -1;
        }

        /// <summary>Advances to the next live item.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _length)
            {
                _index = next;
                return true;
            }

            return false;
        }

        /// <summary>The current live item.</summary>
        public readonly ref readonly TData Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _array[_index];
        }
    }
#pragma warning restore CA1034
}
