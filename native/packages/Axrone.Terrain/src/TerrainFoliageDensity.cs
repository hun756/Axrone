namespace Axrone.Terrain;

/// <summary>
/// Foliage density maps: single-channel byte buffers (one per foliage layer)
/// painted with the shared terrain brush. 255 means the layer's full density
/// multiplier applies at that texel; 0 means no instances. The splat resolution
/// set is reused so paint and foliage tooling stay symmetric.
/// </summary>
public static class TerrainFoliageDensity
{
    private const string Prefix = "atfl1:";

    /// <summary>Creates an empty (all-zero) density buffer.</summary>
    /// <param name="resolution">Density resolution; one of 64, 128 or 256.</param>
    /// <returns>A buffer of <c>resolution * resolution</c> bytes.</returns>
    /// <exception cref="TerrainException">The resolution is unsupported.</exception>
    public static byte[] CreateBuffer(int resolution)
    {
        TerrainLimits.AssertSplatResolution(resolution);
        return new byte[resolution * resolution];
    }

    /// <summary>Paints one density stamp in place.</summary>
    /// <param name="density">Single-channel density buffer.</param>
    /// <param name="resolution">Density resolution; one of 64, 128 or 256.</param>
    /// <param name="descriptor">Terrain footprint and resolution.</param>
    /// <param name="brush">Resolved brush parameters.</param>
    /// <param name="localX">Stamp center X in terrain-local space.</param>
    /// <param name="localZ">Stamp center Z in terrain-local space.</param>
    /// <param name="erase"><see langword="true"/> removes density instead of adding it.</param>
    /// <returns><see langword="true"/> when at least one texel changed.</returns>
    /// <exception cref="TerrainException">A parameter or buffer is invalid.</exception>
    public static bool ApplyStamp(
        Span<byte> density,
        int resolution,
        in TerrainDescriptor descriptor,
        in TerrainBrushOptions brush,
        float localX,
        float localZ,
        bool erase = false)
    {
        descriptor.Validate();
        brush.Validate();
        TerrainLimits.AssertSplatResolution(resolution);
        AssertBuffer(density, resolution);

        int lastTexel = resolution - 1;
        float texelSizeX = descriptor.Width / lastTexel;
        float texelSizeZ = descriptor.Length / lastTexel;

        float texelCenterX = (localX + (descriptor.Width * 0.5f)) / texelSizeX;
        float texelCenterZ = (localZ + (descriptor.Length * 0.5f)) / texelSizeZ;
        float texelRadiusX = brush.Radius / texelSizeX;
        float texelRadiusZ = brush.Radius / texelSizeZ;

        int minX = Math.Max(0, (int)MathF.Floor(texelCenterX - texelRadiusX));
        int maxX = Math.Min(lastTexel, (int)MathF.Ceiling(texelCenterX + texelRadiusX));
        int minZ = Math.Max(0, (int)MathF.Floor(texelCenterZ - texelRadiusZ));
        int maxZ = Math.Min(lastTexel, (int)MathF.Ceiling(texelCenterZ + texelRadiusZ));
        if (minX > maxX || minZ > maxZ)
        {
            return false;
        }

        float stampScale = brush.Strength * TerrainLimits.FoliageDensityStampScale;
        var changed = false;

        for (var texelZ = minZ; texelZ <= maxZ; texelZ++)
        {
            for (var texelX = minX; texelX <= maxX; texelX++)
            {
                float worldDx = (texelX - texelCenterX) * texelSizeX;
                float worldDz = (texelZ - texelCenterZ) * texelSizeZ;
                float distance = MathF.Sqrt((worldDx * worldDx) + (worldDz * worldDz));
                if (distance > brush.Radius)
                {
                    continue;
                }

                float weight = MathF.Pow(TerrainMath.Smoothstep(1f - (distance / brush.Radius)), brush.Falloff);
                if (weight <= 0f)
                {
                    continue;
                }

                int index = (texelZ * resolution) + texelX;
                int current = density[index];
                int target = erase
                    ? Math.Max(0, TerrainMath.RoundHalfUp(current - (255f * stampScale * weight)))
                    : Math.Min(255, TerrainMath.RoundHalfUp(current + ((255 - current) * stampScale * weight)));
                if (target != current)
                {
                    density[index] = (byte)target;
                    changed = true;
                }
            }
        }

        return changed;
    }

    /// <summary>Bilinear density sample at normalized <c>[0, 1]</c> coordinates.</summary>
    /// <param name="density">Single-channel density buffer.</param>
    /// <param name="resolution">Density resolution; one of 64, 128 or 256.</param>
    /// <param name="u">Horizontal coordinate; clamped into <c>[0, 1]</c>.</param>
    /// <param name="v">Vertical coordinate; clamped into <c>[0, 1]</c>.</param>
    /// <returns>Density in <c>[0, 1]</c>.</returns>
    /// <exception cref="TerrainException">The resolution or buffer is invalid.</exception>
    public static float Sample(ReadOnlySpan<byte> density, int resolution, float u, float v)
    {
        TerrainLimits.AssertSplatResolution(resolution);
        AssertBuffer(density, resolution);
        return TerrainMath.BilinearByte(density, resolution, u, v);
    }

    /// <summary>Serializes a density buffer into a versioned Base64 payload.</summary>
    /// <param name="density">Single-channel density buffer.</param>
    /// <returns>A payload beginning with <c>atfl1:</c>.</returns>
    public static string Encode(ReadOnlySpan<byte> density) => string.Concat(Prefix, Base64Helper.Encode(density));

    /// <summary>Parses a payload produced by <see cref="Encode"/>.</summary>
    /// <param name="value">Versioned Base64 payload.</param>
    /// <param name="resolution">Expected density resolution.</param>
    /// <returns>The decoded density buffer.</returns>
    /// <exception cref="TerrainException">The resolution, prefix, Base64 or length is invalid.</exception>
    public static byte[] Decode(string value, int resolution)
    {
        ArgumentNullException.ThrowIfNull(value);
        TerrainLimits.AssertSplatResolution(resolution);

        if (!IsPayload(value))
        {
            throw new TerrainException(
                "Foliage density payload is missing the expected version prefix.",
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
        if (bytes.Length != expectedLength)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Foliage density payload length {bytes.Length} does not match resolution {resolution} (expected {expectedLength} bytes)."),
                TerrainErrorCode.SplatSizeMismatch);
        }

        return bytes;
    }

    /// <summary>Cheap shape check usable by editors before attempting a full decode.</summary>
    /// <param name="value">Candidate payload.</param>
    /// <returns><see langword="true"/> when the value starts with the density prefix.</returns>
    public static bool IsPayload(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    private static void AssertBuffer(ReadOnlySpan<byte> density, int resolution)
    {
        int expectedLength = resolution * resolution;
        if (density.Length != expectedLength)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Density buffer length {density.Length} does not match resolution {resolution} (expected {expectedLength})."),
                TerrainErrorCode.SplatSizeMismatch);
        }
    }
}
