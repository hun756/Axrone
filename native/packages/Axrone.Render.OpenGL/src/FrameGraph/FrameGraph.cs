using System.Runtime.ExceptionServices;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph;

// CA1040: 'IGraphPhase' is a typestate marker, not an abstraction to be replaced
// by a type alias. A phase type must be a nominal type the caller can name, store
// in a variable and pass as a type argument; a using alias cannot appear in a
// type argument list, so replacing the marker would remove the only thing the
// constraint enforces — that a phase type belongs to the graph's own phase
// vocabulary.
#pragma warning disable CA1040

/// <summary>
/// Marker for the typestate phases a <see cref="FrameGraph{TPhase, TPolicy}"/>
/// can be in.
/// </summary>
/// <remarks>
/// <para>The phase is carried in the handle's type argument, so an illegal
/// operation is a compile error instead of a run-time state check:
/// <c>Execute</c> only exists on a compiled graph, <c>Compile</c> only on a
/// building one, and a pass can only be added before the order exists. Nothing
/// in the graph has to ask "which state is this in?" on any leg, including the
/// per-frame one.</para>
/// <para>The marker is what makes the constraint
/// <c>where TPhase : struct, IGraphPhase</c> reject an arbitrary type
/// parameter: a caller can declare their own phase type but cannot pass
/// <c>int</c>, and two unrelated marker hierarchies cannot be mixed.</para>
/// </remarks>
public interface IGraphPhase
{
}

/// <summary>
/// The phase of a graph that accepts passes and has not compiled an order yet.
/// </summary>
public readonly struct BuildingPhase : IGraphPhase
{
}

/// <summary>
/// The phase of a graph that has a validated, topologically ordered pass list.
/// </summary>
public readonly struct CompiledPhase : IGraphPhase
{
}

#pragma warning restore CA1040

// CA1001: false positive on generic types. FrameGraph<TPhase, TPolicy> does
// implement IDisposable (Dispose below forwards to the storage), but the
// analyzer does not resolve the IDisposable constraint on a generic type
// declaration, so it reports the disposable storage field as undisposed.
#pragma warning disable CA1001

// CA1815: a frame-graph handle is a view, not a value. Two handles that name
// the same storage are two views of one graph, and a handle compared for
// equality would invite the question "is this the same graph?", which the
// answer is always no: the graph is the storage, and two handles to it are
// interchangeable. Adding Equals/== to a handle would encode a distinction the
// type does not have.
#pragma warning disable CA1815

