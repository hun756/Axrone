namespace Axrone.Terrain;

/// <summary>Configuration shared by every generic terrain engine.</summary>
public sealed class TerrainEngineOptions
{
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

/// <summary>
/// Fluent builder for a generic terrain engine. The surface, backoff and observer policies
/// are type arguments, so the built engine dispatches to them without runtime policy lookup.
/// </summary>
/// <typeparam name="TSurface">Surface evaluator policy.</typeparam>
/// <typeparam name="TBackoff">Spin backoff policy from <c>Axrone.Utility</c>.</typeparam>
/// <typeparam name="TObserver">Chunk lifecycle observer policy.</typeparam>
public struct TerrainEngineBuilder<TSurface, TBackoff, TObserver>
    where TSurface : struct, ISurfaceEvaluator<TSurface>
    where TBackoff : struct, ISpinBackoff
    where TObserver : struct, IChunkLifecyclePolicy<TObserver>
{
    private TSurface _surface;
    private float _cellSize;
    private float _maxHeight;
    private int _poolCapacity;
    private bool _hasSurface;

    /// <summary>Creates a builder with the documented defaults.</summary>
    public TerrainEngineBuilder()
    {
        _surface = default;
        _cellSize = 1f;
        _maxHeight = 1f;
        _poolCapacity = 256;
        _hasSurface = false;
    }

    /// <summary>Supplies the surface evaluator.</summary>
    /// <param name="surface">Surface policy instance.</param>
    /// <returns>This builder.</returns>
    public TerrainEngineBuilder<TSurface, TBackoff, TObserver> WithSurface(TSurface surface)
    {
        _surface = surface;
        _hasSurface = true;
        return this;
    }

    /// <summary>Sets the grid cell size.</summary>
    /// <param name="cellSize">World-space cell size.</param>
    /// <returns>This builder.</returns>
    public TerrainEngineBuilder<TSurface, TBackoff, TObserver> WithCellSize(float cellSize)
    {
        _cellSize = cellSize;
        return this;
    }

    /// <summary>Sets the world-space height scale.</summary>
    /// <param name="maxHeight">Height applied to a normalized height of 1.</param>
    /// <returns>This builder.</returns>
    public TerrainEngineBuilder<TSurface, TBackoff, TObserver> WithMaxHeight(float maxHeight)
    {
        _maxHeight = maxHeight;
        return this;
    }

    /// <summary>Sets the chunk slot capacity.</summary>
    /// <param name="poolCapacity">Number of resident chunk slots.</param>
    /// <returns>This builder.</returns>
    public TerrainEngineBuilder<TSurface, TBackoff, TObserver> WithPoolCapacity(int poolCapacity)
    {
        _poolCapacity = poolCapacity;
        return this;
    }

    /// <summary>Builds the engine.</summary>
    /// <returns>A configured engine.</returns>
    /// <exception cref="TerrainException">No surface was supplied or a value is invalid.</exception>
    public readonly TerrainEngine<TSurface, TBackoff, TObserver> Build()
    {
        if (!_hasSurface)
        {
            throw new TerrainException("A surface evaluator is required.", TerrainErrorCode.ValidationFailed);
        }

        var options = new TerrainEngineOptions
        {
            CellSize = _cellSize,
            MaxHeight = _maxHeight,
            PoolCapacity = _poolCapacity,
        };
        options.Validate();
        return new TerrainEngine<TSurface, TBackoff, TObserver>(options, _surface);
    }
}
