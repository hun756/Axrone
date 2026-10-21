namespace Axrone.AssetGltf;

/// <summary>Split GLB container payload.</summary>
public readonly record struct GltfContainerData(
    string Format,
    ReadOnlyMemory<byte> JsonBytes,
    ReadOnlyMemory<byte>? BinChunk);

/// <summary>GLB/gltf container splitter (AOT-safe, zero-copy).</summary>
public static class GltfContainer
{
    private const uint GlbMagic = 0x46546C67;
    private const uint GlbVersion2 = 2;
    private const uint JsonChunkType = 0x4E4F534A;
    private const uint BinChunkType = 0x004E4942;

    /// <summary>Splits raw bytes into JSON + optional BIN chunk.</summary>
    public static GltfContainerData Parse(ReadOnlyMemory<byte> source)
    {
        if (source.Length >= 12 &&
            BinaryPrimitives.ReadUInt32LittleEndian(source.Span[..4]) == GlbMagic)
        {
            ReadOnlySpan<byte> span = source.Span;
            uint version = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4));
            if (version != GlbVersion2)
            {
                ThrowHelper.ThrowContainer($"Unsupported GLB version {version}.");
            }

            uint totalLength = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8, 4));
            if (totalLength > (uint)source.Length)
            {
                ThrowHelper.ThrowContainer("GLB declared length overruns payload capacity.");
            }

            int offset = 12;
            ReadOnlyMemory<byte>? json = null;
            ReadOnlyMemory<byte>? bin = null;

            while (offset + 8 <= (int)totalLength)
            {
                uint chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4));
                uint chunkType = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset + 4, 4));
                offset += 8;

                if ((uint)offset + chunkLength > totalLength)
                {
                    ThrowHelper.ThrowContainer("GLB chunk overflows container length.");
                }

                if (chunkType == JsonChunkType && !json.HasValue)
                {
                    json = source.Slice(offset, (int)chunkLength);
                }
                else if (chunkType == BinChunkType && !bin.HasValue)
                {
                    bin = source.Slice(offset, (int)chunkLength);
                }

                offset += (int)chunkLength;
            }

            if (!json.HasValue)
            {
                ThrowHelper.ThrowContainer("Required JSON chunk missing from GLB.");
            }

            return new GltfContainerData("glb", json!.Value, bin);
        }

        return new GltfContainerData("gltf", source, null);
    }
}