/// <summary>
/// A render graph that owns pass ordering, resource dependency resolution and
/// execution, in one of two typestate phases.
/// </summary>
/// <typeparam name="TPhase">
/// The current phase: <see cref="BuildingPhase"/> accepts passes and compiles;
/// <see cref="CompiledPhase"/> executes. The phase is a type argument, so a
/// mis-ordered call does not compile.
/// </typeparam>
/// <typeparam name="TPolicy">
/// The compile-time policy: a stateless <c>struct</c> implementing
/// <see cref="IGraphPolicy"/>. The hooks are static abstracts, so every call
/// site is monomorphized — no policy object, no interface dispatch, no closure.
/// </typeparam>
/// <remarks>
/// <para><b>What is generic here, and what is deliberately not.</b> Two type
/// arguments: the phase (a real state machine the compiler enforces) and the
/// policy (a real cross-cutting concern with four distinct call sites). There is
/// deliberately <b>no</b> <c>TContext</c>. The engine has exactly one execution
/// context — the graph-owned <see cref="GLRenderContext"/> plus the internal
/// <see cref="PassExecutionContext"/> — so a context parameter would have one
/// instantiation, no caller able to select another, and every member of the walk
/// rewritten to thread a type argument that carries no information. That is
/// template theater: a second axis of genericity with a single inhabitant is
/// code the reader must verify and the JIT must instantiate for nothing. The
/// same reasoning rules out a generic backend: a frame graph that does not
/// execute on WebGL is a different engine, not a different instantiation.</para>
/// <para><b>Storage, handles and ownership.</b> The handle is a readonly struct
/// over a reference-shared <see cref="FrameGraphStorage"/>, so it is cheap to
/// copy, carries its phase in its type, and behaves like the mutable graph it
/// replaces: <c>graph.AddPass(p)</c> may be used as a statement or chained,
/// because both the returned handle and the receiver name the same storage.
/// Disposing any handle — building or compiled — releases the graph once,
/// including the command pump.</para>
/// <para><b>Thread affinity.</b> Single render thread, no locks, by design. The
/// graph mutates a pass table, interns names, rebuilds pooled scratch and drives
/// a single-consumer command pump; the GL context underneath is thread-affine
/// as well. Build and walk the graph from the thread that owns the context.</para>
/// <para><b>No fixed capacity, no lifetime allocator.</b> Pass and resource
/// tables and the interned span buffers grow dynamically; compile scratch comes
/// from <see cref="ArrayPool{T}"/> and is returned in <c>finally</c>. Neither
/// choice is a shortcut: a capacity cap is a guess that either breaks a shipped
/// scene or taxes every small graph, and an arena/region lifetime allocator
/// needs a measured allocation profile to justify, which a mutation-leg compile
/// does not have.</para>
/// <para><b>Two resource planes.</b> Resource <i>names</i> are what passes
/// author, and they are interned to <see cref="ResourceId"/> at
/// <c>AddPass</c> so the compile walk is integer-only. Resolution of the actual
/// GPU objects stays string-keyed in <see cref="PassExecutionContext"/>. See the
/// remarks on <see cref="ResourceId"/> for why those two planes are separate.</para>
/// </remarks>
public readonly struct FrameGraph<TPhase, TPolicy> : IDisposable
    where TPhase : struct, IGraphPhase
    where TPolicy : struct, IGraphPolicy
{
    private readonly FrameGraphStorage? _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameGraph{TPhase, TPolicy}"/>
    /// struct in the building phase, with the default command-pump capacity.
    /// </summary>
    /// <param name="context">The GL context this graph renders on.</param>
    /// <remarks>
    /// The pump capacity is not a parameter: the pump is an implementation
    /// detail of the walk, not a per-host tuning knob, so the graph owns the
    /// choice and a host that needs a different one can size the graph's pass
    /// set instead. Everything else the graph needs — the render context, the
    /// per-frame resource scratch and the pump — is created here and lives as
    /// long as the graph.
    /// </remarks>
    public FrameGraph(GLContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _storage = new FrameGraphStorage(context, GraphConstants.DefaultPumpCapacity);
    }

    /// <summary>
    /// Initializes a handle over storage that another phase handle already owns.
    /// </summary>
    /// <param name="storage">The shared graph storage.</param>
    /// <remarks>
    /// Used by the phase transitions (<c>Compile</c>, <c>Reset</c>). The handle
    /// is a view, not a new graph: the phase changes, the storage does not.
    /// </remarks>
    internal FrameGraph(FrameGraphStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

    /// <summary>
    /// Gets the storage this handle views. Extension methods in this assembly
    /// reach the graph through it, so a phase transition and a storage mutation
    /// are two ordinary typed calls instead of a reinterpretation trick.
    /// </summary>
    internal FrameGraphStorage? Storage => _storage;

    /// <summary>
    /// Gets a value indicating whether this graph has been disposed. A
    /// default-constructed handle owns no storage and therefore reads as
    /// disposed.
    /// </summary>
    public bool IsDisposed => _storage is null || _storage.IsDisposed;

    /// <summary>
    /// Gets the number of passes in the graph, in any phase.
    /// </summary>
    public int PassCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _storage is null ? 0 : _storage.PassCount;
    }

    /// <summary>
    /// Gets the command pump owned by this graph. Pump-capable passes
    /// (<see cref="IPumpEnqueue"/>) enqueue library commands into this pump
    /// during the walk; the consumer drains it via
    /// <see cref="FrameGraphCompiledExtensions.PumpQueuedCommands{TPolicy}"/>.
    /// The pump is created with the graph and disposed with it; a reset leaves
    /// it alive.
    /// </summary>
    public RenderPump Pump => AliveStorage.Pump;

    /// <summary>
    /// Gets the graph-owned render context used to execute every pass.
    /// </summary>
    /// <remarks>
    /// A single instance lives for the lifetime of the graph, so its GL state
    /// cache (bound framebuffer, viewport, scissor) stays warm across passes and
    /// across frames, and pass execution never allocates a context per call.
    /// </remarks>
    public GLRenderContext RenderContext => AliveStorage.RenderContext;

    /// <summary>
    /// Releases the graph: its passes, interned ids, per-frame resource scratch
    /// and the command pump's native ring memory.
    /// </summary>
    /// <remarks>
    /// Disposing any handle disposes the shared storage exactly once, so a
    /// building handle can own the lifetime even when execution happens through
    /// a compiled handle obtained from it. A default-constructed handle owns
    /// nothing and does nothing.
    /// </remarks>
    public void Dispose() => _storage?.Dispose();

    /// <summary>
    /// Resolves the storage, failing closed when the graph is gone.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The graph has been disposed.</exception>
    private FrameGraphStorage AliveStorage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            FrameGraphStorage? storage = _storage;
            if (storage is null || storage.IsDisposed)
            {
                ThrowHelper.ThrowObjectDisposed(GraphConstants.Name);
            }

            return storage;
        }
    }
}

