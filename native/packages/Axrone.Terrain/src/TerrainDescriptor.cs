namespace Axrone.Terrain;

/// <summary>
/// World-space description of a terrain: footprint size, height scale and vertex
/// grid resolution. The heightmap itself stores normalized <c>[0, 1]</c> values;
/// <see cref="MaxHeight"/> converts them to world units.
/// </summary>
/// <param name="Width">World-space size along the X axis.</param>
/// <param name="Length">World-space size along the Z axis.</param>
/// <param name="MaxHeight">World-space height applied to a normalized height of 1.</param>
/// <param name="Resolution">Vertex count per side of the height grid.</param>
public readonly record struct TerrainDescriptor(
    float Width,
    float Length,
    float MaxHeight,
    int Resolution)
{
    /// <summary>
    /// Validates the descriptor. Callers construct descriptors freely and validate
    /// at the boundary, matching the TypeScript engine contract.
    /// </summary>
    /// <exception cref="TerrainException">
    /// A dimension is non-finite or negative, or the resolution is unsupported.
    /// </exception>
    public void Validate()
    {
        if (!float.IsFinite(Width) || Width <= 0f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid width: {Width}"),
                TerrainErrorCode.InvalidDimensions);
        }

        if (!float.IsFinite(Length) || Length <= 0f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid length: {Length}"),
                TerrainErrorCode.InvalidDimensions);
        }

        if (!float.IsFinite(MaxHeight) || MaxHeight < 0f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid maxHeight: {MaxHeight}"),
                TerrainErrorCode.InvalidDimensions);
        }

        if (!TerrainLimits.IsResolution(Resolution))
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Invalid resolution: {Resolution}. Expected one of 33, 65, 129, 257."),
                TerrainErrorCode.InvalidResolution);
        }
    }
}
