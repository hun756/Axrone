using Axrone.Execution;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// Implemented by passes that can express their work as render pump commands.
/// The frame graph calls <see cref="EnqueueCommands"/> opportunistically during
/// <see cref="FrameGraph.EnqueuePumpPasses"/>; passes that do not implement this
/// interface keep their direct <see cref="RenderPass.Execute"/> path unchanged.
/// </summary>
/// <remarks>
/// <para>
/// The contract is best-effort: the graph never requires a pass to be
/// pump-capable, and a pump pass whose enqueue is refused (ring full) is simply
/// not counted — the pass's direct <see cref="RenderPass.Execute"/> path remains
/// the fallback for callers that invoke <see cref="FrameGraph.Execute"/>.
/// </para>
/// <para>
/// Implementations must resolve resources from the execution context at enqueue
/// time and must not retain the pump or the receipt beyond the call.
/// </para>
/// </remarks>
public interface IPumpEnqueue
{
    /// <summary>
    /// Enqueues this pass's work as a render command into the given pump.
    /// </summary>
    /// <param name="pump">The pump receiving the command.</param>
    /// <param name="ctx">The pass execution context for resource resolution.</param>
    /// <returns>The enqueue receipt.</returns>
    EnqueueResult EnqueueCommands(RenderPump pump, PassExecutionContext ctx);
}
