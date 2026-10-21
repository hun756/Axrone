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
    ImmutableArray<ReadOnlyMemory<byte>> Levels) : GltfTexturePayload
{
    /// <summary>Mip level metadata for packed transcoded output.</summary>
    public ImmutableArray<GltfTextureMipLevel> MipLevels { get; init; } = ImmutableArray<GltfTextureMipLevel>.Empty;
}

/// <summary>Transcoded texture payload with packed mip levels.</summary>
public sealed record GltfTranscodedTexturePayload(
    ReadOnlyMemory<byte> Bytes,
    GltfTextureFormat Format,
    ImmutableArray<GltfTextureMipLevel> Levels) : GltfTexturePayload;

/// <summary>Packed mip level metadata.</summary>
public readonly record struct GltfTextureMipLevel(
    int Level,
    int Width,
    int Height,
    int ByteOffset,
    int ByteLength);

/// <summary>KTX2 fast header parser.</summary>
public static class Ktx2Decoder
{
    // KTX2 magic as raw bytes: \xAB/\xBB in a u8 literal would UTF-8-expand to
    // two bytes each, so the 12-byte signature is spelled out explicitly.
    private static ReadOnlySpan<byte> Ktx2Identifier =>
        [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

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

/// <summary>Buffer and bufferView resolution with meshopt support.</summary>
internal sealed class GltfResourceRuntimeCache
{
    private readonly ReadOnlyMemory<byte>? _glbBinaryChunk;
    private readonly GltfResourceResolver? _resolver;
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _bufferCache = new();
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _bufferViewCache = new();
    private readonly Lock _gate = new();

    /// <summary>Creates a cache.</summary>
    public GltfResourceRuntimeCache(
        ReadOnlyMemory<byte>? glbBinaryChunk,
        GltfResourceResolver? resolver)
    {
        _glbBinaryChunk = glbBinaryChunk;
        _resolver = resolver;
    }

    /// <summary>Resolves a buffer to bytes.</summary>
    public async ValueTask<ReadOnlyMemory<byte>> ResolveBufferAsync(
        int bufferIndex,
        GltfRawModel.Buffer bufferDef,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bufferDef);
        lock (_gate)
        {
            if (_bufferCache.TryGetValue(bufferIndex, out ReadOnlyMemory<byte> cached))
            {
                return cached;
            }
        }

        ReadOnlyMemory<byte> resolved;

        if (string.IsNullOrEmpty(bufferDef.Uri))
        {
            if (bufferIndex == 0 && _glbBinaryChunk.HasValue)
            {
                resolved = _glbBinaryChunk.Value.Slice(0, Math.Min(bufferDef.ByteLength, _glbBinaryChunk.Value.Length));
            }
            else
            {
                ThrowHelper.ThrowResource($"[buffer:{bufferIndex}]", "buffer", "GLB BIN chunk missing or requested buffer out of range.");
                resolved = default;
            }
        }
        else if (bufferDef.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            resolved = DecodeDataUri(bufferDef.Uri);
        }
        else
        {
            if (_resolver == null)
            {
                ThrowHelper.ThrowResource(bufferDef.Uri, "buffer", "External resource resolver callback not attached.");
            }

            var request = new GltfResourceRequest(bufferDef.Uri, null, "buffer", "application/octet-stream");
            GltfResolvedResource? result = await _resolver!(request, cancellationToken).ConfigureAwait(false);

            if (result == null || result.Value.Data.IsEmpty)
            {
                ThrowHelper.ThrowResource(bufferDef.Uri, "buffer", "Unable to resolve external binary URI.");
            }

            resolved = result.Value.Data;
        }

        lock (_gate)
        {
            _bufferCache[bufferIndex] = resolved;
        }

        return resolved;
    }

    /// <summary>Resolves an external image URI through the configured resolver.</summary>
    public async ValueTask<ReadOnlyMemory<byte>?> TryResolveImageAsync(
        string uri,
        string? mimeType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (_resolver == null)
        {
            return null;
        }

        var request = new GltfResourceRequest(uri, null, "image", mimeType);
        GltfResolvedResource? result = await _resolver(request, cancellationToken).ConfigureAwait(false);
        return result.HasValue && !result.Value.Data.IsEmpty ? result.Value.Data : null;
    }

    /// <summary>Resolves a bufferView to bytes, decompressing meshopt when linked.</summary>
    public async ValueTask<ReadOnlyMemory<byte>> ResolveBufferViewAsync<TMeshoptPolicy>(
        int bufferViewIndex,
        GltfRawModel rawModel,
        CancellationToken cancellationToken)
        where TMeshoptPolicy : struct, IMeshoptCompressionPolicy
    {
        ArgumentNullException.ThrowIfNull(rawModel);
        lock (_gate)
        {
            if (_bufferViewCache.TryGetValue(bufferViewIndex, out ReadOnlyMemory<byte> cached))
            {
                return cached;
            }
        }

        GltfRawModel.BufferView bv = rawModel.BufferViews[bufferViewIndex];
        ReadOnlyMemory<byte> result;

        if (bv.Meshopt != null)
        {
            ReadOnlyMemory<byte> raw = await ResolveBufferAsync(bv.Meshopt.Buffer, rawModel.Buffers[bv.Meshopt.Buffer], cancellationToken).ConfigureAwait(false);
            int uncompressedSize = bv.Meshopt.Count * bv.Meshopt.ByteStride;
            byte[] decompressed = GC.AllocateUninitializedArray<byte>(uncompressedSize, pinned: true);

            bool ok;
            {
                ReadOnlySpan<byte> compressed = raw.Span.Slice(bv.Meshopt.ByteOffset, bv.Meshopt.ByteLength);
                ok = TMeshoptPolicy.IsSupported
                    && TMeshoptPolicy.TryDecompressBufferView(compressed, bv.Meshopt.Count, bv.Meshopt.ByteStride, bv.Meshopt.Mode.AsSpan(), bv.Meshopt.Filter.AsSpan(), decompressed);
            }

            if (!ok)
            {
                ThrowHelper.ThrowContainer($"EXT_meshopt_compression decompression failed for bufferView [{bufferViewIndex}].");
            }

            result = decompressed;
        }
        else
        {
            ReadOnlyMemory<byte> raw = await ResolveBufferAsync(bv.Buffer, rawModel.Buffers[bv.Buffer], cancellationToken).ConfigureAwait(false);
            result = raw.Slice(bv.ByteOffset, bv.ByteLength);
        }

        lock (_gate)
        {
            _bufferViewCache[bufferViewIndex] = result;
        }

        return result;
    }

    /// <summary>Extracts the media type from a data: URI, if present.</summary>
    [SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "A data URI media type is not itself a URI.")]
    public static string? GetDataUriMimeType(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ReadOnlySpan<char> span = uri.AsSpan();
        if (!span.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int commaIndex = span.IndexOf(',');
        if (commaIndex < 5)
        {
            return null;
        }

        ReadOnlySpan<char> metadata = span.Slice(5, commaIndex - 5);
        int semicolonIndex = metadata.IndexOf(';');
        ReadOnlySpan<char> mimeType = semicolonIndex >= 0 ? metadata.Slice(0, semicolonIndex) : metadata;
        return mimeType.Length > 0 ? mimeType.ToString() : null;
    }

    /// <summary>Decodes a data: URI payload.</summary>
    public static ReadOnlyMemory<byte> DecodeDataUri(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        int commaIndex = uri.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex < 0)
        {
            ThrowHelper.ThrowContainer("Malformed glTF data URI.");
        }

        ReadOnlySpan<char> meta = uri.AsSpan(5, commaIndex - 5);
        ReadOnlySpan<char> data = uri.AsSpan(commaIndex + 1);

        if (meta.EndsWith("base64", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.FromBase64String(data.ToString());
        }
        else
        {
            return Encoding.UTF8.GetBytes(Uri.UnescapeDataString(data.ToString()));
        }
    }
}