#pragma warning restore CA1815
#pragma warning restore CA1001

/// <summary>
/// The building-phase leg of the frame graph: add passes, then compile.
/// </summary>
public static class FrameGraphBuildingExtensions
{
    /// <summary>
    /// Adds a render pass to the graph and assigns it the next
    /// <see cref="PassId"/>.
    /// </summary>
    /// <param name="graph">The building graph.</param>
    /// <param name="pass">The pass to add.</param>
    /// <returns>A handle on the same graph, for chaining.</returns>
    /// <remarks>
    /// The return value is optional: the handle is a view over shared storage, so
    /// <c>graph.AddPass(p);</c> mutates the graph the caller already holds.
    /// Declared resource names are interned to <see cref="ResourceId"/> here, in
    /// declaration order.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The graph has been disposed.</exception>
    public static FrameGraph<BuildingPhase, TPolicy> AddPass<TPolicy>(this FrameGraph<BuildingPhase, TPolicy> graph, IRenderPass pass)
        where TPolicy : struct, IGraphPolicy
    {
        RequireAlive(graph).AddPass(pass);
        return graph;
    }

    /// <summary>
    /// Builds a strongly-typed pass from its payload and adds it to the graph.
    /// </summary>
    /// <remarks>
    /// The pass phases are static abstracts on <typeparamref name="TPassData"/>,
    /// so this is a construction-and-add: the payload already knows how to
    /// declare its dependencies, validate itself and execute. The pass therefore
    /// cannot be registered with a missing or mismatched phase.
    /// <para><b>Type-argument arity.</b> Both type arguments belong to the
    /// method, because the policy is carried in the handle's type and the
    /// reduced extension form has no way to bind it any other way — and C# does
    /// not partially infer a trailing type argument from the receiver of a
    /// reduced extension call. So the plain call
    /// <c>graph.AddPass(name, kind, data)</c> infers both and is the normal
    /// spelling, and a call site that names the payload explicitly must name
    /// both: <c>graph.AddPass&lt;MyData, MyPolicy&gt;(name, kind, data)</c>.</para>
    /// </remarks>
    /// <typeparam name="TPassData">
    /// The pass payload data type, implementing the setup, validate and execute
    /// static-abstract phase interfaces.
    /// </typeparam>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="graph">The building graph.</param>
    /// <param name="name">The pass name.</param>
    /// <param name="kind">The pass kind classification.</param>
    /// <param name="data">The pass payload, stored by the pass and declared through its own setup phase.</param>
    /// <returns>A handle on the same graph, for chaining.</returns>
    public static FrameGraph<BuildingPhase, TPolicy> AddPass<TPassData, TPolicy>(
        this FrameGraph<BuildingPhase, TPolicy> graph,
        string name,
        FramePassKind kind,
        TPassData data)
        where TPolicy : struct, IGraphPolicy
        where TPassData : IPassSetup<TPassData>, IPassValidate<TPassData>, IPassExecute<TPassData>
    {
        RequireAlive(graph).AddPass(new RenderPass<TPassData>(name, kind, data));
        return graph;
    }

