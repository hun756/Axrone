namespace Axrone.Render.OpenGL.Pipeline;

/// <summary>
/// Render pass execution context.
/// </summary>
public readonly record struct RenderExecutionContext(int FrameIndex, double DeltaTime, int BackbufferWidth, int BackbufferHeight);

/// <summary>
/// Render frame result.
/// </summary>
public readonly record struct RenderFrameResult(int TotalPassesExecuted, int TotalDrawCalls);

/// <summary>
/// Resolved render pass with all necessary information for execution.
/// </summary>
public readonly record struct ResolvedRenderPass(
    PassKind Kind,
    string Name,
    NativeHandle Target,
    NativeHandle SourceInput,
    int ViewportWidth,
    int ViewportHeight,
    bool DirectFrameOutput = false);

/// <summary>
/// Interface for render pass executors.
/// </summary>
public interface IRenderPassExecutor
{
    /// <summary>
    /// Gets the pass kind.
    /// </summary>
    PassKind Kind { get; }

    /// <summary>
    /// Gets the priority (higher = executed first).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Executes the render pass.
    /// </summary>
    /// <param name="pass">The resolved pass.</param>
    /// <param name="ctx">The execution context.</param>
    /// <param name="glContext">The GL context.</param>
    void Execute(in ResolvedRenderPass pass, in RenderExecutionContext ctx, GLContext glContext);
}
