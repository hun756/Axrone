namespace Axrone.Terrain;

/// <summary>World-space XZ position used by surface evaluators.</summary>
/// <param name="X">World X.</param>
/// <param name="Z">World Z.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct WorldPosition2D(float X, float Z)
{
    /// <summary>Adds two positions component-wise.</summary>
    public static WorldPosition2D operator +(WorldPosition2D left, WorldPosition2D right) =>
        new(left.X + right.X, left.Z + right.Z);

    /// <summary>Subtracts two positions component-wise.</summary>
    public static WorldPosition2D operator -(WorldPosition2D left, WorldPosition2D right) =>
        new(left.X - right.X, left.Z - right.Z);
}

/// <summary>Integer address of one grid cell inside a chunk.</summary>
/// <param name="X">Cell column.</param>
/// <param name="Z">Cell row.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GridCoord(int X, int Z);

/// <summary>Closed normalized height interval.</summary>
/// <param name="Min">Smallest height.</param>
/// <param name="Max">Largest height.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct HeightExtent(float Min, float Max)
{
    /// <summary>Tests whether a height lies inside the interval.</summary>
    /// <param name="height">Height to test.</param>
    /// <returns><see langword="true"/> when the height is inside the closed interval.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(float height) => height >= Min && height <= Max;
}

/// <summary>
/// Compile-time surface policy. The engine is generic over the evaluator, so the JIT sees a
/// concrete call per implementation instead of a virtual dispatch. The interface is instance
/// based rather than static abstract so a seeded or otherwise stateful surface can implement
/// it without hiding state in a static field.
/// </summary>
/// <typeparam name="TSelf">The implementing surface.</typeparam>
public interface ISurfaceEvaluator<TSelf>
    where TSelf : struct, ISurfaceEvaluator<TSelf>
{
    /// <summary>Samples the normalized height at a world position.</summary>
    /// <param name="position">World XZ position.</param>
    /// <returns>A normalized height in <c>[0, 1]</c>.</returns>
    float EvaluateHeight(WorldPosition2D position);

    /// <summary>Samples a straight row of normalized heights.</summary>
    /// <param name="start">World XZ position of the first sample.</param>
    /// <param name="stepX">World-space distance between samples.</param>
    /// <param name="destination">Destination row.</param>
    void EvaluateRow(WorldPosition2D start, float stepX, Span<float> destination);
}

/// <summary>
/// Adapts the stateful <see cref="ITerrainSurface"/> contract to the compile-time
/// <see cref="ISurfaceEvaluator{TSelf}"/> policy used by the generic engine.
/// </summary>
public readonly struct SeededNoiseSurface : ISurfaceEvaluator<SeededNoiseSurface>
{
    private readonly ITerrainSurface _surface;

    /// <summary>Wraps a stateful surface.</summary>
    /// <param name="surface">Surface to delegate to.</param>
    public SeededNoiseSurface(ITerrainSurface surface)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float EvaluateHeight(WorldPosition2D position) => _surface.SampleHeight(position.X, position.Z);

    /// <inheritdoc/>
    public void EvaluateRow(WorldPosition2D start, float stepX, Span<float> destination) =>
        _surface.SampleRow(start.X, start.Z, stepX, destination);
}

/// <summary>Stateless chunk lifecycle observer invoked on residency changes.</summary>
/// <typeparam name="TSelf">The implementing policy.</typeparam>
public interface IChunkLifecyclePolicy<TSelf>
    where TSelf : struct, IChunkLifecyclePolicy<TSelf>
{
    /// <summary>Called after a chunk becomes resident.</summary>
    /// <param name="coord">Committed chunk.</param>
    static abstract void OnMounted(TerrainChunkCoord coord);

    /// <summary>Called before a chunk is released.</summary>
    /// <param name="coord">Chunk about to be released.</param>
    static abstract void OnUnmounted(TerrainChunkCoord coord);
}

/// <summary>Observer policy that does nothing.</summary>
public readonly struct NoOpLifecyclePolicy : IChunkLifecyclePolicy<NoOpLifecyclePolicy>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnMounted(TerrainChunkCoord coord)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnUnmounted(TerrainChunkCoord coord)
    {
    }
}

/// <summary>GPU-ready interleaved terrain vertex.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct TerrainVertex
{
    /// <summary>Initializes a vertex.</summary>
    /// <param name="position">Chunk-local position.</param>
    /// <param name="normal">Unit normal.</param>
    /// <param name="texCoord">Normalized chunk UV.</param>
    /// <param name="splatWeights">Normalized RGBA splat weights.</param>
    public TerrainVertex(Vec3 position, Vec3 normal, Vec2 texCoord, Vec4 splatWeights)
    {
        Position = position;
        Normal = normal;
        TexCoord = texCoord;
        SplatWeights = splatWeights;
    }

    /// <summary>Gets the chunk-local position.</summary>
    public Vec3 Position { get; }

    /// <summary>Gets the unit normal.</summary>
    public Vec3 Normal { get; }

    /// <summary>Gets the normalized chunk UV.</summary>
    public Vec2 TexCoord { get; }

    /// <summary>Gets the normalized RGBA splat weights.</summary>
    public Vec4 SplatWeights { get; }
}
