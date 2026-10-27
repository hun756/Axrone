namespace Axrone.Terrain;

/// <summary>
/// Persistence codec for splat weight buffers: raw RGBA bytes, Base64-encoded
/// behind a version prefix that mirrors the sculpted-height format.
/// </summary>
public static class TerrainSplatCodec
{
    private const string Prefix = "atsp1:";

    /// <summary>Serializes an RGBA weight buffer into a versioned Base64 payload.</summary>
    /// <param name="splat">RGBA weight buffer.</param>
    /// <returns>A payload beginning with <c>atsp1:</c>.</returns>
    public static string Encode(ReadOnlySpan<byte> splat) => string.Concat(Prefix, Base64Helper.Encode(splat));

    /// <summary>Parses a payload produced by <see cref="Encode"/>.</summary>
    /// <param name="value">Versioned Base64 payload.</param>
    /// <param name="resolution">Expected splat resolution.</param>
    /// <returns>The decoded RGBA weight buffer.</returns>
    /// <exception cref="TerrainException">The resolution, prefix, Base64 or length is invalid.</exception>
    public static byte[] Decode(string value, int resolution)
    {
        ArgumentNullException.ThrowIfNull(value);
        TerrainLimits.AssertSplatResolution(resolution);

        if (!IsPayload(value))
        {
            throw new TerrainException(
                "Splat data payload is missing the expected version prefix.",
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

        int expectedLength = resolution * resolution * 4;
        if (bytes.Length != expectedLength)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Splat data payload length {bytes.Length} does not match resolution {resolution} (expected {expectedLength} bytes)."),
                TerrainErrorCode.SplatSizeMismatch);
        }

        return bytes;
    }

    /// <summary>Cheap shape check usable by editors before attempting a full decode.</summary>
    /// <param name="value">Candidate payload.</param>
    /// <returns><see langword="true"/> when the value starts with the splat prefix.</returns>
    public static bool IsPayload(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);
}
