namespace Axrone.Scene.Runtime;

/// <summary>
/// Backend-agnostic frame planner. One instance is reused every frame: camera
/// selection, lighting synchronization, pass iteration and render-item queues
/// all write into preallocated state, so a warmed frame allocates nothing.
/// </summary>
public sealed class SceneFramePipeline : IDisposable
{
    private readonly SceneWorld _world;
    private readonly SceneAssetRegistry _assets;
    private readonly SceneCameraFrameCollector _cameraCollector = new();
    private readonly SceneLightingCollector _lightingCollector;
    private readonly SceneRenderItemCollector _items;
    private readonly SceneRenderPassPlan _passes;
    private readonly SceneRenderFrameState _frameState = new();

    private readonly Vec3 _ambient;
    private readonly Vec3 _sky;
    private readonly Vec3 _ground;

    private int _viewportWidth;
    private int _viewportHeight;
    private bool _disposed;

    /// <summary>Creates a frame planner for a world and asset catalog.</summary>
    public SceneFramePipeline(
        SceneWorld world,
        SceneAssetRegistry assets,
        int viewportWidth,
        int viewportHeight,
        in Vec3 ambient,
        in Vec3 sky,
        in Vec3 ground,
        int maxLocalLights = 4)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(assets);
        ValidateViewport(viewportWidth, viewportHeight);

        if (maxLocalLights < 0)
        {
            throw new SceneConfigurationException(
                $"Local-light capacity must not be negative; received {maxLocalLights}.");
        }

        _world = world;
        _assets = assets;
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        _ambient = ambient;
        _sky = sky;
        _ground = ground;
        _lightingCollector = new SceneLightingCollector(maxLocalLights);
        _items = new SceneRenderItemCollector();
        _passes = new SceneRenderPassPlan(assets);
    }

    /// <summary>Selected camera frame for the current frame, or null before planning.</summary>
    public SceneCameraFrame? Camera { get; private set; }

    /// <summary>Resolved lighting for the current frame, or null when no camera exists.</summary>
    public LightingSelectionState? Lighting { get; private set; }

    /// <summary>Enabled render passes in execution order.</summary>
    public SceneRenderPassPlan Passes => _passes;

    /// <summary>Mutable per-frame counters.</summary>
    public SceneRenderFrameState FrameState => _frameState;

    /// <summary>Planned render items for the last collected pass.</summary>
    public ReadOnlySpan<SceneRenderItem> Items => _items.Items;

    /// <summary>Viewport width in pixels.</summary>
    public int ViewportWidth
    {
        get => _viewportWidth;
        set
        {
            ValidateViewport(value, _viewportHeight);
            _viewportWidth = value;
        }
    }

    /// <summary>Viewport height in pixels.</summary>
    public int ViewportHeight
    {
        get => _viewportHeight;
        set
        {
            ValidateViewport(_viewportWidth, value);
            _viewportHeight = value;
        }
    }

    /// <summary>Rebuilds the ordered pass list after asset changes.</summary>
    public void RebuildPasses()
    {
        ThrowIfDisposed();
        _passes.Rebuild();
    }

    /// <summary>
    /// Plans camera selection and lighting for one frame.
    /// </summary>
    /// <returns>False when the scene has no enabled camera.</returns>
    public bool Plan(int frameIndex)
    {
        ThrowIfDisposed();

        if (frameIndex < 0)
        {
            throw new SceneConfigurationException(
                $"Frame indices must not be negative; received {frameIndex}.");
        }

        _frameState.Begin(frameIndex);
        Camera = _cameraCollector.Collect(_world, _viewportWidth, _viewportHeight);

        if (Camera is null)
        {
            Lighting = null;
            return false;
        }

        Lighting = _lightingCollector.Collect(
            _world,
            Camera.Position,
            _ambient,
            _sky,
            _ground);
        return true;
    }

    /// <summary>Collects, culls and sorts items for one pass.</summary>
    /// <returns>Number of planned items.</returns>
    public int CollectItems(SceneAssetId<RenderPassAssetTag> passId)
    {
        ThrowIfDisposed();

        if (Camera is null)
        {
            throw new SceneLifecycleException(
                "Render items cannot be collected before a camera frame is planned.");
        }

        int count = _items.Collect(_world, Camera, passId, _assets);

        int transparent = 0;
        ReadOnlySpan<SceneRenderItem> items = _items.Items;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].Transparent)
            {
                transparent++;
            }
        }

        _frameState.RecordQueues(count - transparent, transparent);
        return count;
    }

    /// <summary>Takes the immutable counter snapshot for the finished frame.</summary>
    public SceneRenderStats Complete(int passCount) => _frameState.Snapshot(passCount);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lightingCollector.Dispose();
        _disposed = true;
    }

    private static void ValidateViewport(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new SceneConfigurationException(
                $"Viewport dimensions must be positive; received {width}x{height}.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new SceneDisposedException(nameof(SceneFramePipeline));
        }
    }
}
