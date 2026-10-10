namespace Axrone.Utility.NativeBuffer;

/// <summary>
/// Growable 64-byte-aligned native buffer: the staging area for native
/// assembly paths. Append-only with geometric growth; length resets without
/// freeing. Ownership is unique and moves by assignment — default the source
/// after a move, the way <c>NativeMesh</c> construction does, because two live
/// copies would free the same block twice. Not thread-safe and not guarded
/// against use-after-dispose: dispose exactly once at the end of the build.
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

    /// <summary>Creates a buffer with at least 16 slots; smaller requests are lifted.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeBuffer(nuint initialCapacity)
    {
        nuint alignedCap = (initialCapacity + 15) & ~((nuint)15);
        if (alignedCap < 16) alignedCap = 16;
        _pointer = (T*)NativeMemory.AlignedAlloc(checked(alignedCap * (nuint)sizeof(T)), 64);
        if (_pointer == null)
        {
            ThrowHelper.ThrowInsufficientMemory("Failed to allocate native buffer storage.");
        }
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

    /// <summary>Sets the length, growing when beyond capacity. New slots are uninitialized.</summary>
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

        nuint next = checked(_capacity * 2);
        if (next < requiredCapacity)
        {
            next = (requiredCapacity + 15) & ~((nuint)15);
        }

        T* newPtr = (T*)NativeMemory.AlignedAlloc(checked(next * (nuint)sizeof(T)), 64);
        if (newPtr == null)
        {
            ThrowHelper.ThrowInsufficientMemory("Failed to grow native buffer storage.");
        }
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
        nuint next = checked(_capacity * 2);
        T* newPtr = (T*)NativeMemory.AlignedAlloc(checked(next * (nuint)sizeof(T)), 64);
        if (newPtr == null)
        {
            ThrowHelper.ThrowInsufficientMemory("Failed to grow native buffer storage.");
        }
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
