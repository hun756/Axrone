namespace Axrone.Terrain;

/// <summary>Configuration for <see cref="TerrainChunkEngine"/>.</summary>
public sealed class TerrainChunkEngineOptions
{
    /// <summary>Gets the continuous height field used to generate chunks.</summary>
    public required ITerrainSurface Surface { get; init; }

    /// <summary>Gets the world-space size of one grid cell. Defaults to 1.</summary>
    public float CellSize { get; init; } = 1f;

    /// <summary>Gets the world-space height applied to a normalized height of 1. Defaults to 1.</summary>
    public float MaxHeight { get; init; } = 1f;

    /// <summary>Gets the number of chunk slots the pool reserves. Defaults to 256.</summary>
    public int PoolCapacity { get; init; } = 256;

    /// <summary>Validates the options.</summary>
    /// <exception cref="TerrainException">A value is outside its documented domain.</exception>
    public void Validate()
    {
        if (Surface is null)
        {
            throw new TerrainException("A terrain surface is required.", TerrainErrorCode.ValidationFailed);
        }

        if (!float.IsFinite(CellSize) || CellSize <= 0f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid cell size: {CellSize}."),
                TerrainErrorCode.ValidationFailed);
        }

        if (!float.IsFinite(MaxHeight) || MaxHeight < 0f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid max height: {MaxHeight}."),
                TerrainErrorCode.ValidationFailed);
        }

        if (PoolCapacity <= 0)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid pool capacity: {PoolCapacity}."),
                TerrainErrorCode.ValidationFailed);
        }
    }
}
