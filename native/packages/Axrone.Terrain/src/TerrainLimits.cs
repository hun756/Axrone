namespace Axrone.Terrain;

/// <summary>
/// Fixed limits and tuning constants shared by the terrain pipeline. These mirror
/// the TypeScript engine contract so authored terrain data stays portable.
/// </summary>
public static class TerrainLimits
{
    /// <summary>Maximum simultaneous splat layers (one per RGBA channel).</summary>
    public const int MaxLayers = 4;

    /// <summary>Maximum simultaneous foliage layers per terrain.</summary>
    public const int MaxFoliageLayers = 4;

    /// <summary>Hard cap keeping a single layer's batch mesh within editor budgets.</summary>
    public const int MaxFoliageInstances = 10_000;

    /// <summary>Maximum foliage candidate points evaluated per density texel.</summary>
    public const float FoliageCandidatesPerTexel = 0.35f;

    /// <summary>Height brush: strength 1 moves a sample by this fraction of full range per stamp.</summary>
    public const float BrushHeightStampScale = 0.04f;

    /// <summary>Splat paint: strength 1 at full weight approaches the target by this fraction per stamp.</summary>
    public const float SplatStampScale = 0.35f;

    /// <summary>Foliage density: strength 1 at full weight approaches the target by this fraction per stamp.</summary>
    public const float FoliageDensityStampScale = 0.45f;

    /// <summary>Raycast: bisection refinement iterations after a marching step crossing.</summary>
    public const int RaycastRefineSteps = 24;

    /// <summary>Raycast: default maximum march distance in world units.</summary>
    public const float RaycastDefaultMaxDistance = 10_000f;

    /// <summary>Supported vertex-grid resolutions per terrain side (power-of-two + 1).</summary>
    public static ReadOnlySpan<int> Resolutions => s_resolutions;

    /// <summary>Supported per-side splat weight-map resolutions.</summary>
    public static ReadOnlySpan<int> SplatResolutions => s_splatResolutions;

    private static readonly int[] s_resolutions = [33, 65, 129, 257];
    private static readonly int[] s_splatResolutions = [64, 128, 256];

    /// <summary>Returns whether <paramref name="value"/> is a supported height-grid resolution.</summary>
    /// <param name="value">Candidate resolution.</param>
    /// <returns><see langword="true"/> for 33, 65, 129 or 257.</returns>
    public static bool IsResolution(int value) => value is 33 or 65 or 129 or 257;

    /// <summary>Returns whether <paramref name="value"/> is a supported splat resolution.</summary>
    /// <param name="value">Candidate resolution.</param>
    /// <returns><see langword="true"/> for 64, 128 or 256.</returns>
    public static bool IsSplatResolution(int value) => value is 64 or 128 or 256;

    /// <summary>Throws when <paramref name="resolution"/> is not a supported height-grid resolution.</summary>
    /// <param name="resolution">Candidate resolution.</param>
    /// <exception cref="TerrainException">The resolution is unsupported.</exception>
    public static void AssertResolution(int resolution)
    {
        if (!IsResolution(resolution))
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid resolution: {resolution}."),
                TerrainErrorCode.InvalidResolution);
        }
    }

    /// <summary>Throws when <paramref name="resolution"/> is not a supported splat resolution.</summary>
    /// <param name="resolution">Candidate resolution.</param>
    /// <exception cref="TerrainException">The resolution is unsupported.</exception>
    public static void AssertSplatResolution(int resolution)
    {
        if (!IsSplatResolution(resolution))
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid splat resolution: {resolution}."),
                TerrainErrorCode.InvalidResolution);
        }
    }
}