    /// <summary>
    /// Clears the graph for the next frame and returns it to the building phase.
    /// </summary>
    /// <param name="graph">The building graph.</param>
    /// <returns>A building handle on the same, now empty, graph.</returns>
    /// <remarks>
    /// The command pump is left alive and is <b>not</b> drained: commands queued
    /// but not yet pumped survive a reset and are still returned by
    /// <see cref="FrameGraphCompiledExtensions.PumpQueuedCommands{TPolicy}"/>.
    /// Only <see cref="FrameGraph{TPhase, TPolicy}.Dispose"/> releases the
    /// pump's native ring memory. Because resource and pass ids restart at zero,
    /// an id captured before the reset must not be used after it.
    /// </remarks>
    public static FrameGraph<BuildingPhase, TPolicy> Reset<TPolicy>(this FrameGraph<BuildingPhase, TPolicy> graph)
        where TPolicy : struct, IGraphPolicy
    {
        RequireAlive(graph).Clear();
        return graph;
    }

    /// <summary>
    /// Validates every enabled pass, resolves dependencies and topologically
    /// sorts the enabled passes, returning the graph in the compiled phase.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="graph">The building graph.</param>
    /// <returns>A compiled handle on the same graph.</returns>
    /// <remarks>
    /// <para>Compile runs on the mutation leg, not on the per-frame hot path:
    /// the result is cached in the graph's storage and reused by every
    /// subsequent execution. It reads live <see cref="IRenderPass.IsEnabled"/>
    /// values and calls <see cref="IRenderPass.Validate"/> on every enabled pass,
    /// so toggling a pass and re-adding it is the intended way to change the
    /// shape of a frame.</para>
    /// <para>A cycle is a policy decision, not an exception the graph invents:
    /// the totals are routed to <see cref="IGraphPolicy.OnCycleDetected"/>, which
    /// by default throws <see cref="RenderException"/> with
    /// <see cref="RenderErrorCode.GraphCycleDetected"/>. A policy that only
    /// observes does not unlock the graph — no partial order is ever cached, so
    /// the compile still fails closed, because a half-ordered graph renders a
    /// frame nobody asked for.</para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The graph has been disposed.</exception>
    public static FrameGraph<CompiledPhase, TPolicy> Compile<TPolicy>(this FrameGraph<BuildingPhase, TPolicy> graph)
        where TPolicy : struct, IGraphPolicy
    {
        FrameGraphStorage storage = RequireAlive(graph);

        if (!storage.TryCompile(out uint resolvedPasses, out uint totalPasses))
        {
            TPolicy.OnCycleDetected(totalPasses, resolvedPasses);

            // A policy that returns from the cycle hook has declined to throw.
            // The graph still refuses to produce an order: fail closed.
            ThrowHelper.Throw(
                RenderErrorCode.GraphCycleDetected,
                $"Frame graph contains cycles and cannot be executed ({resolvedPasses} of {totalPasses} passes ordered)",
                GraphConstants.Name);
        }

        return new FrameGraph<CompiledPhase, TPolicy>(storage);
    }

    /// <summary>
    /// Resolves a handle's storage or fails closed.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="graph">The building graph.</param>
    /// <returns>The live storage.</returns>
    /// <exception cref="ObjectDisposedException">The handle owns no storage, or the graph has been disposed.</exception>
    private static FrameGraphStorage RequireAlive<TPolicy>(FrameGraph<BuildingPhase, TPolicy> graph)
        where TPolicy : struct, IGraphPolicy
    {
        FrameGraphStorage? storage = graph.Storage;
        if (storage is null || storage.IsDisposed)
        {
            ThrowHelper.ThrowObjectDisposed(GraphConstants.Name);
        }

        return storage;
    }
}

