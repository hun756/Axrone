namespace Axrone.Collections;

using System.Numerics;
using System.Runtime.CompilerServices;

public sealed unsafe class VyukovBoundedBatchQueue<T, TBackoff> : IDisposable
    where T : unmanaged
    where TBackoff : struct, IContentionStrategy
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Cell
    {
        public nuint Sequence;
        public T Element;
    }

    private readonly Cell* _cells;
    private readonly nuint _bufferMask;
    private readonly nuint* _enqueuePosition;
    private readonly nuint* _dequeuePosition;
    private int _disposed;

    public nuint Capacity => _bufferMask + 1;

    public VyukovBoundedBatchQueue(BatchCapacity capacity)
    {
        nuint cap = BitOperations.RoundUpToPowerOf2((uint)capacity.Value);
        _bufferMask = cap - 1;

        nuint totalBytes = cap * (nuint)sizeof(Cell);
        _cells = (Cell*)NativeMemory.AlignedAlloc(totalBytes, 64);

        for (nuint i = 0; i < cap; ++i)
        {
            _cells[i].Sequence = i;
            _cells[i].Element = default;
        }

        _enqueuePosition = (nuint*)NativeMemory.AlignedAlloc(128, 128);
        _dequeuePosition = (nuint*)NativeMemory.AlignedAlloc(128, 128);
        NativeMemory.Clear(_enqueuePosition, 128);
        NativeMemory.Clear(_dequeuePosition, 128);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryEnqueue(in T item)
    {
        Cell* cell;
        nuint pos = Volatile.Read(ref *_enqueuePosition);
        uint spin = 0;

        while (true)
        {
            cell = &_cells[pos & _bufferMask];
            nuint seq = Volatile.Read(ref cell->Sequence);
            nint diff = (nint)seq - (nint)pos;

            if (diff == 0)
            {
                if (Interlocked.CompareExchange(ref *_enqueuePosition, pos + 1, pos) == pos)
                {
                    break;
                }
            }
            else if (diff < 0)
            {
                return false;
            }
            else
            {
                pos = Volatile.Read(ref *_enqueuePosition);
            }

            TBackoff.Backoff(++spin);
        }

        cell->Element = item;
        Volatile.Write(ref cell->Sequence, pos + 1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryDequeue(out T item)
    {
        Cell* cell;
        nuint pos = Volatile.Read(ref *_dequeuePosition);
        uint spin = 0;

        while (true)
        {
            cell = &_cells[pos & _bufferMask];
            nuint seq = Volatile.Read(ref cell->Sequence);
            nint diff = (nint)seq - (nint)(pos + 1);

            if (diff == 0)
            {
                if (Interlocked.CompareExchange(ref *_dequeuePosition, pos + 1, pos) == pos)
                {
                    break;
                }
            }
            else if (diff < 0)
            {
                item = default;
                return false;
            }
            else
            {
                pos = Volatile.Read(ref *_dequeuePosition);
            }

            TBackoff.Backoff(++spin);
        }

        item = cell->Element;
        Volatile.Write(ref cell->Sequence, pos + _bufferMask + 1);
        return true;
    }

    ~VyukovBoundedBatchQueue()
    {
        Release();
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            if (_cells != null) NativeMemory.AlignedFree(_cells);
            if (_enqueuePosition != null) NativeMemory.AlignedFree(_enqueuePosition);
            if (_dequeuePosition != null) NativeMemory.AlignedFree(_dequeuePosition);
        }
    }
}
