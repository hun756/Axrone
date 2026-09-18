namespace Axrone.Memory.Arena;

public readonly record struct ArenaMarker(int ChunkIndex, nuint Offset);
