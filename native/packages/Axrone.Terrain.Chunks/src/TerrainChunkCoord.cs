namespace Axrone.Terrain.Chunks;

/// <summary>
/// Integer address of a terrain chunk on the infinite XZ grid. Chunks tile the plane, so
/// negative coordinates are valid.
/// </summary>
/// <param name="X">Chunk column.</param>
/// <param name="Z">Chunk row.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TerrainChunkCoord(int X, int Z) : IComparable<TerrainChunkCoord>
{
    /// <summary>Packs the coordinate into a single 64-bit key.</summary>
    /// <returns>The packed key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Pack() => ((ulong)(uint)X << 32) | (uint)Z;

    /// <summary>Unpacks a key produced by <see cref="Pack"/>.</summary>
    /// <param name="key">The packed key.</param>
    /// <returns>The coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TerrainChunkCoord Unpack(ulong key) => new((int)(key >> 32), (int)(key & 0xFFFFFFFF));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(TerrainChunkCoord other)
    {
        int compareX = X.CompareTo(other.X);
        return compareX != 0 ? compareX : Z.CompareTo(other.Z);
    }

    /// <summary>Orders two chunk coordinates by X, then Z.</summary>
    public static bool operator <(TerrainChunkCoord left, TerrainChunkCoord right) => left.CompareTo(right) < 0;

    /// <summary>Orders two chunk coordinates by X, then Z.</summary>
    public static bool operator <=(TerrainChunkCoord left, TerrainChunkCoord right) => left.CompareTo(right) <= 0;

    /// <summary>Orders two chunk coordinates by X, then Z.</summary>
    public static bool operator >(TerrainChunkCoord left, TerrainChunkCoord right) => left.CompareTo(right) > 0;

    /// <summary>Orders two chunk coordinates by X, then Z.</summary>
    public static bool operator >=(TerrainChunkCoord left, TerrainChunkCoord right) => left.CompareTo(right) >= 0;
}
