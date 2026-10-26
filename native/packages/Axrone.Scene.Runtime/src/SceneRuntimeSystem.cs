namespace Axrone.Scene.Runtime;

using Axrone.GameLoop;

/// <summary>
/// Game-loop system that drives a <see cref="SceneRuntime"/>: fixed steps and
/// frame updates dispatch world behaviours, then the render phase either
/// submits a GL frame or plans one headlessly.
/// </summary>
public sealed class SceneRuntimeSystem : IGameLoopSystem<SceneRuntimeFrameState>
{
    private readonly SceneRuntime _runtime;
    private readonly SceneGlFrameRenderer? _renderer;

    /// <summary>Creates a scene loop system.</summary>
    public SceneRuntimeSystem(SceneRuntime runtime, SceneGlFrameRenderer? renderer = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        _renderer = renderer;
    }

    /// <inheritdoc/>
    public SystemId Id { get; } = SystemId.FromString("scene.runtime");

    /// <inheritdoc/>
    public void FixedUpdate(ref readonly FixedUpdateContext<SceneRuntimeFrameState> ctx)
    {
        _runtime.World.FixedUpdate(ToSeconds(ctx.StepDelta));
        _runtime.World.UpdateTransforms();
    }

    /// <inheritdoc/>
    public void Update(ref readonly UpdateContext<SceneRuntimeFrameState> ctx)
    {
        float deltaSeconds = ToSeconds(ctx.Delta);
        _runtime.World.Update(deltaSeconds);
        _runtime.World.LateUpdate(deltaSeconds);
        _runtime.World.UpdateTransforms();
    }

    /// <inheritdoc/>
    public void Render(ref readonly RenderContext<SceneRuntimeFrameState> ctx)
    {
        int frameIndex = ToFrameIndex(ctx.Frame);
        float elapsedSeconds = ToSeconds(ctx.Elapsed);

        if (_renderer is not null)
        {
            SceneRenderStats stats = _renderer.Render(frameIndex, elapsedSeconds);
            ctx.State = new SceneRuntimeFrameState(ctx.Frame, stats, _runtime.Frame.Camera is not null);
            return;
        }

        bool cameraAvailable = _runtime.Frame.Plan(frameIndex);
        ctx.State = new SceneRuntimeFrameState(
            ctx.Frame,
            _runtime.Frame.Complete(0),
            cameraAvailable);
    }

    private static float ToSeconds(in GameTime time) =>
        (float)(time.TotalMilliseconds * 0.001);

    private static float ToSeconds(in FixedStep step) =>
        (float)(step.TotalMilliseconds * 0.001);

    private static int ToFrameIndex(FrameIndex frame) =>
        frame.Value > int.MaxValue ? int.MaxValue : (int)frame.Value;
}
