namespace Axrone.Memory;

internal sealed class MpmcRingBuffer<TItem>
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Cell
    {
        public nint Sequence;
        public TItem? Element;
    }

    private readonly Cell[] _storage;
    private readonly int _mask;
    private nint _producerIndex;
    private nint _consumerIndex;

    public MpmcRingBuffer(int bufferCapacity)
    {
        if (!BitOperations.IsPow2(bufferCapacity))
        {
            bufferCapacity = (int)BitOperations.RoundUpToPowerOf2((uint)bufferCapacity);
        }

        _storage = new Cell[bufferCapacity];
        _mask = bufferCapacity - 1;

        for (nint i = 0; i < bufferCapacity; i++)
        {
            _storage[i].Sequence = i;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryEnqueue(TItem item)
    {
        Cell[] storage = _storage;
        int mask = _mask;

        while (true)
        {
            nint current = Volatile.Read(ref _producerIndex);
            ref Cell cell = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(storage), (int)(current & mask));
            nint sequence = Volatile.Read(ref cell.Sequence);
            nint difference = sequence - current;

            if (difference == 0)
            {
                if (Interlocked.CompareExchange(ref _producerIndex, current + 1, current) == current)
                {
                    cell.Element = item;
                    Volatile.Write(ref cell.Sequence, current + 1);
                    return true;
                }
            }
            else if (difference < 0)
            {
                return false;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryDequeue([MaybeNullWhen(false)] out TItem item)
    {
        Cell[] storage = _storage;
        int mask = _mask;

        while (true)
        {
            nint current = Volatile.Read(ref _consumerIndex);
            ref Cell cell = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(storage), (int)(current & mask));
            nint sequence = Volatile.Read(ref cell.Sequence);
            nint difference = sequence - (current + 1);

            if (difference == 0)
            {
                if (Interlocked.CompareExchange(ref _consumerIndex, current + 1, current) == current)
                {
                    item = cell.Element!;
                    cell.Element = default;
                    Volatile.Write(ref cell.Sequence, current + mask + 1);
                    return true;
                }
            }
            else if (difference < 0)
            {
                item = default;
                return false;
            }
        }
    }
}
