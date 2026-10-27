namespace Axrone.Terrain;

/// <summary>
/// Compact, environment-agnostic persistence codec for sculpted heightmaps.
/// Normalized heights are quantized to unsigned 16-bit (1/65535 precision) and
/// Base64-encoded behind a version prefix so future layout changes stay
/// detectable.
/// </summary>
public static class TerrainHeightCodec
{
    private const string Prefix = "athf1:";
    private const int QuantizationMax = 0xFFFF;

    /// <summary>Serializes normalized heights into a versioned Base64 payload.</summary>
    /// <param name="heights">Normalized heights; values outside <c>[0, 1]</c> are clamped.</param>
    /// <returns>A payload beginning with <c>athf1:</c>.</returns>
    public static string Encode(ReadOnlySpan<float> heights)
    {
        var bytes = new byte[heights.Length * 2];
        for (var index = 0; index < heights.Length; index++)
        {
            float clamped = TerrainMath.Clamp01(heights[index]);
            int quantized = Math.Clamp(TerrainMath.RoundHalfUp(clamped * QuantizationMax), 0, QuantizationMax);
            bytes[index * 2] = (byte)(quantized & 0xFF);
            bytes[(index * 2) + 1] = (byte)((quantized >> 8) & 0xFF);
        }

        return string.Concat(Prefix, Base64Helper.Encode(bytes));
    }

    /// <summary>Parses a payload produced by <see cref="Encode"/>.</summary>
    /// <param name="value">Versioned Base64 payload.</param>
    /// <param name="resolution">Expected height-grid resolution.</param>
    /// <returns>Decoded normalized heights.</returns>
    /// <exception cref="TerrainException">The resolution, prefix, Base64 or length is invalid.</exception>
    public static float[] Decode(string value, int resolution)
    {
        ArgumentNullException.ThrowIfNull(value);
        TerrainLimits.AssertResolution(resolution);

        if (!IsPayload(value))
        {
            throw new TerrainException(
                "Height data payload is missing the expected version prefix.",
                TerrainErrorCode.SourceDecodeFailed);
        }

        byte[] bytes;
        try
        {
            bytes = Base64Helper.Decode(value[Prefix.Length..]);
        }
        catch (FormatException exception)
        {
            throw new TerrainException(
                "Payload contains an invalid base64 character.",
                TerrainErrorCode.SourceDecodeFailed,
                exception);
        }

        int expectedLength = resolution * resolution;
        if (bytes.Length != expectedLength * 2)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Height data payload length {bytes.Length} does not match resolution {resolution} (expected {expectedLength * 2} bytes)."),
                TerrainErrorCode.HeightmapSizeMismatch);
        }

        var heights = new float[expectedLength];
        for (var index = 0; index < expectedLength; index++)
        {
            int quantized = bytes[index * 2] | (bytes[(index * 2) + 1] << 8);
            heights[index] = quantized / (float)QuantizationMax;
        }

        return heights;
    }

    /// <summary>Cheap shape check usable by editors before attempting a full decode.</summary>
    /// <param name="value">Candidate payload.</param>
    /// <returns><see langword="true"/> when the value starts with the height prefix.</returns>
    public static bool IsPayload(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);
}
