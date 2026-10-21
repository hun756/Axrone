namespace Axrone.AssetGltf;

/// <summary>Draco mesh decompression policy (monomorphized per importer).</summary>
public interface IDracoMeshPolicy
{
    /// <summary>Whether a decoder is linked.</summary>
    static abstract bool IsSupported { get; }

    /// <summary>Tries to decode one primitive attribute.</summary>
    static abstract bool TryDecodePrimitive(
        ReadOnlySpan<byte> dracoBytes,
        GltfMeshSemantic semantic,
        int componentType,
        Span<byte> destination,
        out int decodedVertexCount);
}

/// <summary>Null Draco policy: clean rejection when no decoder is linked.</summary>
public readonly struct NoDracoCompressionPolicy : IDracoMeshPolicy
{
    /// <inheritdoc/>
    public static bool IsSupported => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDecodePrimitive(
        ReadOnlySpan<byte> dracoBytes,
        GltfMeshSemantic semantic,
        int componentType,
        Span<byte> destination,
        out int decodedVertexCount)
    {
        decodedVertexCount = 0;
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
        Span<byte> destination) => false;
}

/// <summary>Texture transcode policy (KTX2/Basis to runtime format).</summary>
public interface ITextureTranscoderPolicy
{
    /// <summary>Policy identity for diagnostics.</summary>
    static abstract string PolicyId { get; }

    /// <summary>Whether a source-to-target transcode is supported.</summary>
    static abstract bool CanTranscode(GltfTextureFormat source, GltfTextureFormat target);

    /// <summary>Tries to transcode texture bytes.</summary>
    static abstract bool TryTranscode(
        ReadOnlyMemory<byte> sourceBytes,
        GltfTextureFormat source,
        GltfTextureFormat target,
        out ReadOnlyMemory<byte> transcodedBytes,
        out string? failureReason);
}

/// <summary>Passthrough policy: only identity transcodes, zero work.</summary>
public readonly struct PassthroughTranscoderPolicy : ITextureTranscoderPolicy
{
    /// <inheritdoc/>
    public static string PolicyId => "Passthrough";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanTranscode(GltfTextureFormat source, GltfTextureFormat target) => source == target;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryTranscode(
        ReadOnlyMemory<byte> sourceBytes,
        GltfTextureFormat source,
        GltfTextureFormat target,
        out ReadOnlyMemory<byte> transcodedBytes,
        out string? failureReason)
    {
        transcodedBytes = sourceBytes;
        failureReason = null;
        return true;
    }
}
