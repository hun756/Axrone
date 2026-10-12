namespace Axrone.Memory.Arena;

/// <summary>
/// Allocation-free walk over the live chunks. The header layout is shared by
/// every policy instantiation, so one enumerator serves all arenas.
/// </summary>
public readonly unsafe struct ChunkEnumerable
{
    private readonly ChunkNode* _head;

    /// <summary>Creates a walk; prefer <c>Chunks</c> on the arena.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChunkEnumerable(void* head) => _head = (ChunkNode*)head;

    /// <summary>Returns the chunk enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkEnumerator GetEnumerator() => new(_head);
}

/// <summary>Stack-free enumerator over chunk snapshots.</summary>
public unsafe ref struct ChunkEnumerator
{
    private ChunkNode* _current;

    /// <summary>Creates an enumerator; prefer <c>Chunks</c> on the arena.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ChunkEnumerator(void* head)
    {
        _current = (ChunkNode*)head;
        Current = default;
    }

    /// <summary>The snapshot at the current position.</summary>
    public ChunkInfo Current { get; private set; }

    /// <summary>Advances to the next chunk, if any.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_current == null)
        {
            return false;
        }

        Current = new ChunkInfo(
            new ChunkId(_current->Id),
            new ByteSize(_current->Capacity),
            new ByteSize((nuint)Volatile.Read(ref _current->Cursor)),
            (nuint)_current->Buffer,
            _current->CommittedAllocationSize);

        _current = _current->Next;
        return true;
    }
}
