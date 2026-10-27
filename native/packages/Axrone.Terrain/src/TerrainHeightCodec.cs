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

    /// <summary>Parses a payload produced by <see cref="Encode(ReadOnlySpan{float})"/>.</summary>
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

    /// <summary>Serializes world-space heights normalized into an explicit range.</summary>
    /// <param name="heights">World-space heights.</param>
    /// <param name="minHeight">World height mapped to zero.</param>
    /// <param name="maxHeight">World height mapped to one.</param>
    /// <returns>A payload beginning with <c>athf1:</c>.</returns>
    public static string Encode(ReadOnlySpan<float> heights, float minHeight, float maxHeight)
    {
        float range = MathF.Max(maxHeight - minHeight, 1e-5f);
        var normalized = new float[heights.Length];
        for (var index = 0; index < heights.Length; index++)
        {
            normalized[index] = Math.Clamp((heights[index] - minHeight) / range, 0f, 1f);
        }

        return Encode(normalized);
    }

    /// <summary>Parses a world-space payload into a destination span.</summary>
    /// <param name="value">Versioned Base64 payload.</param>
    /// <param name="minHeight">World height mapped to zero.</param>
    /// <param name="maxHeight">World height mapped to one.</param>
    /// <param name="destination">Destination span.</param>
    /// <exception cref="TerrainException">The prefix, Base64 or length is invalid.</exception>
    public static void Decode(string value, float minHeight, float maxHeight, Span<float> destination)
    {
        ArgumentNullException.ThrowIfNull(value);

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

        if (bytes.Length != destination.Length * 2)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Height data payload length {bytes.Length} does not match destination {destination.Length} (expected {destination.Length * 2} bytes)."),
                TerrainErrorCode.HeightmapSizeMismatch);
        }

        float range = maxHeight - minHeight;
        for (var index = 0; index < destination.Length; index++)
        {
            int quantized = bytes[index * 2] | (bytes[(index * 2) + 1] << 8);
            destination[index] = minHeight + ((quantized / (float)QuantizationMax) * range);
        }
    }

    /// <summary>Cheap shape check usable by editors before attempting a full decode.</summary>
    /// <param name="value">Candidate payload.</param>
    /// <returns><see langword="true"/> when the value starts with the height prefix.</returns>
    public static bool IsPayload(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);
}
