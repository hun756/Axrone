namespace Axrone.Memory;

using System.Runtime.InteropServices;
using Axrone.Utility.Alignment;
using Axrone.Utility.Disposable;

public sealed class ManagedArrayBlockAllocator<T> : IBlockAllocator<T>
{
    public static readonly ManagedArrayBlockAllocator<T> Instance = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Memory<T> Allocate(int elementCount, out object? lifetimeToken)
    {
        lifetimeToken = null;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            return new Memory<T>(new T[elementCount]);
        }
        return new Memory<T>(GC.AllocateUninitializedArray<T>(elementCount));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Free(Memory<T> memory, object? lifetimeToken) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ComputeByteSize(int elementCount) => checked(elementCount * (long)Unsafe.SizeOf<T>());
}

public sealed class PinnedHeapBlockAllocator<T> : IBlockAllocator<T>
{
    public static readonly PinnedHeapBlockAllocator<T> Instance = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Memory<T> Allocate(int elementCount, out object? lifetimeToken)
    {
        lifetimeToken = null;
        return new Memory<T>(GC.AllocateArray<T>(elementCount, pinned: true));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Free(Memory<T> memory, object? lifetimeToken) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ComputeByteSize(int elementCount) => checked(elementCount * (long)Unsafe.SizeOf<T>());
}

public sealed unsafe class NativeAlignedBlockAllocator<T> : IBlockAllocator<T> where T : unmanaged
{
    public static readonly NativeAlignedBlockAllocator<T> Instance = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Memory<T> Allocate(int elementCount, out object? lifetimeToken)
    {
        nuint byteSize = checked((nuint)elementCount * (nuint)sizeof(T));
        void* nativePointer = NativeMemory.AlignedAlloc(byteSize, (nuint)Alignment.CacheLine64Bytes);
        if (nativePointer == null)
        {
            ThrowAllocationFailed(byteSize);
        }

        NativeBlockMemoryManager<T> manager = new((T*)nativePointer, elementCount, (nuint)Alignment.CacheLine64Bytes);
        lifetimeToken = manager;
        return manager.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Free(Memory<T> memory, object? lifetimeToken)
    {
        if (lifetimeToken is NativeBlockMemoryManager<T> manager)
        {
            ((IDisposable)manager).Dispose();
            NativeMemory.AlignedFree(manager.Pointer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ComputeByteSize(int elementCount) => checked(elementCount * (long)sizeof(T));

    [DoesNotReturn]
    private static void ThrowAllocationFailed(nuint bytes) =>
        ThrowHelper.ThrowInsufficientMemory($"Failed to allocate {bytes} bytes with 64-byte alignment.");
}

internal sealed unsafe class NativeBlockMemoryManager<T> : MemoryManager<T> where T : unmanaged
{
    private readonly T* _pointer;
    private readonly int _length;
    private readonly nuint _alignment;
    private DisposalTracker _tracker;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeBlockMemoryManager(T* pointer, int length, nuint alignment)
    {
        _pointer = pointer;
        _length = length;
        _alignment = alignment;
    }

    public T* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pointer;
    }

    public nuint Alignment
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _alignment;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Span<T> GetSpan()
    {
        if (_tracker.IsDisposed)
        {
            ThrowDisposed();
        }
        return new Span<T>(_pointer, _length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (_tracker.IsDisposed)
        {
            ThrowDisposed();
        }
        if ((uint)elementIndex > (uint)_length)
        {
            ThrowIndexOutOfRange();
        }
        return new MemoryHandle(_pointer + elementIndex);
    }

    public override void Unpin() { }

    protected override void Dispose(bool disposing)
    {
        _tracker.TryDispose();
    }

    [DoesNotReturn]
    private static void ThrowDisposed() =>
        throw new ObjectDisposedException(nameof(NativeBlockMemoryManager<T>));

    [DoesNotReturn]
    private static void ThrowIndexOutOfRange() =>
        ThrowHelper.ThrowArgumentOutOfRangeException("elementIndex");
}
