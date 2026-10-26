namespace Axrone.Scene.Runtime;

using Axrone.GameLoop;

/// <summary>State carried through the game loop by the scene system.</summary>
public readonly record struct SceneRuntimeFrameState(
    FrameIndex Frame,
    SceneRenderStats RenderStats,
    bool CameraAvailable,
    SceneRenderMode Mode);

/// <summary>Loop timing settings for <see cref="SceneRuntimeLoop"/>.</summary>
public sealed record SceneRuntimeLoopOptions
{
    /// <summary>Fixed simulation step.</summary>
    public FixedStep FixedDelta { get; init; } = FixedStep.SixtyFps;

    /// <summary>Frame delta clamp.</summary>
    public GameTime MaxDelta { get; init; } = GameTime.FromMilliseconds(250.0);

    /// <summary>Maximum fixed steps per frame.</summary>
    public uint MaxSubSteps { get; init; } = 8U;

    /// <summary>Time multiplier.</summary>
    public TimeScale TimeScale { get; init; } = TimeScale.Identity;

    /// <summary>Initial system capacity.</summary>
    public int InitialCapacity { get; init; } = 8;
}
