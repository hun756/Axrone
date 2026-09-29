using Axrone.Execution;
using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.FrameGraph;


/// <summary>
/// Classifies the type of a render pass for frame graph scheduling and optimization.
/// </summary>
public enum FramePassKind
{
    /// <summary>Framebuffer clear pass.</summary>
    Clear,

    /// <summary>Opaque geometry rendering pass.</summary>
    Opaque,

    /// <summary>Transparent geometry rendering pass.</summary>
    Transparent,

    /// <summary>Shadow map rendering pass.</summary>
    Shadow,

    /// <summary>General post-processing pass.</summary>
    PostProcess,

    /// <summary>Bloom effect pass (bright-pass + blur + composite).</summary>
    Bloom,

    /// <summary>HDR to LDR tone mapping pass.</summary>
    ToneMap,

    /// <summary>FXAA anti-aliasing pass.</summary>
    Fxaa,

    /// <summary>GPU compute shader dispatch pass.</summary>
    Compute,

    /// <summary>Fullscreen quad/triangle rendering pass.</summary>
    FullscreenQuad,

    /// <summary>Framebuffer blit (copy) pass.</summary>
    Blit,

    /// <summary>Depth-only pre-pass that primes the depth buffer for early-Z.</summary>
    DepthPrepass,

    /// <summary>Skybox/background rendering pass.</summary>
    Skybox,

    /// <summary>Presentation pass (blit to default framebuffer).</summary>
    Present,

    /// <summary>User-defined custom pass.</summary>
    Custom,
}

/// <summary>
/// Implementation of <see cref="IRenderPassBuilder"/> that collects pass configuration.
/// </summary>
internal sealed class RenderPassBuilder : IRenderPassBuilder
{
    private readonly List<string> _reads = new(4);
    private readonly List<string> _writes = new(4);

    public RenderPassDescriptor Descriptor { get; private set; }
    public AttachmentLoadAction LoadAction { get; private set; } = AttachmentLoadAction.Load;
    public AttachmentStoreAction StoreAction { get; private set; } = AttachmentStoreAction.Store;

    public IReadOnlyList<string> ReadsList => _reads;
    public IReadOnlyList<string> WritesList => _writes;

    public void Reads(string resourceName) => _reads.Add(resourceName);
    public void Writes(string resourceName) => _writes.Add(resourceName);
    public void SetDescriptor(in RenderPassDescriptor descriptor) => Descriptor = descriptor;
    public void SetLoadAction(AttachmentLoadAction action) => LoadAction = action;
    public void SetStoreAction(AttachmentStoreAction action) => StoreAction = action;
}

/// <summary>
/// Strongly-typed render pass. The single canonical pass implementation in the frame graph:
/// a pass payload that owns its own setup / validate / execute phases, executed
/// <see cref="IRenderContext"/>-native on the graph-owned <see cref="GLRenderContext"/>
/// so the state cache stays warm across passes and execution allocates nothing per pass.
/// </summary>
/// <remarks>
/// <para>The phase protocol is static-abstract (see <see cref="IPassSetup{TSelf}"/>,
/// <see cref="IPassValidate{TSelf}"/> and <see cref="IPassExecute{TSelf}"/>), so a pass
/// cannot be constructed with a missing, null or mismatched phase: the constraint below
/// turns every one of those into a compile error, and there is no per-frame branch to
/// check. <c>CustomPass</c> remains the documented callback-based escape hatch.</para>
/// <para>Per-frame mutation goes through <see cref="Data"/> (a mutable ref): update fields
/// such as view-projection matrices, intensities or time seeds before the graph executes.
/// Reference-type payloads inside the struct (for example mesh lists) stay shared.</para>
/// <para>Pump-capable passes use <see cref="PumpRenderPass{TPassData}"/> instead; this type
/// never touches the command pump and always takes the direct leg.</para>
/// </remarks>
/// <typeparam name="TPassData">
/// The payload type storing pass inputs and parameters and implementing the three
/// static-abstract phase interfaces.
/// </typeparam>
public sealed class RenderPass<TPassData> : IRenderPass
    where TPassData : IPassSetup<TPassData>, IPassValidate<TPassData>, IPassExecute<TPassData>
{
    private TPassData _data;
    private readonly string[] _reads;
    private readonly string[] _writes;

    /// <summary>Gets the pass name.</summary>
    public string Name { get; }

    /// <summary>Gets the pass kind for scheduling classification.</summary>
    public FramePassKind Kind { get; }

    /// <summary>Gets or sets a value indicating whether this pass is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets the hardware-agnostic descriptor describing targets and attachments.</summary>
    public RenderPassDescriptor Descriptor { get; }

    /// <summary>Gets how this pass's render target attachments must be loaded.</summary>
    public AttachmentLoadAction LoadAction { get; }

    /// <summary>Gets how this pass's render target attachments must be stored.</summary>
    public AttachmentStoreAction StoreAction { get; }

    /// <summary>Gets a mutable reference to the pass payload. Update per frame before execution.</summary>
    public ref TPassData Data => ref _data;

    /// <summary>Initializes a render pass from a fully built payload.</summary>
    /// <param name="name">The pass name.</param>
    /// <param name="kind">The pass kind classification.</param>
    /// <param name="data">
    /// The pass payload. It is stored as-is and then handed to
    /// <see cref="IPassSetup{TSelf}.Declare"/> so the pass can snapshot its own
    /// dependencies, descriptor and attachment actions.
    /// </param>
    public RenderPass(string name, FramePassKind kind, TPassData data)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
        Kind = kind;
        _data = data;

        var builder = new RenderPassBuilder();
        TPassData.Declare(builder, ref _data);

        Descriptor = builder.Descriptor;
        LoadAction = builder.LoadAction;
        StoreAction = builder.StoreAction;
        _reads = builder.ReadsList.ToArray();
        _writes = builder.WritesList.ToArray();
    }

    /// <inheritdoc/>
    public ReadOnlySpan<string> GetReadResources() => _reads;

    /// <inheritdoc/>
    public ReadOnlySpan<string> GetWrittenResources() => _writes;

    /// <inheritdoc/>
    void IRenderPass.Execute(IRenderContext context)
    {
        if (context is GLRenderContext glCtx)
        {
            if (glCtx.PassContext is null)
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "PassContext is not configured on the GLRenderContext.", nameof(RenderPass<TPassData>));
            }

            TPassData.Execute(in _data, glCtx, glCtx.PassContext);
        }
        else
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, $"Execution of {GetType().Name} requires a GLRenderContext backend.", nameof(RenderPass<TPassData>));
        }
    }

    /// <inheritdoc/>
    public void Validate() => TPassData.Validate(in _data);

    /// <inheritdoc/>
    public override string ToString() => $"RenderPass: \"{Name}\" ({Kind}), Enabled={IsEnabled}";
}

