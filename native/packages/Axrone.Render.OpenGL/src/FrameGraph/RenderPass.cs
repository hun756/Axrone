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
/// Delegate for the setup phase of a render pass.
/// Declares resource dependencies, the render target descriptor and load/store actions,
/// and initializes the pass payload.
/// </summary>
public delegate void RenderPassSetupDelegate<TPassData>(IRenderPassBuilder builder, ref TPassData data) where TPassData : struct;

/// <summary>
/// Delegate for the execution phase of a render pass.
/// Receives the graph-owned render context (warm state cache) plus the pass execution
/// context for resource resolution. Raw GL remains reachable through
/// <see cref="PassExecutionContext.Context"/> for operations the hardware-agnostic
/// <see cref="IRenderContext"/> verbs do not cover (SSBO/image bindings, matrix uploads,
/// mesh draws).
/// </summary>
public delegate void RenderPassExecuteDelegate<TPassData>(in TPassData data, IRenderContext context, PassExecutionContext ctx) where TPassData : struct;

/// <summary>
/// Delegate for the validation phase of a render pass.
/// Enforces pass-specific pre-conditions (disposed programs, value ranges, wiring).
/// </summary>
public delegate void RenderPassValidateDelegate<TPassData>(in TPassData data) where TPassData : struct;

/// <summary>
/// Delegate for the pump-enqueue phase of a pump-capable render pass.
/// Resolves resources from the execution context at enqueue time and must not retain
/// the pump or the receipt beyond the call.
/// </summary>
public delegate EnqueueResult RenderPassPumpDelegate<TPassData>(in TPassData data, RenderPump pump, PassExecutionContext ctx) where TPassData : struct;

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
/// pass payload data plus setup / execute / validate delegates, executed
/// <see cref="IRenderContext"/>-native on the graph-owned <see cref="GLRenderContext"/>
/// so the state cache stays warm across passes and execution allocates nothing per pass.
/// </summary>
/// <remarks>
/// <para>Per-frame mutation goes through <see cref="Data"/> (a mutable ref): update fields
/// such as view-projection matrices, intensities or time seeds before the graph executes.
/// Reference-type payloads inside the struct (for example mesh lists) stay shared.</para>
/// <para>Pump-capable passes use <see cref="PumpRenderPass{TPassData}"/> instead; this type
/// never touches the command pump and always takes the direct leg.</para>
/// </remarks>
/// <typeparam name="TPassData">The struct payload type storing pass inputs and parameters.</typeparam>
public sealed class RenderPass<TPassData> : IRenderPass where TPassData : struct
{
    private TPassData _data;
    private readonly RenderPassExecuteDelegate<TPassData> _execute;
    private readonly RenderPassValidateDelegate<TPassData>? _validate;
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

    /// <summary>Initializes a render pass with setup, execution and optional validation delegates.</summary>
    public RenderPass(
        string name,
        FramePassKind kind,
        RenderPassSetupDelegate<TPassData> setup,
        RenderPassExecuteDelegate<TPassData> execute,
        RenderPassValidateDelegate<TPassData>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(execute);

        Name = name;
        Kind = kind;
        _execute = execute;
        _validate = validate;

        var builder = new RenderPassBuilder();
        setup(builder, ref _data);

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

            _execute(in _data, glCtx, glCtx.PassContext);
        }
        else
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, $"Execution of {GetType().Name} requires a GLRenderContext backend.", nameof(RenderPass<TPassData>));
        }
    }

    /// <inheritdoc/>
    public void Validate() => _validate?.Invoke(in _data);

    /// <inheritdoc/>
    public override string ToString() => $"RenderPass: \"{Name}\" ({Kind}), Enabled={IsEnabled}";
}

/// <summary>
/// Pump-capable render pass. Same single execution path as <see cref="RenderPass{TPassData}"/>,
/// plus an <see cref="IPumpEnqueue"/> leg that expresses the work as render pump commands.
/// When the enqueue is refused (ring full) the frame graph falls back to the direct leg,
/// so the pass still runs exactly once and its GL effects stay inside the global order.
/// </summary>
/// <typeparam name="TPassData">The struct payload type storing pass inputs and parameters.</typeparam>
public sealed class PumpRenderPass<TPassData> : IRenderPass, IPumpEnqueue where TPassData : struct
{
    private TPassData _data;
    private readonly RenderPassExecuteDelegate<TPassData> _execute;
    private readonly RenderPassValidateDelegate<TPassData>? _validate;
    private readonly RenderPassPumpDelegate<TPassData> _pump;
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

    /// <summary>Initializes a pump-capable render pass.</summary>
    public PumpRenderPass(
        string name,
        FramePassKind kind,
        RenderPassSetupDelegate<TPassData> setup,
        RenderPassExecuteDelegate<TPassData> execute,
        RenderPassPumpDelegate<TPassData> pump,
        RenderPassValidateDelegate<TPassData>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(pump);

        Name = name;
        Kind = kind;
        _execute = execute;
        _pump = pump;
        _validate = validate;

        var builder = new RenderPassBuilder();
        setup(builder, ref _data);

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

            _execute(in _data, glCtx, glCtx.PassContext);
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
        return _pump(in _data, pump, ctx);
    }

    /// <inheritdoc/>
    public void Validate() => _validate?.Invoke(in _data);

    /// <inheritdoc/>
    public override string ToString() => $"RenderPass: \"{Name}\" ({Kind}), Enabled={IsEnabled}";
}
