namespace Axrone.Scene.Rendering.OpenGL;

/// <summary>
/// Executes a planned frame across every active render path. For each pass it
/// submits 3D meshes through the draw executor and 2D sprites through the
/// sprite batch, so mixed 2D/3D scenes share one deterministic pass order.
/// </summary>
public sealed class SceneGlRenderOrchestrator : IDisposable
{
    private readonly SceneGlDrawExecutor _meshExecutor;
    private readonly SceneGlSpriteBatchRenderer _spriteRenderer;
    private readonly bool _hasSpriteComponent;
    private bool _disposed;

    /// <summary>Creates an orchestrator over an uploaded GPU asset cache.</summary>
    public SceneGlRenderOrchestrator(
        GLContext context,
        SceneGlAssetCache assets,
        SceneWorld world)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(world);

        _meshExecutor = new SceneGlDrawExecutor(context, assets);
        _spriteRenderer = new SceneGlSpriteBatchRenderer(context, assets);
        _hasSpriteComponent = world.Registry.TryGetDataByType(typeof(SceneSpriteRenderer), out _);
    }

    /// <summary>
    /// Renders every enabled pass and returns the accumulated frame counters.
    /// </summary>
    public SceneRenderStats RenderFrame(
        SceneWorld world,
        SceneCameraFrame camera,
        ISceneFrameItemSource items,
        ReadOnlySpan<SceneRenderPassDefinition> passes,
        SceneRenderFrameState frameState,
        float elapsedSeconds = 0.0f)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(frameState);

        int passCount = 0;

        for (int i = 0; i < passes.Length; i++)
        {
            SceneRenderPassDefinition pass = passes[i];

            ReadOnlySpan<SceneRenderItem> meshes = items.CollectMeshes(pass.Id);
            _meshExecutor.Render(
                world,
                camera,
                meshes,
                items.Lighting,
                pass,
                frameState,
                elapsedSeconds);

            if (_hasSpriteComponent)
            {
                ReadOnlySpan<SceneSpriteRenderItem> sprites = items.CollectSprites(pass.Id);
                if (sprites.Length > 0)
                {
                    _ = _spriteRenderer.Render(world, camera, sprites, pass, frameState);
                }
            }

            passCount++;
        }

        return frameState.Snapshot(passCount);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _spriteRenderer.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new SceneDisposedException(nameof(SceneGlRenderOrchestrator));
        }
    }
}
