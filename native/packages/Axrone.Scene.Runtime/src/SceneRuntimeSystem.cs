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
    private readonly SceneRuntimeProfiler _profiler;

    /// <summary>Creates a scene loop system.</summary>
    public SceneRuntimeSystem(SceneRuntime runtime, SceneGlFrameRenderer? renderer = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        _renderer = renderer;
        _profiler = runtime.Profiler;
    }

    /// <inheritdoc/>
    public SystemId Id { get; } = SystemId.FromString("scene.runtime");

    /// <inheritdoc/>
    public void BeforeUpdate(ref readonly BeforeUpdateContext<SceneRuntimeFrameState> ctx)
    {
        _profiler.BeginFrame(
            ToFrameIndex(ctx.Frame),
            ctx.Time.TotalMilliseconds,
            ctx.Delta.TotalMilliseconds);

        using SceneProfilerScope preUpdate = _profiler.MeasurePhase(SceneRuntimePhase.PreUpdate);
    }

    /// <inheritdoc/>
    public void FixedUpdate(ref readonly FixedUpdateContext<SceneRuntimeFrameState> ctx)
    {
        using SceneProfilerScope fixedUpdate = _profiler.MeasurePhase(SceneRuntimePhase.FixedUpdate);
        _runtime.World.FixedUpdate(ToSeconds(ctx.StepDelta));
        _runtime.World.UpdateTransforms();

        if (_runtime.PhysicsBridges.Bridge3D)
        {
            _runtime.Physics.SyncBodies();
            _runtime.Physics.Step(ToSeconds(ctx.StepDelta));
            _runtime.Physics.SyncTransformsFromPhysics();
        }
    }

    /// <inheritdoc/>
    public void Update(ref readonly UpdateContext<SceneRuntimeFrameState> ctx)
    {
        using SceneProfilerScope update = _profiler.MeasurePhase(SceneRuntimePhase.Update);
        float deltaSeconds = ToSeconds(ctx.Delta);
        _runtime.World.Update(deltaSeconds);
        _runtime.World.LateUpdate(deltaSeconds);
        _runtime.World.UpdateTransforms();
    }

    /// <inheritdoc/>
    public void Render(ref readonly RenderContext<SceneRuntimeFrameState> ctx)
    {
        using SceneProfilerScope render = _profiler.MeasurePhase(SceneRuntimePhase.Render);
        int frameIndex = ToFrameIndex(ctx.Frame);
        float elapsedSeconds = ToSeconds(ctx.Elapsed);

        if (_renderer is not null)
        {
            SceneRenderStats stats = _renderer.Render(frameIndex, elapsedSeconds);
            _profiler.AttachRenderStats(in stats);
            ctx.State = new SceneRuntimeFrameState(ctx.Frame, stats, _runtime.Frame.Camera is not null, _runtime.Frame.Mode);
            return;
        }

        bool cameraAvailable = _runtime.Frame.Plan(frameIndex);
        SceneRenderStats headlessStats = _runtime.Frame.Complete(0);
        _profiler.AttachRenderStats(in headlessStats);
        ctx.State = new SceneRuntimeFrameState(
            ctx.Frame,
            headlessStats,
            cameraAvailable,
            _runtime.Frame.Mode);
    }

    /// <inheritdoc/>
    public void AfterFrame(ref readonly AfterFrameContext<SceneRuntimeFrameState> ctx)
    {
        using (SceneProfilerScope postUpdate = _profiler.MeasurePhase(SceneRuntimePhase.PostUpdate))
        {
            // Post-frame hooks (physics bridges, streaming) run here.
        }

        _profiler.EndFrame(ctx.Time.TotalMilliseconds, (int)ctx.FixedStepsExecuted);
    }

    private static float ToSeconds(in GameTime time) =>
        (float)(time.TotalMilliseconds * 0.001);

    private static float ToSeconds(in FixedStep step) =>
        (float)(step.TotalMilliseconds * 0.001);

    private static int ToFrameIndex(FrameIndex frame) =>
        frame.Value > int.MaxValue ? int.MaxValue : (int)frame.Value;
}
