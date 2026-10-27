namespace Axrone.Terrain;

/// <summary>Lifecycle state stored in a chunk header.</summary>
public enum TerrainChunkState
{
    /// <summary>The slot is not in use.</summary>
    Free = 0,

    /// <summary>Heights and normals are being generated.</summary>
    Building = 1,

    /// <summary>The chunk is published in the directory.</summary>
    Ready = 2,
}

/// <summary>
/// Fixed slot geometry shared by the pool, the view and the mesh builder. Each chunk is a
/// 33x33 vertex grid (32x32 cells) with heights, normals, splat weights and four foliage
/// density layers laid out back to back. The slot is rounded to a 64-byte multiple so every
/// slab address from <c>ContiguousSlabPool</c> keeps cache-line alignment.
/// </summary>
public static class TerrainChunkLayout
{
    /// <summary>Cells per chunk side.</summary>
    public const int Subdivisions = 32;

    /// <summary>Vertices per chunk side.</summary>
    public const int VerticesPerAxis = Subdivisions + 1;

    /// <summary>Vertices in one chunk.</summary>
    public const int VertexCount = VerticesPerAxis * VerticesPerAxis;

    /// <summary>Cells in one chunk.</summary>
    public const int CellCount = Subdivisions * Subdivisions;

    /// <summary>Triangle indices in one chunk.</summary>
    public const int IndexCount = CellCount * 6;

    /// <summary>Foliage density layers per chunk.</summary>
    public const int FoliageLayers = 4;

    /// <summary>Bytes reserved for the chunk header.</summary>
    public const int HeaderBytes = 64;

    /// <summary>Byte offset of the normalized height array.</summary>
    public const int HeightsOffset = HeaderBytes;

    /// <summary>Byte size of the normalized height array.</summary>
    public const int HeightsBytes = VertexCount * sizeof(float);

    /// <summary>Byte offset of the interleaved XYZ normal array.</summary>
    public const int NormalsOffset = HeightsOffset + HeightsBytes;

    /// <summary>Byte size of the normal array.</summary>
    public const int NormalsBytes = VertexCount * 3 * sizeof(float);

    /// <summary>Byte offset of the RGBA splat weight array.</summary>
    public const int SplatOffset = NormalsOffset + NormalsBytes;

    /// <summary>Byte size of the splat array.</summary>
    public const int SplatBytes = VertexCount * 4;

    /// <summary>Byte offset of the foliage density array.</summary>
    public const int DensityOffset = SplatOffset + SplatBytes;

    /// <summary>Byte size of all foliage density layers.</summary>
    public const int DensityBytes = VertexCount * FoliageLayers;

    /// <summary>Payload size before slot alignment.</summary>
    public const int PayloadBytes = DensityOffset + DensityBytes;

    /// <summary>Slot size rounded to a 64-byte multiple.</summary>
    public const int SlotBytes = (PayloadBytes + 63) & ~63;

    /// <summary>Slot size expressed in 64-byte header elements, as the pool expects.</summary>
    public const int SlotBlocks = SlotBytes / HeaderBytes;
}

/// <summary>
/// 64-byte chunk header. The rest of the slot holds the arrays described by
/// <see cref="TerrainChunkLayout"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = TerrainChunkLayout.HeaderBytes)]
public struct TerrainChunkHeader
{
    /// <summary>Chunk grid coordinate.</summary>
    public TerrainChunkCoord Coord;

    /// <summary>Smallest normalized height in the chunk.</summary>
    public float MinHeight;

    /// <summary>Largest normalized height in the chunk.</summary>
    public float MaxHeight;

    /// <summary>Monotonic generation assigned when the slot was committed.</summary>
    public uint Generation;

    /// <summary>Current lifecycle state.</summary>
    public TerrainChunkState State;

    /// <summary>Reserved for future flags; always zero.</summary>
    public int Reserved;
}

/// <summary>
/// Ref struct over one chunk slot. The view is only valid while the owning engine lock is
/// held; it never escapes a public call.
/// </summary>
internal readonly ref struct TerrainChunkView
{
    private readonly Span<byte> _data;

    internal TerrainChunkView(Span<byte> data)
    {
        if (data.Length < TerrainChunkLayout.SlotBytes)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Chunk slot is {data.Length} bytes; expected at least {TerrainChunkLayout.SlotBytes}."),
                TerrainErrorCode.ValidationFailed);
        }

        _data = data;
    }

    internal ref TerrainChunkHeader Header =>
        ref Unsafe.As<byte, TerrainChunkHeader>(ref MemoryMarshal.GetReference(_data));

    internal Span<float> Heights =>
        MemoryMarshal.Cast<byte, float>(_data.Slice(TerrainChunkLayout.HeightsOffset, TerrainChunkLayout.HeightsBytes));

    internal Span<float> Normals =>
        MemoryMarshal.Cast<byte, float>(_data.Slice(TerrainChunkLayout.NormalsOffset, TerrainChunkLayout.NormalsBytes));

    internal Span<byte> Splat =>
        _data.Slice(TerrainChunkLayout.SplatOffset, TerrainChunkLayout.SplatBytes);

    internal Span<byte> Density =>
        _data.Slice(TerrainChunkLayout.DensityOffset, TerrainChunkLayout.DensityBytes);

    internal Span<byte> FoliageDensity(int layerIndex) =>
        Density.Slice(layerIndex * TerrainChunkLayout.VertexCount, TerrainChunkLayout.VertexCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Vec3 GetNormal(int vertexIndex)
    {
        Span<float> normals = Normals;
        int offset = vertexIndex * 3;
        return new Vec3(normals[offset], normals[offset + 1], normals[offset + 2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetNormal(int vertexIndex, in Vec3 normal)
    {
        Span<float> normals = Normals;
        int offset = vertexIndex * 3;
        normals[offset] = normal.X;
        normals[offset + 1] = normal.Y;
        normals[offset + 2] = normal.Z;
    }
}
