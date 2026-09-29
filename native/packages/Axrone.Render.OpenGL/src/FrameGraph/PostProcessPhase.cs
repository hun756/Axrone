namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// Where a post-processing effect runs relative to tone mapping. HDR-space
/// effects (SSAO, depth of field) run before; display-referred effects
/// (grain, vignette, grading) run after.
/// </summary>
public enum PostProcessPhase
{
    /// <summary>Effect runs in HDR space, before tone mapping.</summary>
    BeforeTonemap = 0,

    /// <summary>Effect runs in display space, after tone mapping.</summary>
    AfterTonemap = 1,
}
