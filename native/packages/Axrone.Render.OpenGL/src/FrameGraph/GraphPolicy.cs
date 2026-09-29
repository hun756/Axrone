namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// What the graph knows about a pass at the moment it is about to run it, or has
/// just run it.
/// </summary>
/// <remarks>
/// Passed by <c>ref readonly</c> to the policy hooks, so a hook can read the
/// identity without the graph allocating a copy per pass and without giving a
/// policy a handle it could rewrite. The pair is the whole point of the struct:
/// <see cref="Id"/> is what the graph indexes by (cheap, stable for the frame)
/// while <see cref="Name"/> is what a human reads in a log or a profiler
/// capture (authored, possibly duplicated, never unique).
/// </remarks>
/// <param name="Id">The graph slot of the pass.</param>
/// <param name="Name">The authored pass name.</param>
public readonly record struct PassMetadata(PassId Id, string Name);

/// <summary>
/// Compile-time policy of the frame graph: the four points where the graph has to
/// make a decision that a host, a profiler or a recording tool may want to
/// observe or override.
/// </summary>
/// <remarks>
/// <para><b>Why static abstract, and why this interface at all.</b> The policy is a
/// type argument of <see cref="FrameGraph{TPhase, TPolicy}"/>, and the hooks are
/// static abstracts, so the JIT monomorphizes every call site: there is no policy
/// object, no interface dispatch, no virtual call in the walk, and no captured
/// closure. A policy therefore has to be a stateless <c>struct</c> type. Anything
/// a host wants to record goes into the host's own state through the
/// <see cref="PassMetadata"/> parameters it is handed, or through a static field,
/// not into the policy.</para>
/// <para><b>Why four hooks and not a delegate chain.</b> The two execution hooks
/// give a policy a per-pass enter/exit pair that a profiler, a leak tracer or a
/// GPU-capture scope can bracket without the graph knowing about it. The two
/// failure hooks are the escape hatch for a host that wants to change what a
/// cycle or a pass failure means — report it to a crash reporter, degrade to a
/// previous frame's order, count frames dropped.</para>
/// <para><b>Fail-closed by contract.</b> <see cref="OnCycleDetected"/> and
/// <see cref="OnPassError"/> are permitted to throw, and
/// <see cref="DefaultGraphPolicy"/> does. A policy that instead only observes
/// does not suppress the failure: the graph still fails closed, because a
/// partially ordered graph or a silently skipped GL command is a corrupted frame,
/// not a degraded one. See the remarks on
/// <see cref="FrameGraphCompiledExtensions.Execute{TPolicy}"/>.</para>
/// </remarks>
public interface IGraphPolicy
{
    /// <summary>
    /// Called on the render thread immediately before a pass runs, and before any
    /// of its work reaches GL — including before a pump-capable pass enqueues.
    /// </summary>
    /// <param name="passId">The graph slot of the pass.</param>
    /// <param name="metadata">The pass identity. Read-only.</param>
    static abstract void OnPassExecuting(PassId passId, ref readonly PassMetadata metadata);

    /// <summary>
    /// Called on the render thread after a pass has completed its leg: after the
    /// direct execution, or after the pump accepted its command. A pass that
    /// failed, or that was skipped because it is disabled, does not fire this.
    /// </summary>
    /// <param name="passId">The graph slot of the pass.</param>
    /// <param name="metadata">The pass identity. Read-only.</param>
    static abstract void OnPassExecuted(PassId passId, ref readonly PassMetadata metadata);

    /// <summary>
    /// Called when the compile walk cannot order every enabled pass, which means
    /// the declared dependencies contain a cycle.
    /// </summary>
    /// <param name="totalPasses">The number of enabled passes the walk tried to order.</param>
    /// <param name="resolvedPasses">The number of passes that were ordered before the walk stalled.</param>
    static abstract void OnCycleDetected(uint totalPasses, uint resolvedPasses);

    /// <summary>
    /// Called when a pass's direct execution throws. A pass-identity
    /// <see cref="RenderException"/> is what reaches the caller.
    /// </summary>
    /// <param name="passId">The graph slot of the failing pass.</param>
    /// <param name="exception">The original exception, unwrapped.</param>
    static abstract void OnPassError(PassId passId, Exception exception);
}

// CA1815: a policy is a stateless strategy type, not a value. It has no
// instance state to compare, its hooks are static abstracts by design, and two
// DefaultGraphPolicy instances are the same policy by construction — the
// compiler is the only thing that should ever select one, through the type
// argument. Equality operators on it would suggest it is data.
#pragma warning disable CA1815