/// <summary>
/// The compiled-phase leg of the frame graph: execute, pump and inspect the
/// compiled order.
/// </summary>
public static class FrameGraphCompiledExtensions
{
    /// <summary>
    /// Executes every ordered, enabled pass: pump-driven where the pass supports
    /// it, direct otherwise.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="graph">The compiled graph.</param>
    /// <remarks>
    /// <para>The walk is hybrid and strictly order-preserving. For every ordered,
    /// enabled pass:</para>
    /// <list type="bullet">
    /// <item><description>a pump-capable pass (<see cref="IPumpEnqueue"/>) has
    /// its commands enqueued into <see cref="FrameGraph{TPhase, TPolicy}.Pump"/>
    /// and moves on; the commands reach the GL API at the next drain point;</description></item>
    /// <item><description>a refused enqueue (ring full) drains the pump once and
    /// retries that enqueue exactly once;</description></item>
    /// <item><description>a pass still refused after the retry falls back to its
    /// direct execution in place, so it still runs exactly once and its GL
    /// effects stay inside the global order;</description></item>
    /// <item><description>a classic pass drains the pump first — pumped work
    /// enqueued by earlier passes must be applied before a direct pass reads its
    /// inputs — and then executes directly.</description></item>
    /// </list>
    /// <para>One final drain closes the walk, so no command enqueued here
    /// outlives the call. Order is preserved because the ring is FIFO and
    /// enqueues follow the compiled order, so pumped work can never overtake an
    /// earlier pass, and the drain-before-direct rule keeps direct work behind
    /// everything enqueued before it.</para>
    /// <para><b>Policy hooks.</b> Every pass that runs is bracketed by
    /// <see cref="IGraphPolicy.OnPassExecuting"/> and
    /// <see cref="IGraphPolicy.OnPassExecuted"/>. For a pump leg the pair
    /// brackets the enqueue, which is where that pass's work enters the
    /// pipeline; a pass that is disabled, or whose leg failed, fires only the
    /// first hook. The hooks are inside the fault-isolated unit, so a policy that
    /// throws fails the walk exactly like a failing pass does.</para>
    /// <para><b>Fault policy (fail-closed, per pass).</b> The pump-enqueue
    /// attempt, the refusal drain-and-retry, the direct fallback and the drains
    /// issued inside the walk are one fault-isolated unit: the first exception
    /// aborts the whole walk. The graph-owned
    /// <see cref="FrameGraph{TPhase, TPolicy}.Pump"/> is marked faulted through
    /// <c>Pump.Fault</c> (which also fires the pump's fault telemetry and never
    /// throws), so every later enqueue is refused and no producer has to branch
    /// on the fault. The walk is deliberately not drained on the way out: GL
    /// state is unknown once a pass has failed, so commands queued but not yet
    /// pumped are preserved in the ring (the documented reset semantics) instead
    /// of being executed out of order. The original exception is rethrown through
    /// a captured <see cref="ExceptionDispatchInfo"/>, so its type, message and
    /// stack survive the isolation boundary; a direct leg is wrapped by
    /// <see cref="IGraphPolicy.OnPassError"/>, whose default is a pass-identity
    /// <see cref="RenderException"/> with
    /// <see cref="RenderErrorCode.PassExecutionFailed"/> and the original
    /// exception inside. A policy that only observes the error does not suppress
    /// it: the graph still throws, because GL state after a failed pass is not
    /// something a frame may continue from.</para>
    /// <para><b>Known limitations, unchanged.</b> An exception raised by the
    /// render command processor surfaces from the drain that runs the command,
    /// not from the pass that enqueued it, so it carries no pass identity and is
    /// not wrapped — the pump already contains and marks it. Retry granularity
    /// is per pass, not per command: a pass refused part-way through re-runs its
    /// whole enqueue after the drain, so the commands already accepted execute
    /// twice. The shipped single-command pump passes are unaffected.</para>
    /// <para><b>Execution seam.</b> Every direct leg runs through
    /// <see cref="IRenderPass.Execute"/> on the graph-owned
    /// <see cref="FrameGraph{TPhase, TPolicy}.RenderContext"/>. All passes are
    /// <see cref="IRenderContext"/>-native: they consume the shared context
    /// directly, keeping its state cache warm for the whole walk. There is no
    /// legacy bridge.</para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The graph has been disposed.</exception>
    public static void Execute<TPolicy>(this FrameGraph<CompiledPhase, TPolicy> graph)
        where TPolicy : struct, IGraphPolicy
    {
        ExecuteCore<TPolicy>(RequireAlive(graph));
    }

