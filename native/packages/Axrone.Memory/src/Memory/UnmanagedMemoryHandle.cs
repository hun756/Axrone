namespace Axrone.Memory;

/// <summary>
/// A lightweight class wrapping a native memory pointer rented from <see cref="UnmanagedMemoryPool"/>.
/// Provides zero-overhead access to unmanaged memory as spans and pointers.
/// Reference type to prevent double-free on copy.
/// </summary>
public sealed class UnmanagedMemoryHandle : IDisposable
{
    private UnmanagedMemoryPool? _pool;
    private IntPtr _pointer;
    private readonly int _size;

    internal UnmanagedMemoryHandle(UnmanagedMemoryPool pool, IntPtr pointer, int size)
    {
        _pool = pool;
        _pointer = pointer;
        _size = size;
    }

    /// <summary>Gets the native memory pointer.</summary>
    public IntPtr Pointer => _pointer;

    /// <summary>Gets the size of the allocated buffer in bytes.</summary>
    public int Size => _size;

    /// <summary>Gets the buffer contents as a <see cref="Span{T}"/> of bytes.</summary>
    /// <returns>A span over the native memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<byte> AsSpan() => new((void*)_pointer, _size);

    /// <summary>Gets the buffer contents as a <see cref="Span{T}"/> of the specified unmanaged type.</summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <returns>A span over the native memory reinterpreted as <typeparamref name="T"/> elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<T> AsSpan<T>() where T : unmanaged =>
        new((void*)_pointer, _size / Unsafe.SizeOf<T>());

    /// <summary>Gets the raw native pointer as a <c>void*</c>.</summary>
    /// <returns>The native pointer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void* ToPointer() => (void*)_pointer;

    /// <summary>Creates a typed <see cref="UnmanagedMemoryHandle{T}"/> over this buffer.</summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <returns>A typed handle with length computed from the buffer size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedMemoryHandle<T> As<T>() where T : unmanaged =>
        new(this, 0, _size / Unsafe.SizeOf<T>());

    /// <summary>Returns the buffer to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var pointer = Interlocked.Exchange(ref _pointer, IntPtr.Zero);
        if (pointer != IntPtr.Zero)
        {
            _pool?.Return(pointer, _size);
            _pool = null;
        }
    }
}

/// <summary>
/// A typed struct wrapping a native memory pointer, providing indexed access
/// and span views over unmanaged memory for a specific unmanaged type.
/// </summary>
/// <typeparam name="T">Unmanaged element type.</typeparam>
public readonly struct UnmanagedMemoryHandle<T> : IDisposable where T : unmanaged
{
    private readonly IntPtr _pointer;
    private readonly int _length;
    private readonly UnmanagedMemoryHandle? _baseHandle;

    internal UnmanagedMemoryHandle(IntPtr pointer, int length)
    {
        _pointer = pointer;
        _length = length;
        _baseHandle = null;
    }

    internal UnmanagedMemoryHandle(UnmanagedMemoryHandle baseHandle, int offset, int length)
    {
        _baseHandle = baseHandle;
        _length = length;
        unsafe { _pointer = (IntPtr)((byte*)baseHandle.Pointer + (nint)offset * Unsafe.SizeOf<T>()); }
    }

    /// <summary>Gets the native memory pointer.</summary>
    public IntPtr Pointer => _pointer;

    /// <summary>Gets the number of <typeparamref name="T"/> elements in this handle.</summary>
    public int Length => _length;

    /// <summary>Gets the buffer contents as a <see cref="Span{T}"/>.</summary>
    /// <returns>A span over the native memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<T> AsSpan() => new((void*)_pointer, _length);

    /// <summary>Gets the buffer contents as a <see cref="ReadOnlySpan{T}"/>.</summary>
    /// <returns>A read-only span over the native memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ReadOnlySpan<T> AsReadOnlySpan() => new((void*)_pointer, _length);

    /// <summary>Gets the raw native pointer as a typed pointer.</summary>
    /// <returns>The typed native pointer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe T* ToPointer() => (T*)_pointer;

    /// <summary>Gets a reference to the element at the specified index.</summary>
    /// <param name="index">Zero-based index.</param>
    /// <returns>A reference to the element.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is out of bounds.</exception>
    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(index));

            unsafe
            {
                return ref Unsafe.Add(ref Unsafe.AsRef<T>((void*)_pointer), index);
            }
        }
    }

    /// <summary>Creates a slice of this handle.</summary>
    /// <param name="start">Start element index.</param>
    /// <param name="length">Number of elements.</param>
    /// <returns>A new typed handle covering the specified range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Range is outside the handle bounds.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedMemoryHandle<T> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
            throw new ArgumentOutOfRangeException(nameof(start));

        unsafe { return new UnmanagedMemoryHandle<T>((IntPtr)((byte*)_pointer + (nint)start * Unsafe.SizeOf<T>()), length); }
    }

    /// <summary>Copies the handle contents to the destination span.</summary>
    /// <param name="destination">Destination span.</param>
    /// <exception cref="ArgumentOutOfRangeException">Destination is too small.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<T> destination)
    {
        if (destination.Length < _length)
            throw new ArgumentOutOfRangeException(nameof(destination));

        AsSpan().CopyTo(destination);
    }

    /// <summary>Disposes the underlying base handle, returning memory to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _baseHandle?.Dispose();
    }
}
