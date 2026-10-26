namespace Axrone.Scene.Runtime;

/// <summary>Frame phases recorded by the scene runtime profiler.</summary>
public enum SceneRuntimePhase
{
    /// <summary>Work before fixed steps.</summary>
    PreUpdate = 0,

    /// <summary>Fixed-step simulation.</summary>
    FixedUpdate = 1,

    /// <summary>Variable frame update.</summary>
    Update = 2,

    /// <summary>Post-frame work.</summary>
    PostUpdate = 3,

    /// <summary>Rendering and submission.</summary>
    Render = 4
}

/// <summary>Scene physics timings captured for a frame.</summary>
public readonly record struct ScenePhysicsStats(
    float StepMs,
    float CollisionMs,
    float SolveMs);

/// <summary>Per-phase frame timings in milliseconds.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SceneRuntimePhaseTimes(
    float PreUpdateMs,
    float FixedUpdateMs,
    float UpdateMs,
    float PostUpdateMs,
    float RenderMs)
{
    /// <summary>Reads one phase timing.</summary>
    public float GetPhaseMs(SceneRuntimePhase phase) => phase switch
    {
        SceneRuntimePhase.PreUpdate => PreUpdateMs,
        SceneRuntimePhase.FixedUpdate => FixedUpdateMs,
        SceneRuntimePhase.Update => UpdateMs,
        SceneRuntimePhase.PostUpdate => PostUpdateMs,
        SceneRuntimePhase.Render => RenderMs,
        _ => 0.0f
    };
}

/// <summary>One captured stack frame.</summary>
public readonly record struct SceneRuntimeStackFrame(string Function, string? File);

/// <summary>Stack sample captured for one phase.</summary>
public sealed class SceneRuntimeStackSample
{
    /// <summary>Creates a stack sample.</summary>
    public SceneRuntimeStackSample(
        SceneRuntimePhase phase,
        IReadOnlyList<SceneRuntimeStackFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        Phase = phase;
        Frames = [.. frames];
    }

    /// <summary>Phase the sample was captured in.</summary>
    public SceneRuntimePhase Phase { get; }

    /// <summary>Captured frames, innermost first.</summary>
    public IReadOnlyList<SceneRuntimeStackFrame> Frames { get; }
}

/// <summary>Immutable record for one profiled frame.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct SceneRuntimeFrameRecord(
    int Frame,
    double TimestampMs,
    double FrameTimeMs,
    double Fps,
    double DeltaMs,
    int FixedSteps,
    SceneRuntimePhaseTimes PhaseMs,
    bool HasRenderStats,
    SceneRenderStats RenderStats,
    bool HasPhysicsStats,
    ScenePhysicsStats PhysicsStats,
    bool HasMemorySample,
    long MemoryUsedBytes);

/// <summary>Rolling profiler summary over the recorded window.</summary>
public readonly record struct SceneRuntimeProfilerSummary(
    int FrameCount,
    double AvgFrameTimeMs,
    double MinFrameTimeMs,
    double MaxFrameTimeMs,
    double P95FrameTimeMs,
    double AvgFps,
    SceneRuntimePhaseTimes AvgPhaseMs);

/// <summary>Profiler capacity, sampling and enablement settings.</summary>
public sealed record SceneRuntimeProfilerOptions
{
    /// <summary>Retained frame records.</summary>
    public int Capacity { get; init; } = 300;

    /// <summary>Whether recording starts enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Interval between managed-memory samples.</summary>
    public double MemorySampleIntervalMs { get; init; } = 250.0;

    /// <summary>Retained stack samples.</summary>
    public int StackSampleCapacity { get; init; } = 4000;
}

/// <summary>Receives every completed frame record.</summary>
/// <param name="record">Completed frame record.</param>
public delegate void SceneRuntimeProfilerListener(in SceneRuntimeFrameRecord record);
