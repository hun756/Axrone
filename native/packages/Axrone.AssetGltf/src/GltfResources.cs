namespace Axrone.AssetGltf;

/// <summary>External resource fetch request.</summary>
public readonly record struct GltfResourceRequest(
    string Uri,
    string? AbsoluteUri,
    string Kind,
    string? MimeType);

/// <summary>Fetched resource payload.</summary>
public readonly record struct GltfResolvedResource(
    ReadOnlyMemory<byte> Data,
    string? MimeType);

/// <summary>External resource resolver callback.</summary>
/// <param name="request">Resource request.</param>
/// <param name="token">Cancellation token.</param>
public delegate ValueTask<GltfResolvedResource?> GltfResourceResolver(GltfResourceRequest request, CancellationToken token);

/// <summary>Texture payload variants.</summary>
public abstract record GltfTexturePayload;

/// <summary>External texture reference.</summary>
public sealed record GltfExternalTexturePayload(string Uri) : GltfTexturePayload;

/// <summary>Raw decoded texture bytes.</summary>
public sealed record GltfRawTexturePayload(ReadOnlyMemory<byte> Bytes, string MimeType) : GltfTexturePayload;

/// <summary>Compressed KTX2 texture payload.</summary>
public sealed record GltfCompressedTexturePayload(
    int VkFormat,
    int Width,
    int Height,
    int LevelCount,
    int SupercompressionScheme,
    ImmutableArray<ReadOnlyMemory<byte>> Levels) : GltfTexturePayload;

/// <summary>KTX2 fast header parser.</summary>
public static class Ktx2Decoder
{
    private static ReadOnlySpan<byte> Ktx2Identifier => "\xABKTX 20\xBB\r\n\x1A\n"u8;

    /// <summary>Tries to parse KTX2 mip levels without transcoding.</summary>
    public static bool TryParse(
        ReadOnlyMemory<byte> data,
        [NotNullWhen(true)] out GltfCompressedTexturePayload? payload,
        out string? errorMessage)
    {
        payload = null;
        errorMessage = null;

        ReadOnlySpan<byte> span = data.Span;
        if (span.Length < 80)
        {
            errorMessage = "KTX2 header buffer underrun.";
            return false;
        }

        if (!span[..12].SequenceEqual(Ktx2Identifier))
        {
            errorMessage = "Invalid KTX2 identifier signature.";
            return false;
        }

        uint vkFormat = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12, 4));
        uint pixelWidth = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(20, 4));
        uint pixelHeight = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(24, 4));
        uint pixelDepth = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(28, 4));
        uint layerCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(32, 4));
        uint faceCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(36, 4));
        uint levelCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(40, 4));
        uint supercompressionScheme = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(44, 4));

        if (pixelDepth > 1 || layerCount > 1 || faceCount > 1)
        {
            errorMessage = "3D/Array/Cubemap KTX2 textures are prohibited by runtime profile.";
            return false;
        }

        levelCount = Math.Max(1u, levelCount);
        int levelIndexOffset = 80;
        int levelIndexSize = (int)levelCount * 24;

        if (span.Length < levelIndexOffset + levelIndexSize)
        {
            errorMessage = "KTX2 level index buffer truncated.";
            return false;
        }

        var levelsBuilder = ImmutableArray.CreateBuilder<ReadOnlyMemory<byte>>((int)levelCount);
        for (int i = 0; i < (int)levelCount; i++)
        {
            int entryOffset = levelIndexOffset + (i * 24);
            ulong byteOffset = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(entryOffset, 8));
            ulong byteLength = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(entryOffset + 8, 8));

            if (byteOffset + byteLength > (ulong)data.Length)
            {
                errorMessage = $"KTX2 mip level {i} payload exceeds container boundaries.";
                return false;
            }

            levelsBuilder.Add(data.Slice((int)byteOffset, (int)byteLength));
        }

        payload = new GltfCompressedTexturePayload(
            (int)vkFormat,
            (int)pixelWidth,
            (int)pixelHeight,
            (int)levelCount,
            (int)supercompressionScheme,
            levelsBuilder.MoveToImmutable());

        return true;
    }

    /// <summary>Maps a Vulkan format to a runtime texture format.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GltfTextureFormat MapVkFormat(int vkFormat) =>
        vkFormat switch
        {
            131 => GltfTextureFormat.BC1_RGB,
            137 => GltfTextureFormat.BC3_RGBA,
            139 => GltfTextureFormat.BC4_R,
            141 => GltfTextureFormat.BC5_RG,
            145 => GltfTextureFormat.BC7_RGBA,
            >= 157 and <= 170 => (GltfTextureFormat)vkFormat,
            _ => GltfTextureFormat.Unknown,
        };
}
