namespace Axrone.AssetGltf;

/// <summary>Decoded Draco primitive: attributes keyed by Draco unique id.</summary>
public sealed class GltfDracoDecodedMesh
{
    /// <summary>Decoded vertex count.</summary>
    public required int VertexCount { get; init; }

    /// <summary>Attribute values keyed by Draco unique id, packed as floats.</summary>
    public required ImmutableDictionary<int, float[]> Attributes { get; init; }

    /// <summary>Decoded triangle indices.</summary>
    public required ImmutableArray<uint> Indices { get; init; }
}

/// <summary>Draco mesh decompression policy (monomorphized per importer).</summary>
public interface IDracoMeshPolicy
{
    /// <summary>Whether a decoder is linked.</summary>
    static abstract bool IsSupported { get; }

    /// <summary>Decodes one Draco mesh into attributes and indices.</summary>
    static abstract bool TryDecodeMesh(
        ReadOnlySpan<byte> dracoBytes,
        [NotNullWhen(true)] out GltfDracoDecodedMesh? mesh);
}

/// <summary>Null Draco policy: clean rejection when no decoder is linked.</summary>
public readonly struct NoDracoCompressionPolicy : IDracoMeshPolicy
{
    /// <inheritdoc/>
    public static bool IsSupported => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecodeMesh(
        ReadOnlySpan<byte> dracoBytes,
        [NotNullWhen(true)] out GltfDracoDecodedMesh? mesh)
    {
        mesh = null;
        return false;
    }
}

/// <summary>Meshopt bufferView decompression policy.</summary>
public interface IMeshoptCompressionPolicy
{
    /// <summary>Whether a decoder is linked.</summary>
    static abstract bool IsSupported { get; }

    /// <summary>Tries to decompress a meshopt bufferView.</summary>
    static abstract bool TryDecompressBufferView(
        ReadOnlySpan<byte> source,
        int count,
        int byteStride,
        ReadOnlySpan<char> mode,
        ReadOnlySpan<char> filter,
        Span<byte> destination);
}

/// <summary>Null meshopt policy: clean rejection when no decoder is linked.</summary>
public readonly struct NoMeshoptCompressionPolicy : IMeshoptCompressionPolicy
{
    /// <inheritdoc/>
    public static bool IsSupported => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecompressBufferView(
        ReadOnlySpan<byte> source,
        int count,
        int byteStride,
        ReadOnlySpan<char> mode,
        ReadOnlySpan<char> filter,
        Span<byte> destination) => false;
}

/// <summary>Texture transcode policy (KTX2/Basis to runtime format).</summary>
public interface ITextureTranscoderPolicy
{
    /// <summary>Policy identity for diagnostics.</summary>
    static abstract string PolicyId { get; }

    /// <summary>Whether a source-to-target transcode is supported.</summary>
    static abstract bool CanTranscode(
        ReadOnlySpan<byte> sourceBytes,
        GltfTextureFormat source,
        GltfTextureFormat target);

    /// <summary>Tries to transcode texture bytes.</summary>
    static abstract bool TryTranscode(
        ReadOnlyMemory<byte> sourceBytes,
        GltfTextureFormat source,
        GltfTextureFormat target,
        [NotNullWhen(true)] out GltfTranscodeOutput? output,
        out string? failureReason);
}

/// <summary>Successful texture transcode output.</summary>
public readonly record struct GltfTranscodeOutput(
    GltfTexturePayload Payload,
    GltfTextureFormat Format);

/// <summary>Passthrough policy: only identity transcodes, zero work.</summary>
public readonly struct PassthroughTranscoderPolicy : ITextureTranscoderPolicy
{
    /// <inheritdoc/>
    public static string PolicyId => "Passthrough";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanTranscode(
        ReadOnlySpan<byte> sourceBytes,
        GltfTextureFormat source,
        GltfTextureFormat target) => source == target;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryTranscode(
        ReadOnlyMemory<byte> sourceBytes,
        GltfTextureFormat source,
        GltfTextureFormat target,
        [NotNullWhen(true)] out GltfTranscodeOutput? output,
        out string? failureReason)
    {
        output = new GltfTranscodeOutput(
            new GltfRawTexturePayload(sourceBytes, "application/octet-stream"),
            source);
        failureReason = null;
        return true;
    }
}
