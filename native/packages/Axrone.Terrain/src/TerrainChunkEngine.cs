namespace Axrone.Terrain;

/// <summary>
/// Convenience engine closed over the seeded value-noise surface, the adaptive spin backoff
/// and the no-op lifecycle observer. Use
/// <see cref="TerrainEngine{TSurface,TBackoff,TObserver}"/> directly when a custom surface or
/// observer is needed.
/// </summary>
public sealed class TerrainChunkEngine
    : TerrainEngine<SeededNoiseSurface, AdaptiveSpinBackoff, NoOpLifecyclePolicy>
{
    /// <summary>Creates a seeded chunk engine.</summary>
    /// <param name="options">Engine configuration.</param>
    /// <exception cref="TerrainException">The options are invalid.</exception>
    public TerrainChunkEngine(TerrainChunkEngineOptions options)
        : base(
            TerrainChunkEngineOptions.ToEngineOptions(options),
            new SeededNoiseSurface(options.Surface))
    {
    }
}
