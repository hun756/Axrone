using Axrone.Execution;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph;

// CA1716: 'Declare' is a Visual Basic reserved word. The phase name is a
// cross-agent contract (every shipped pass payload and the test payloads implement
// it), so it stays; the reserved word is harmless for a static abstract member that
// no Visual Basic caller can implement.
#pragma warning disable CA1716

/// <summary>
/// The setup phase of a render pass, expressed as a static abstract so the
/// dependency and attachment declaration is part of the payload type itself
/// instead of a delegate handed to the pass at construction.
/// </summary>
/// <remarks>
/// <para>Setup is CPU work: the frame graph calls it once, on the thread that
/// constructs the pass, and snapshots the result into the pass's read/write sets,
/// descriptor and load/store actions. It runs before the render thread exists, so
/// it must not touch GL.</para>
/// <para>Static abstracts let the JIT monomorphize the call site, so a pass
/// carries no setup delegate, allocates no closure, and cannot be constructed with
/// a missing or mismatched phase: the constraint on
/// <see cref="RenderPass{TPassData}"/> turns every phase error into a compile
/// error. This is the same idiom as <see cref="ICommandProcessor{TCommand, TContext}"/>
/// and <c>IGLFullscreenInvoker&lt;TSelf&gt;</c>.</para>
/// <para>The payload arrives by <c>ref</c> so a phase may still finish initializing
/// it in place; the shipped passes only read, and the built-in payloads are already
/// fully populated by their factory before the pass is constructed.</para>
/// </remarks>
/// <typeparam name="TSelf">The payload type that owns the phase implementation.</typeparam>
public interface IPassSetup<TSelf>
{
    /// <summary>Declares this pass's resource dependencies, descriptor and attachment actions.</summary>
    /// <param name="builder">The builder collecting the declarations.</param>
    /// <param name="data">The pass payload being declared.</param>
    static abstract void Declare(IRenderPassBuilder builder, ref TSelf data);
}

/// <summary>
/// The validation phase of a render pass, expressed as a static abstract.
/// Enforces pass-specific pre-conditions (disposed programs, value ranges, wiring)
/// before the graph executes.
/// </summary>
/// <remarks>
/// Validation is CPU work and runs on the graph's compile leg, which is a mutation
/// path rather than the per-frame hot path. It is mandatory by construction: a
/// payload that satisfies <see cref="IPassValidate{TSelf}"/> always has a
/// <c>Validate</c> body, so there is no null check and no way to ship a pass whose
/// validation is silently skipped. A payload with no pre-conditions implements an
/// empty body.
/// </remarks>
/// <typeparam name="TSelf">The payload type that owns the phase implementation.</typeparam>
public interface IPassValidate<TSelf>
{
    /// <summary>Validates the pass configuration.</summary>
    /// <param name="data">The pass payload to validate.</param>
    static abstract void Validate(in TSelf data);
}

/// <summary>
/// The direct-execution phase of a render pass, expressed as a static abstract.
/// </summary>
/// <remarks>
/// <para>Execution is render-thread work. It receives the graph-owned
/// <see cref="IRenderContext"/> (whose state cache stays warm across the whole
/// walk) plus the <see cref="PassExecutionContext"/> for resource resolution. Raw
/// GL remains reachable through <see cref="PassExecutionContext.Context"/> for
/// operations the hardware-agnostic verbs do not cover (SSBO/image bindings,
/// matrix uploads, mesh draws).</para>
/// <para>The payload is passed as <c>in</c>: a readonly reference, so the phase
/// cannot mutate the pass state the graph is about to execute.</para>
/// </remarks>
/// <typeparam name="TSelf">The payload type that owns the phase implementation.</typeparam>
public interface IPassExecute<TSelf>
{
    /// <summary>Executes the pass on the render thread.</summary>
    /// <param name="data">The pass payload to execute.</param>
    /// <param name="context">The graph-owned render context with a warm state cache.</param>
    /// <param name="ctx">The pass execution context for resource resolution.</param>
    static abstract void Execute(in TSelf data, IRenderContext context, PassExecutionContext ctx);
}

/// <summary>
/// The pump-enqueue phase of a pump-capable render pass, expressed as a static
/// abstract: the render-thread work expressed as a render pump command instead of
/// issued inline.
/// </summary>
/// <remarks>
/// <para>Enqueue is render-thread work that runs ahead of execution, so the pass's
/// GL effects land at the next drain point in graph order. Resources are resolved
/// from the execution context at enqueue time, and the implementation must not
/// retain the pump or the receipt beyond the call.</para>
/// <para>Refusal (ring full) is a normal outcome, not an error: the frame graph
/// drains and retries once, then falls back to
/// <see cref="IPassExecute{TSelf}"/>, so a pump-capable pass still runs exactly
/// once.</para>
/// </remarks>
/// <typeparam name="TSelf">The payload type that owns the phase implementation.</typeparam>
public interface IPassEnqueue<TSelf>
{
    /// <summary>Enqueues this pass's work as a render command.</summary>
    /// <param name="data">The pass payload to enqueue.</param>
    /// <param name="pump">The pump receiving the command.</param>
    /// <param name="ctx">The pass execution context for resource resolution.</param>
    /// <returns>The enqueue receipt.</returns>
    static abstract EnqueueResult EnqueueCommands(in TSelf data, RenderPump pump, PassExecutionContext ctx);
}
