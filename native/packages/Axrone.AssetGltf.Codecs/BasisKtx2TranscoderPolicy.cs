using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Axrone.AssetGltf;
using BasisUniversal;

namespace Axrone.AssetGltf.Codecs;

/// <summary>Basis Universal transcoder policy for KTX2 textures.</summary>
public readonly struct BasisKtx2TranscoderPolicy : ITextureTranscoderPolicy
{
    /// <inheritdoc/>
    public static string PolicyId => "BasisUniversal";

    /// <inheritdoc/>
    public static bool CanTranscode(
        ReadOnlySpan<byte> sourceBytes,
        GltfTextureFormat source,
        GltfTextureFormat target) =>
        TryMapTarget(target, out _) && Ktx2Decoder.TryParse(sourceBytes.ToArray(), out _, out _);

    /// <inheritdoc/>
    public static bool TryTranscode(
        ReadOnlyMemory<byte> sourceBytes,
        GltfTextureFormat source,
        GltfTextureFormat target,
        [NotNullWhen(true)] out GltfTranscodeOutput? output,
        out string? failureReason)
    {
        output = null;
        failureReason = null;

        if (!TryMapTarget(target, out TranscoderTextureFormat targetFormat))
        {
            failureReason = $"Basis Universal does not support target format '{target}'.";
            return false;
        }

        try
        {
            using BasisKtx2Texture texture = BasisKtx2Texture.Open(sourceBytes.Span);
            var levels = new List<GltfTextureMipLevel>(texture.Info.Levels);
            using var stream = new MemoryStream();

            for (int level = 0; level < texture.Info.Levels; level++)
            {
                TranscodedImage image = texture.TranscodeImageLevel(
                    targetFormat,
                    level,
                    0,
                    0,
                    BasisDecodeFlags.None);
                int byteOffset = checked((int)stream.Position);
                stream.Write(image.Span);
                BasisKtx2LevelInfo info = texture.GetLevelInfo(level, 0, 0);
                levels.Add(new GltfTextureMipLevel(
                    level,
                    info.ActualWidth,
                    info.ActualHeight,
                    byteOffset,
                    image.SizeInBytes));
            }

            var payload = new GltfTranscodedTexturePayload(
                stream.ToArray(),
                target,
                levels.ToImmutableArray());
            output = new GltfTranscodeOutput(payload, target);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            failureReason = exception.Message;
            return false;
        }
    }

    private static bool TryMapTarget(GltfTextureFormat target, out TranscoderTextureFormat format)
    {
        format = target switch
        {
            GltfTextureFormat.BC1_RGB => TranscoderTextureFormat.Bc1Rgb,
            GltfTextureFormat.BC3_RGBA => TranscoderTextureFormat.Bc3Rgba,
            GltfTextureFormat.BC4_R => TranscoderTextureFormat.Bc4R,
            GltfTextureFormat.BC5_RG => TranscoderTextureFormat.Bc5Rg,
            GltfTextureFormat.BC7_RGBA => TranscoderTextureFormat.Bc7Rgba,
            GltfTextureFormat.ASTC_4x4 => TranscoderTextureFormat.AstcLdr4x4Rgba,
            GltfTextureFormat.ASTC_5x4 => TranscoderTextureFormat.AstcLdr5x4Rgba,
            GltfTextureFormat.ASTC_5x5 => TranscoderTextureFormat.AstcLdr5x5Rgba,
            GltfTextureFormat.ASTC_6x5 => TranscoderTextureFormat.AstcLdr6x5Rgba,
            GltfTextureFormat.ASTC_6x6 => TranscoderTextureFormat.AstcLdr6x6Rgba,
            GltfTextureFormat.ASTC_8x5 => TranscoderTextureFormat.AstcLdr8x5Rgba,
            GltfTextureFormat.ASTC_8x6 => TranscoderTextureFormat.AstcLdr8x6Rgba,
            GltfTextureFormat.ASTC_8x8 => TranscoderTextureFormat.AstcLdr8x8Rgba,
            GltfTextureFormat.ASTC_10x5 => TranscoderTextureFormat.AstcLdr10x5Rgba,
            GltfTextureFormat.ASTC_10x6 => TranscoderTextureFormat.AstcLdr10x6Rgba,
            GltfTextureFormat.ASTC_10x8 => TranscoderTextureFormat.AstcLdr10x8Rgba,
            GltfTextureFormat.ASTC_10x10 => TranscoderTextureFormat.AstcLdr10x10Rgba,
            GltfTextureFormat.ASTC_12x10 => TranscoderTextureFormat.AstcLdr12x10Rgba,
            GltfTextureFormat.ASTC_12x12 => TranscoderTextureFormat.AstcLdr12x12Rgba,
            GltfTextureFormat.R8G8B8A8_UNorm => TranscoderTextureFormat.Rgba32,
            _ => default,
        };

        return target is not GltfTextureFormat.Unknown &&
            format != default;
    }
}
