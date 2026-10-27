namespace Axrone.Terrain;

/// <summary>Sculpt brush modes.</summary>
public enum TerrainBrushKind
{
    /// <summary>Raises heights inside the brush footprint.</summary>
    Raise,

    /// <summary>Lowers heights inside the brush footprint.</summary>
    Lower,

    /// <summary>Converges heights toward the neighborhood mean.</summary>
    Smooth,

    /// <summary>Pulls heights toward the height sampled at the stamp center.</summary>
    Flatten,
}

/// <summary>
/// Resolved brush parameters shared by height sculpting, splat painting and
/// foliage density painting so every tool uses the same footprint and falloff.
/// </summary>
public readonly record struct TerrainBrushOptions
{
    /// <summary>Initializes the documented default brush.</summary>
    public TerrainBrushOptions()
    {
    }

    /// <summary>Gets the brush mode. Defaults to <see cref="TerrainBrushKind.Raise"/>.</summary>
    public TerrainBrushKind Kind { get; init; } = TerrainBrushKind.Raise;

    /// <summary>Gets the world-space brush radius. Defaults to 8.</summary>
    public float Radius { get; init; } = 8f;

    /// <summary>Gets the normalized stamp intensity in <c>(0, 1]</c>. Defaults to 0.5.</summary>
    public float Strength { get; init; } = 0.5f;

    /// <summary>Gets the falloff exponent; 1 is smoothstep. Defaults to 1.</summary>
    public float Falloff { get; init; } = 1f;

    /// <summary>Gets the normalized flatten target in <c>[0, 1]</c>. Defaults to 0.</summary>
    public float FlattenTarget { get; init; }

    /// <summary>Validates the brush parameters.</summary>
    /// <exception cref="TerrainException">A parameter is outside its documented domain.</exception>
    public void Validate()
    {
        if (Kind is < TerrainBrushKind.Raise or > TerrainBrushKind.Flatten)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid brush kind: {Kind}"),
                TerrainErrorCode.ValidationFailed);
        }

        if (!float.IsFinite(Radius) || Radius <= 0f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid brush radius: {Radius}"),
                TerrainErrorCode.ValidationFailed);
        }

        if (!float.IsFinite(Strength) || Strength <= 0f || Strength > 1f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid brush strength: {Strength}. Expected a value in (0, 1]."),
                TerrainErrorCode.ValidationFailed);
        }

        if (!float.IsFinite(Falloff) || Falloff <= 0f || Falloff > 8f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid brush falloff: {Falloff}. Expected a value in (0, 8]."),
                TerrainErrorCode.ValidationFailed);
        }

        if (!float.IsFinite(FlattenTarget) || FlattenTarget < 0f || FlattenTarget > 1f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid flatten target: {FlattenTarget}. Expected a value in [0, 1]."),
                TerrainErrorCode.ValidationFailed);
        }
    }
}

/// <summary>
/// Parameters for the deterministic value-noise generator. A <see langword="default"/>
/// value resolves to the documented defaults (seed 1337, frequency 0.02, 4 octaves).
/// </summary>
public readonly record struct TerrainNoiseOptions
{
    /// <summary>Initializes the documented default noise parameters.</summary>
    public TerrainNoiseOptions()
    {
    }

    /// <summary>Gets the PRNG seed. Defaults to 1337.</summary>
    public ulong Seed { get; init; } = 1337;

    /// <summary>Gets the base sampling frequency. Defaults to 0.02.</summary>
    public float Frequency { get; init; } = 0.02f;

    /// <summary>Gets the octave count in <c>[1, 8]</c>. Defaults to 4.</summary>
    public int Octaves { get; init; } = 4;

    /// <summary>Gets the amplitude persistence in <c>(0, 1]</c>. Defaults to 0.5.</summary>
    public float Persistence { get; init; } = 0.5f;

    /// <summary>Gets the frequency lacunarity, at least 1. Defaults to 2.</summary>
    public float Lacunarity { get; init; } = 2f;

    /// <summary>Gets the world-space X offset. Defaults to 0.</summary>
    public float OffsetX { get; init; }

    /// <summary>Gets the world-space Z offset. Defaults to 0.</summary>
    public float OffsetZ { get; init; }

    /// <summary>Validates the noise parameters.</summary>
    /// <exception cref="TerrainException">A parameter is outside its documented domain.</exception>
    public void Validate()
    {
        if (!float.IsFinite(Frequency) || Frequency <= 0f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid noise frequency: {Frequency}"),
                TerrainErrorCode.ValidationFailed);
        }

        if (Octaves is < 1 or > 8)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid noise octaves: {Octaves}. Expected an integer in [1, 8]."),
                TerrainErrorCode.ValidationFailed);
        }

        if (!float.IsFinite(Persistence) || Persistence <= 0f || Persistence > 1f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid noise persistence: {Persistence}. Expected a value in (0, 1]."),
                TerrainErrorCode.ValidationFailed);
        }

        if (!float.IsFinite(Lacunarity) || Lacunarity < 1f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid noise lacunarity: {Lacunarity}. Expected a value >= 1."),
                TerrainErrorCode.ValidationFailed);
        }

        if (!float.IsFinite(OffsetX) || !float.IsFinite(OffsetZ))
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid noise offset: ({OffsetX}, {OffsetZ})"),
                TerrainErrorCode.ValidationFailed);
        }
    }
}