    /// <summary>
    /// Enqueues the work of every ordered, enabled pass that implements
    /// <see cref="IPumpEnqueue"/>, in topological order, without executing
    /// anything.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="graph">The compiled graph.</param>
    /// <returns>The number of enqueues the pump accepted.</returns>
    /// <remarks>
    /// Non-pump passes are skipped silently; their path is the direct
    /// <see cref="Execute{TPolicy}"/> traversal. A pump pass whose enqueue is
    /// refused (ring full) is not counted and does not stop later passes;
    /// enqueue failures thrown by a pass propagate to the caller unchanged. The
    /// policy hooks do not fire here: this entry point enqueues, it does not
    /// execute, and bracketing a pass that has not run would misreport it.
    /// </remarks>
    public static uint EnqueuePumpPasses<TPolicy>(this FrameGraph<CompiledPhase, TPolicy> graph)
        where TPolicy : struct, IGraphPolicy
    {
        FrameGraphStorage storage = RequireAlive(graph);

        uint enqueued = 0;
        ReadOnlySpan<PassId> order = storage.Order;
        for (int i = 0; i < order.Length; i++)
        {
            IRenderPass pass = storage.GetPass(order[i]);
            if (!pass.IsEnabled)
            {
                continue;
            }

            if (pass is IPumpEnqueue pumpPass && pumpPass.EnqueueCommands(storage.Pump, storage.ExecutionContext).IsEnqueued)
            {
                enqueued++;
            }
        }

        return enqueued;
    }

    /// <summary>
    /// Pumps every command currently queued in the graph's pump through the
    /// render command processor.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="graph">The compiled graph.</param>
    /// <returns>The number of commands processed.</returns>
    /// <remarks>
    /// Single-consumer: exactly one thread may pump, and it is the render thread.
    /// Processor exceptions propagate to the caller; the faulting command stays
    /// claimed so a retry observes the same head of queue.
    /// </remarks>
    public static uint PumpQueuedCommands<TPolicy>(this FrameGraph<CompiledPhase, TPolicy> graph)
        where TPolicy : struct, IGraphPolicy
    {
        FrameGraphStorage storage = RequireAlive(graph);
        return storage.PumpQueuedCommands();
    }

    /// <summary>
    /// Returns the compiled emission order.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="graph">The compiled graph.</param>
    /// <returns>
    /// The ordered pass ids, or an empty span for a graph with no enabled pass.
    /// The span points into the graph's cached order and stays valid until the
    /// next compile or reset; copy it to keep it across either.
    /// </returns>
    /// <remarks>
    /// The order is a function of the compiled pass set alone, so it is
    /// deterministic: writers ascending, each writer's writes in declaration
    /// order, each resource's readers ascending, ready queue seeded ascending
    /// and drained FIFO. Two compiles of the same pass set produce the same
    /// order.
    /// </remarks>
    public static ReadOnlySpan<PassId> GetTopologicalOrder<TPolicy>(this FrameGraph<CompiledPhase, TPolicy> graph)
        where TPolicy : struct, IGraphPolicy
    {
        FrameGraphStorage storage = RequireAlive(graph);
        return storage.Order;
    }

    /// <summary>
    /// Clears the compiled graph and returns it to the building phase, ready
    /// for the next frame's pass set.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="graph">The compiled graph.</param>
    /// <returns>A building handle on the same, now empty, graph.</returns>
    /// <remarks>
    /// The pump is left alive and is not drained, exactly as in the building
    /// phase: queued-but-unpumped commands survive the reset. The compiled order
    /// is dropped, and pass and resource ids restart at zero, so an id captured
    /// from <see cref="GetTopologicalOrder{TPolicy}"/> must not outlive the
    /// reset.
    /// </remarks>
    public static FrameGraph<BuildingPhase, TPolicy> Reset<TPolicy>(this FrameGraph<CompiledPhase, TPolicy> graph)
        where TPolicy : struct, IGraphPolicy
    {
        FrameGraphStorage? storage = graph.Storage;
        if (storage is null || storage.IsDisposed)
        {
            ThrowHelper.ThrowObjectDisposed(GraphConstants.Name);
        }

        storage.Clear();
        return new FrameGraph<BuildingPhase, TPolicy>(storage);
    }

