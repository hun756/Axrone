namespace Axrone.Scene.Runtime;

/// <summary>
/// OpenGL execution layer over a <see cref="SceneRuntime"/>. Uploads the asset
/// catalog once (or after a reload) and submits one draw pass per planned
/// render pass. All methods are render-thread affine.
/// </summary>
public sealed class SceneGlFrameRenderer : IDisposable
{
    private readonly GLContext _context;
    private readonly SceneRuntime _runtime;
    private readonly SceneGlAssetCache _assets;
    private readonly SceneGlDrawExecutor _executor;
    private bool _disposed;

    /// <summary>Creates a GL frame renderer for a runtime and context.</summary>
    public SceneGlFrameRenderer(GLContext context, SceneRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(runtime);

        _context = context;
        _runtime = runtime;
        _assets = new SceneGlAssetCache(context, runtime.Assets);
        _executor = new SceneGlDrawExecutor(context, _assets);
    }

    /// <summary>GPU-side asset cache.</summary>
    public SceneGlAssetCache GpuAssets => _assets;

    /// <summary>Uploads every registered asset; call after loading a snapshot.</summary>
    public SceneGlUploadStats UploadAssets()
    {
        ThrowIfDisposed();
        return _assets.UploadAll();
    }

    /// <summary>Plans and renders one frame.</summary>
    public SceneRenderStats Render(int frameIndex, float elapsedSeconds = 0.0f)
    {
        ThrowIfDisposed();
        _context.AssertRenderThread();

        SceneFramePipeline frame = _runtime.Frame;
        if (!frame.Plan(frameIndex))
        {
            return frame.Complete(0);
        }

        SceneCameraFrame camera = frame.Camera!;
        ReadOnlySpan<SceneRenderPassDefinition> passes = frame.Passes.Passes;
        int passCount = 0;

        for (int i = 0; i < passes.Length; i++)
        {
            SceneRenderPassDefinition pass = passes[i];
            _ = frame.CollectItems(pass.Id);
            _executor.Render(
                _runtime.World,
                camera,
                frame.Items,
                frame.Lighting,
                pass,
                frame.FrameState,
                elapsedSeconds);
            passCount++;
        }

        return frame.Complete(passCount);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _assets.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new SceneDisposedException(nameof(SceneGlFrameRenderer));
        }
    }
}