/// <summary>
/// Pump-capable render pass. Same single execution path as <see cref="RenderPass{TPassData}"/>,
/// plus an <see cref="IPumpEnqueue"/> leg that expresses the work as render pump commands.
/// When the enqueue is refused (ring full) the frame graph falls back to the direct leg,
/// so the pass still runs exactly once and its GL effects stay inside the global order.
/// </summary>
/// <remarks>
/// All four phases (setup, validate, execute, enqueue) are static abstracts on the
/// payload, so a pump pass cannot be built with a missing phase and the enqueue leg
/// is guaranteed to exist whenever the pass declares itself pump-capable.
/// </remarks>
/// <typeparam name="TPassData">
/// The payload type storing pass inputs and parameters and implementing the four
/// static-abstract phase interfaces.
/// </typeparam>
public sealed class PumpRenderPass<TPassData> : IRenderPass, IPumpEnqueue
    where TPassData : IPassSetup<TPassData>, IPassValidate<TPassData>, IPassExecute<TPassData>, IPassEnqueue<TPassData>
{
    private TPassData _data;
    private readonly string[] _reads;
    private readonly string[] _writes;

    /// <summary>Gets the pass name.</summary>
    public string Name { get; }

    /// <summary>Gets the pass kind for scheduling classification.</summary>
    public FramePassKind Kind { get; }

    /// <summary>Gets or sets a value indicating whether this pass is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets the hardware-agnostic descriptor describing targets and attachments.</summary>
    public RenderPassDescriptor Descriptor { get; }

    /// <summary>Gets how this pass's render target attachments must be loaded.</summary>
    public AttachmentLoadAction LoadAction { get; }

    /// <summary>Gets how this pass's render target attachments must be stored.</summary>
    public AttachmentStoreAction StoreAction { get; }

    /// <summary>Gets a mutable reference to the pass payload. Update per frame before execution.</summary>
    public ref TPassData Data => ref _data;

    /// <summary>Initializes a pump-capable render pass from a fully built payload.</summary>
    /// <param name="name">The pass name.</param>
    /// <param name="kind">The pass kind classification.</param>
    /// <param name="data">
    /// The pass payload. It is stored as-is and then handed to
    /// <see cref="IPassSetup{TSelf}.Declare"/> so the pass can snapshot its own
    /// dependencies, descriptor and attachment actions.
    /// </param>
    public PumpRenderPass(string name, FramePassKind kind, TPassData data)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
        Kind = kind;
        _data = data;

        var builder = new RenderPassBuilder();
        TPassData.Declare(builder, ref _data);

        Descriptor = builder.Descriptor;
        LoadAction = builder.LoadAction;
        StoreAction = builder.StoreAction;
        _reads = builder.ReadsList.ToArray();
        _writes = builder.WritesList.ToArray();
    }

    /// <inheritdoc/>
    public ReadOnlySpan<string> GetReadResources() => _reads;

    /// <inheritdoc/>
    public ReadOnlySpan<string> GetWrittenResources() => _writes;

    /// <inheritdoc/>
    void IRenderPass.Execute(IRenderContext context)
    {
        if (context is GLRenderContext glCtx)
        {
            if (glCtx.PassContext is null)
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "PassContext is not configured on the GLRenderContext.", nameof(PumpRenderPass<TPassData>));
            }

            TPassData.Execute(in _data, glCtx, glCtx.PassContext);
        }
        else
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, $"Execution of {GetType().Name} requires a GLRenderContext backend.", nameof(PumpRenderPass<TPassData>));
        }
    }

    /// <inheritdoc/>
    public EnqueueResult EnqueueCommands(RenderPump pump, PassExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(pump);
        ArgumentNullException.ThrowIfNull(ctx);
        return TPassData.EnqueueCommands(in _data, pump, ctx);
    }

    /// <inheritdoc/>
    public void Validate() => TPassData.Validate(in _data);

    /// <inheritdoc/>
    public override string ToString() => $"RenderPass: \"{Name}\" ({Kind}), Enabled={IsEnabled}";
}
