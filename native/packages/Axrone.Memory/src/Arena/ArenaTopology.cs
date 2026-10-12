namespace Axrone.Memory.Arena;

/// <summary>
/// Shared chunk header topology. Namespace-level because the CLR forbids
/// explicit layout on types nested in a generic class, and the chunk
/// inspector reads the same layout the engine writes.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal unsafe struct ChunkNode
{
    [FieldOffset(0)] public ChunkNode* Next;
    [FieldOffset(8)] public byte* Buffer;
    [FieldOffset(16)] public nuint Capacity;
    [FieldOffset(24)] public nuint CommittedAllocationSize;
    [FieldOffset(32)] public long Cursor;
    [FieldOffset(40)] public uint Id;
    [FieldOffset(44)] public uint Flags;
}

/// <summary>
/// Arena lifecycle counters isolated on two cache lines. Hoisted for the same
/// CLR explicit-layout rule as <see cref="ChunkNode"/>.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct PaddedArenaState
{
    [FieldOffset(0)] public IntPtr ActiveChunk;
    [FieldOffset(8)] public int ActiveLeases;
    [FieldOffset(12)] public int LifecycleState;
    [FieldOffset(16)] public uint ChunkSequence;

    [FieldOffset(64)] public nuint InitialChunkSize;
    [FieldOffset(72)] public nuint MaxChunkSize;
    [FieldOffset(80)] public nuint DefaultAlignment;
    [FieldOffset(88)] public uint ZeroOnReset;
}
