namespace Axrone.Collections;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Cacheline-aligned storage engine managing dual off-heap and on-heap topological splits.
/// Value types (unmanaged, no references) use native aligned memory;
/// reference types use GC-allocated arrays with proper root clearing.
/// </summary>
internal sealed unsafe class BoundedSlotStorage<T> : IDisposable
{
    private readonly uint _capacity;
    private readonly nuint _mask;
    private readonly nuint* _sequences;
    private readonly nint _nativePointer;
    private readonly T[]? _managedItems;
    private int _isDisposed;

    public uint Capacity => _capacity;
    public nuint Mask => _mask;
    public nuint* Sequences => _sequences;

    public BoundedSlotStorage(BufferCapacity capacity)
    {
        _capacity = capacity.Value;
        _mask = (nuint)_capacity - 1;

        // Allocate 64-byte aligned off-heap memory for monotonic sequences
        nuint seqBytes = (nuint)_capacity * (nuint)sizeof(nuint);
        _sequences = (nuint*)NativeMemory.AllocZeroed(seqBytes, 64);

        for (nuint i = 0; i < (nuint)_capacity; i++)
        {
            _sequences[i] = i;
        }

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            nuint itemBytes = (nuint)_capacity * (nuint)Unsafe.SizeOf<T>();
            _nativePointer = (nint)NativeMemory.AllocZeroed(itemBytes, 64);
            _managedItems = null;
        }
        else
        {
            _nativePointer = 0;
            _managedItems = GC.AllocateUninitializedArray<T>((int)_capacity);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ref T GetItemRef(SequenceNumber position)
    {
        nuint index = position.Value & _mask;
        if (_nativePointer != 0)
        {
            return ref Unsafe.Add(ref Unsafe.AsRef<T>((void*)_nativePointer), (nint)index);
        }
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_managedItems!), (nint)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void WriteBatch(SequenceNumber startPosition, ReadOnlySpan<T> batch)
    {
        nuint offset = startPosition.Value & _mask;
        nuint contiguous = _capacity - offset;
        int count = batch.Length;

        if ((nuint)count <= contiguous)
        {
            WriteSlice(batch, offset);
        }
        else
        {
            int firstChunk = (int)contiguous;
            int secondChunk = count - firstChunk;
            WriteSlice(batch.Slice(0, firstChunk), offset);
            WriteSlice(batch.Slice(firstChunk, secondChunk), 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void WriteSlice(ReadOnlySpan<T> slice, nuint offset)
    {
        if (_nativePointer != 0)
        {
            ReadOnlySpan<T> src = slice;
            Span<T> dest = new((void*)(_nativePointer + (nint)offset * Unsafe.SizeOf<T>()), slice.Length);
            src.CopyTo(dest);
        }
        else
        {
            ref T target = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_managedItems!), (nint)offset);
            Span<T> dest = MemoryMarshal.CreateSpan(ref target, slice.Length);
            slice.CopyTo(dest);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ReadBatch(SequenceNumber startPosition, Span<T> destination)
    {
        nuint offset = startPosition.Value & _mask;
        nuint contiguous = _capacity - offset;
        int count = destination.Length;

        if ((nuint)count <= contiguous)
        {
            ReadSlice(destination, offset);
        }
        else
        {
            int firstChunk = (int)contiguous;
            int secondChunk = count - firstChunk;
            ReadSlice(destination.Slice(0, firstChunk), offset);
            ReadSlice(destination.Slice(firstChunk, secondChunk), 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void ReadSlice(Span<T> slice, nuint offset)
    {
        if (_nativePointer != 0)
        {
            ReadOnlySpan<T> src = new((void*)(_nativePointer + (nint)offset * Unsafe.SizeOf<T>()), slice.Length);
            src.CopyTo(slice);
        }
        else
        {
            ref T target = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_managedItems!), (nint)offset);
            Span<T> src = MemoryMarshal.CreateSpan(ref target, slice.Length);
            src.CopyTo(slice);

            // GC root clearing to eliminate memory loitering
            src.Clear();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void PublishEnqueueSequences(SequenceNumber startPosition, nuint count)
    {
        nuint mask = _mask;
        nuint* seqs = _sequences;
        nuint start = startPosition.Value;
        nuint i = 0;
        nuint unrollLimit = count & ~(nuint)3;

        for (; i < unrollLimit; i += 4)
        {
            Volatile.Write(ref seqs[(start + i) & mask], start + i + 1);
            Volatile.Write(ref seqs[(start + i + 1) & mask], start + i + 2);
            Volatile.Write(ref seqs[(start + i + 2) & mask], start + i + 3);
            Volatile.Write(ref seqs[(start + i + 3) & mask], start + i + 4);
        }

        for (; i < count; i++)
        {
            Volatile.Write(ref seqs[(start + i) & mask], start + i + 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void PublishDequeueSequences(SequenceNumber startPosition, nuint count)
    {
        nuint mask = _mask;
        nuint cap = _capacity;
        nuint* seqs = _sequences;
        nuint start = startPosition.Value;
        nuint i = 0;
        nuint unrollLimit = count & ~(nuint)3;

        for (; i < unrollLimit; i += 4)
        {
            Volatile.Write(ref seqs[(start + i) & mask], start + i + cap);
            Volatile.Write(ref seqs[(start + i + 1) & mask], start + i + 1 + cap);
            Volatile.Write(ref seqs[(start + i + 2) & mask], start + i + 2 + cap);
            Volatile.Write(ref seqs[(start + i + 3) & mask], start + i + 3 + cap);
        }

        for (; i < count; i++)
        {
            Volatile.Write(ref seqs[(start + i) & mask], start + i + cap);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

        if (_sequences != null)
        {
            NativeMemory.AlignedFree(_sequences);
        }

        if (_nativePointer != 0)
        {
            NativeMemory.AlignedFree((void*)_nativePointer);
        }

        if (_managedItems != null)
        {
            Array.Clear(_managedItems);
        }

        GC.SuppressFinalize(this);
    }
}