    /// <summary>
    /// The single walk: order-preserving, pump-hybrid, fault-isolated per pass.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="storage">The live graph storage.</param>
    private static void ExecuteCore<TPolicy>(FrameGraphStorage storage)
        where TPolicy : struct, IGraphPolicy
    {
        // Allocation-free: locals only, no LINQ, no closures; pump legs and
        // direct legs keep the same position. Each iteration is one
        // fault-isolated unit: a failure faults the pump and aborts the walk
        // instead of leaking GL work.
        ReadOnlySpan<PassId> order = storage.Order;
        for (int i = 0; i < order.Length; i++)
        {
            PassId passId = order[i];
            IRenderPass pass = storage.GetPass(passId);

            if (!pass.IsEnabled)
            {
                continue;
            }

            var metadata = new PassMetadata(passId, pass.Name);
            try
            {
                TPolicy.OnPassExecuting(passId, ref metadata);
                RunPass<TPolicy>(storage, passId, pass);
                TPolicy.OnPassExecuted(passId, ref metadata);
            }
#pragma warning disable CA1031 // Fail-closed by policy: the pump is marked Faulted and the original exception, not a wrapped one, reaches the caller.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                // Fail closed: the pump stops accepting work for the rest of the
                // graph's life, the ring is left intact (never drained here, GL
                // state is unknown), and the original stack reaches the caller
                // untouched.
                storage.Pump.Fault(ex);
                ExceptionDispatchInfo.Capture(ex).Throw();
                throw; // Fault + captured rethrow above never return; this ends the loop for the compiler too.
            }
        }

        // Final drain: nothing enqueued by this walk survives the call.
        storage.PumpQueuedCommands();
    }

    /// <summary>
    /// Runs one pass on whichever leg it supports, in the position the walk gave
    /// it.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="storage">The live graph storage.</param>
    /// <param name="passId">The pass id, for the policy's failure hook.</param>
    /// <param name="pass">The pass to run.</param>
    private static void RunPass<TPolicy>(FrameGraphStorage storage, PassId passId, IRenderPass pass)
        where TPolicy : struct, IGraphPolicy
    {
        if (pass is IPumpEnqueue pumpPass)
        {
            if (pumpPass.EnqueueCommands(storage.Pump, storage.ExecutionContext).IsEnqueued)
            {
                // Pump leg: the commands execute at the next drain point, in
                // enqueue order, which is graph order.
                return;
            }

            // Refused (ring full): drain once. The pending pumped work lands in
            // graph order and the freed slots give this pass a second chance.
            storage.PumpQueuedCommands();
            if (pumpPass.EnqueueCommands(storage.Pump, storage.ExecutionContext).IsEnqueued)
            {
                return;
            }

            // Still refused: take the direct leg here rather than skipping the
            // pass, so its GL effects stay inside the global order.
            ExecuteDirect<TPolicy>(storage, passId, pass);
            return;
        }

        // Classic pass: drain-before-direct, so pumped work enqueued by
        // earlier passes is already applied when this pass reads its inputs.
        storage.PumpQueuedCommands();
        ExecuteDirect<TPolicy>(storage, passId, pass);
    }

    /// <summary>
    /// Executes one pass on the direct path through the graph-owned render
    /// context, routing a failure to the policy and failing closed with a
    /// pass-identity <see cref="RenderException"/>.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="storage">The live graph storage.</param>
    /// <param name="passId">The pass id, for the policy's failure hook.</param>
    /// <param name="pass">The pass to execute.</param>
    private static void ExecuteDirect<TPolicy>(FrameGraphStorage storage, PassId passId, IRenderPass pass)
        where TPolicy : struct, IGraphPolicy
    {
        try
        {
            pass.Execute(storage.RenderContext);
        }
#pragma warning disable CA1031 // Pass failures are wrapped with pass identity; the type is preserved
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // The policy decides what the caller sees. By default that is the
            // pass-identity RenderException(PassExecutionFailed) carrying the
            // original as its inner exception.
            TPolicy.OnPassError(passId, ex);

            // A policy that only observes the error does not suppress it: the
            // walk still fails closed, this time with the pass name attached.
            throw new RenderException($"Pass '{pass.Name}' failed: {ex.Message}", RenderErrorCode.PassExecutionFailed, ex);
        }
    }

    /// <summary>
    /// Resolves a handle's storage or fails closed.
    /// </summary>
    /// <typeparam name="TPolicy">The compile-time policy.</typeparam>
    /// <param name="graph">The compiled graph.</param>
    /// <returns>The live storage.</returns>
    /// <exception cref="ObjectDisposedException">The handle owns no storage, or the graph has been disposed.</exception>
    private static FrameGraphStorage RequireAlive<TPolicy>(FrameGraph<CompiledPhase, TPolicy> graph)
        where TPolicy : struct, IGraphPolicy
    {
        FrameGraphStorage? storage = graph.Storage;
        if (storage is null || storage.IsDisposed)
        {
            ThrowHelper.ThrowObjectDisposed(GraphConstants.Name);
        }

        return storage;
    }
}
