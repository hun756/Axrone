namespace Axrone.Terrain;

/// <summary>One splat layer definition. Up to four layers map to RGBA channels.</summary>
public readonly record struct TerrainLayer
{
    /// <summary>Initializes an empty layer definition.</summary>
    public TerrainLayer()
    {
    }

    /// <summary>Gets the stable layer id used by authored data.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the project-relative albedo texture path; empty until assigned.</summary>
    public string TextureAsset { get; init; } = string.Empty;

    /// <summary>Gets the world-space tiling repeat across the terrain footprint.</summary>
    public float Tiling { get; init; } = 1f;
}

/// <summary>One foliage layer definition.</summary>
public readonly record struct TerrainFoliageLayer
{
    /// <summary>Initializes a foliage layer with the documented defaults.</summary>
    public TerrainFoliageLayer()
    {
    }

    /// <summary>Gets the stable layer id used by authored data.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the project-relative mesh asset path; empty selects the built-in cross-quad card.</summary>
    public string MeshAsset { get; init; } = string.Empty;

    /// <summary>Gets the global density multiplier in <c>[0, 1]</c> applied on top of the painted map.</summary>
    public float Density { get; init; } = 0.5f;

    /// <summary>Gets the minimum instance scale.</summary>
    public float MinScale { get; init; } = 0.8f;

    /// <summary>Gets the maximum instance scale.</summary>
    public float MaxScale { get; init; } = 1.4f;

    /// <summary>Gets whether instances tilt toward the surface normal instead of world up.</summary>
    public bool AlignToNormal { get; init; }

    /// <summary>Gets the maximum slope in degrees on which instances may spawn.</summary>
    public float MaxSlopeDeg { get; init; } = 90f;
}

/// <summary>A scattered foliage instance in terrain-local space.</summary>
/// <param name="X">Local X on the origin-centered grid.</param>
/// <param name="Y">Local Y on the terrain surface.</param>
/// <param name="Z">Local Z on the origin-centered grid.</param>
/// <param name="RotationY">Yaw rotation in radians.</param>
/// <param name="Scale">Uniform instance scale.</param>
public readonly record struct TerrainFoliageInstance(
    float X,
    float Y,
    float Z,
    float RotationY,
    float Scale);

/// <summary>Validates authored layer definitions.</summary>
public static class TerrainValidation
{
    /// <summary>Validates splat layer definitions.</summary>
    /// <param name="layers">Layer definitions to validate.</param>
    /// <exception cref="TerrainException">The set is too large or a layer is invalid.</exception>
    public static void ValidateLayers(ReadOnlySpan<TerrainLayer> layers)
    {
        if (layers.Length > TerrainLimits.MaxLayers)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Terrain supports at most {TerrainLimits.MaxLayers} splat layers, received {layers.Length}."),
                TerrainErrorCode.ValidationFailed);
        }

        for (var index = 0; index < layers.Length; index++)
        {
            TerrainLayer layer = layers[index];
            if (string.IsNullOrEmpty(layer.Id))
            {
                throw new TerrainException(
                    FormattableString.Invariant($"Terrain layer at index {index} is missing a stable id."),
                    TerrainErrorCode.ValidationFailed);
            }

            if (!float.IsFinite(layer.Tiling) || layer.Tiling <= 0f)
            {
                throw new TerrainException(
                    FormattableString.Invariant(
                        $"Terrain layer '{layer.Id}' has an invalid tiling value: {layer.Tiling}."),
                    TerrainErrorCode.ValidationFailed);
            }
        }
    }

    /// <summary>Validates foliage layer definitions.</summary>
    /// <param name="layers">Layer definitions to validate.</param>
    /// <exception cref="TerrainException">The set is too large or a layer is invalid.</exception>
    public static void ValidateFoliageLayers(ReadOnlySpan<TerrainFoliageLayer> layers)
    {
        if (layers.Length > TerrainLimits.MaxFoliageLayers)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Terrain supports at most {TerrainLimits.MaxFoliageLayers} foliage layers, received {layers.Length}."),
                TerrainErrorCode.ValidationFailed);
        }

        for (var index = 0; index < layers.Length; index++)
        {
            TerrainFoliageLayer layer = layers[index];
            if (string.IsNullOrEmpty(layer.Id))
            {
                throw new TerrainException(
                    FormattableString.Invariant($"Foliage layer at index {index} is missing a stable id."),
                    TerrainErrorCode.ValidationFailed);
            }

            if (!float.IsFinite(layer.Density) || layer.Density < 0f || layer.Density > 1f)
            {
                throw new TerrainException(
                    FormattableString.Invariant(
                        $"Foliage layer '{layer.Id}' has an invalid density: {layer.Density}. Expected a value in [0, 1]."),
                    TerrainErrorCode.ValidationFailed);
            }

            if (!float.IsFinite(layer.MinScale) ||
                !float.IsFinite(layer.MaxScale) ||
                layer.MinScale <= 0f ||
                layer.MaxScale < layer.MinScale)
            {
                throw new TerrainException(
                    FormattableString.Invariant(
                        $"Foliage layer '{layer.Id}' has an invalid scale range: [{layer.MinScale}, {layer.MaxScale}]."),
                    TerrainErrorCode.ValidationFailed);
            }

            if (!float.IsFinite(layer.MaxSlopeDeg) || layer.MaxSlopeDeg < 0f || layer.MaxSlopeDeg > 90f)
            {
                throw new TerrainException(
                    FormattableString.Invariant(
                        $"Foliage layer '{layer.Id}' has an invalid max slope: {layer.MaxSlopeDeg}. Expected degrees in [0, 90]."),
                    TerrainErrorCode.ValidationFailed);
            }
        }
    }
}
