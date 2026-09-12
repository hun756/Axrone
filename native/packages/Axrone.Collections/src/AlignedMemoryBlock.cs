namespace Axrone.Collections;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Axrone.Utility.Alignment;
using Axrone.Utility.Disposable;

public interface IAlignedMemoryBlock<T> : IDisposable where T : unmanaged
{
    nuint Alignment { get; }
    BatchCapacity Capacity { get; }
    Span<T> Span { get; }
    ReadOnlySpan<T> ReadOnlySpan { get; }
    unsafe T* RawPointer { get; }
}

public sealed unsafe class AlignedMemoryBlock<T> : IAlignedMemoryBlock<T> where T : unmanaged
{
    private nint _pointer;
    private readonly BatchCapacity _capacity;
    private readonly Alignment _alignment;
    private DisposalTracker _tracker;

    public nuint Alignment => _alignment.Value;
    public BatchCapacity Capacity => _capacity;

    public unsafe T* RawPointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            nint ptr = Volatile.Read(ref _pointer);
            if (ptr == 0) ThrowHelper.ThrowObjectDisposed();
            return (T*)ptr;
        }
    }

    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            nint ptr = Volatile.Read(ref _pointer);
            if (ptr == 0) ThrowHelper.ThrowObjectDisposed();
            return new Span<T>((void*)ptr, _capacity.Value);
        }
    }

    public ReadOnlySpan<T> ReadOnlySpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Span;
    }

    public AlignedMemoryBlock(BatchCapacity capacity, Alignment alignment)
    {
        _capacity = capacity;
        _alignment = alignment;
        nuint byteCount = (nuint)capacity.Value * (nuint)sizeof(T);
        _pointer = (nint)NativeMemory.AlignedAlloc(byteCount, alignment.Value);
        NativeMemory.Clear((void*)_pointer, byteCount);
    }

    ~AlignedMemoryBlock()
    {
        Release();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (_tracker.TryDispose())
        {
            nint ptr = Interlocked.Exchange(ref _pointer, 0);
            if (ptr != 0)
            {
                NativeMemory.AlignedFree((void*)ptr);
            }
        }
    }
}