/// <summary>
/// The default frame-graph policy: today's observable behavior, expressed as
/// policy.
/// </summary>
/// <remarks>
/// <para>The two execution hooks are empty and inline away completely, so
/// <c>DefaultGraphPolicy</c> costs nothing at all on the walk. The two failure
/// hooks reproduce the exceptions the non-generic graph used to construct
/// directly:</para>
/// <list type="bullet">
/// <item><description><see cref="OnCycleDetected"/> throws
/// <see cref="RenderException"/> with
/// <see cref="RenderErrorCode.GraphCycleDetected"/>;</description></item>
/// <item><description><see cref="OnPassError"/> throws
/// <see cref="RenderException"/> with
/// <see cref="RenderErrorCode.PassExecutionFailed"/> and the original exception
/// as <see cref="Exception.InnerException"/>, so the pass failure keeps the type,
/// the message and the stack it had before the graph was split into phases.</description></item>
/// </list>
/// <para><b>What the pass-error message can and cannot carry.</b> The hook
/// signature is identity-by-id, so the default message names the failing pass by
/// its <see cref="PassId"/> (<c>Pass PassId(3) failed: ...</c>) and not by its
/// authored name. The hook receives no per-graph state by construction — a
/// static abstract cannot reach one — so no policy can recover the name from
/// here. A host that wants names in its failure log reads them from
/// <see cref="PassMetadata.Name"/> in <see cref="OnPassExecuting"/>, which is the
/// hook that has the pass in hand, or pairs this policy with a recording policy
/// that keeps its own id-to-name table.</para>
/// </remarks>
public readonly struct DefaultGraphPolicy : IGraphPolicy
{
    /// <summary>
    /// Does nothing. The default policy adds no per-pass work to the walk.
    /// </summary>
    /// <param name="passId">The graph slot of the pass.</param>
    /// <param name="metadata">The pass identity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnPassExecuting(PassId passId, ref readonly PassMetadata metadata)
    {
        // Intentionally empty: the default policy observes nothing and allocates nothing.
    }

    /// <summary>
    /// Does nothing. The default policy adds no per-pass work to the walk.
    /// </summary>
    /// <param name="passId">The graph slot of the pass.</param>
    /// <param name="metadata">The pass identity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnPassExecuted(PassId passId, ref readonly PassMetadata metadata)
    {
        // Intentionally empty: the default policy observes nothing and allocates nothing.
    }

    /// <summary>
    /// Throws the cycle error, exactly as the non-generic graph did.
    /// </summary>
    /// <param name="totalPasses">The number of enabled passes the walk tried to order.</param>
    /// <param name="resolvedPasses">The number of passes that were ordered before the walk stalled.</param>
    public static void OnCycleDetected(uint totalPasses, uint resolvedPasses) =>
        ThrowHelper.Throw(
            RenderErrorCode.GraphCycleDetected,
            $"Frame graph contains cycles and cannot be executed ({resolvedPasses} of {totalPasses} passes ordered)",
            GraphConstants.Name);

    /// <summary>
    /// Throws the pass-failure error with the original exception as the inner
    /// exception, so the pass identity and the cause both survive.
    /// </summary>
    /// <param name="passId">The graph slot of the failing pass.</param>
    /// <param name="exception">The original exception, unwrapped.</param>
    public static void OnPassError(PassId passId, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        throw new RenderException(
            $"Pass {passId} failed: {exception.Message}",
            RenderErrorCode.PassExecutionFailed,
            exception);
    }
}

#pragma warning restore CA1815

/// <summary>
/// Names shared by the frame graph types. Kept in one place so the exception
/// payloads name the graph exactly as before the phase split.
/// </summary>
internal static class GraphConstants
{
    /// <summary>
    /// The frame graph's public name, used as the exception context of every
    /// error the graph raises through <see cref="Axrone.Render.Core.ThrowHelper"/>.
    /// </summary>
    public const string Name = "FrameGraph";

    /// <summary>
    /// The default ring slot capacity of the graph-owned command pump. The
    /// public constructor does not take a capacity: the pump is an engine
    /// implementation detail, not a per-host tuning knob, and the previous
    /// default is the only value any shipped caller relied on.
    /// </summary>
    public const int DefaultPumpCapacity = 256;
}
