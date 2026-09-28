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
/// Base class for all render passes in the frame graph.
/// Each pass declares its resource reads/writes and implements execution logic.
/// </summary>
/// <remarks>
/// <para>Execution contract: the frame graph executes every pass through
/// <see cref="IRenderPass.Execute(IRenderContext)"/> on its graph-owned
/// <see cref="GLRenderContext"/>. GL-specific subclasses override the legacy bridge
/// <see cref="Execute(Context.GLContext, PassExecutionContext)"/>, which the interface
/// implementation adapts to; <see cref="IRenderContext"/>-native pass types implement
/// the interface directly and leave the bridge unimplemented. Subclasses may override
/// <see cref="Validate"/> to enforce pre-conditions. The constructor must call
/// <see cref="Reads"/> and <see cref="Writes"/> to declare resource dependencies
/// for the frame graph scheduler.</para>
/// <para>Attachment load/store metadata (<see cref="LoadAction"/>,
/// <see cref="StoreAction"/>) is opt-in and is declared from the subclass
/// constructor via <see cref="DeclaresLoadAction"/> and
/// <see cref="DeclaresStoreAction"/>. The defaults (<see cref="AttachmentLoadAction.Load"/>
/// / <see cref="AttachmentStoreAction.Store"/>) exactly preserve the previous
/// behavior. The scheduler does not consume the metadata yet, so declaring it has no
/// effect on ordering or execution.</para>
/// </remarks>
public abstract class RenderPass : IRenderPass
{
    private readonly List<string> _reads = new();
    private readonly List<string> _writes = new();
    private string[]? _readsSnapshot;
    private string[]? _writesSnapshot;

    /// <summary>Gets the pass name.</summary>
    public string Name { get; }

    /// <summary>Gets the pass kind for scheduling classification.</summary>
    public FramePassKind Kind { get; }

    /// <summary>Gets or sets a value indicating whether this pass is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets the hardware-agnostic descriptor describing targets and attachments.</summary>
    public RenderPassDescriptor Descriptor { get; protected set; }

    /// <summary>
    /// Gets how this pass's render target attachments must be loaded.
    /// Defaults to <see cref="AttachmentLoadAction.Load"/>. Settable only from the
    /// subclass constructor via <see cref="DeclaresLoadAction"/>.
    /// </summary>
    public AttachmentLoadAction LoadAction
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    } = AttachmentLoadAction.Load;

    /// <summary>
    /// Gets how this pass's render target attachments must be stored.
    /// Defaults to <see cref="AttachmentStoreAction.Store"/>. Settable only from the
    /// subclass constructor via <see cref="DeclaresStoreAction"/>.
    /// </summary>
    public AttachmentStoreAction StoreAction
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    } = AttachmentStoreAction.Store;

    /// <summary>Gets the resource names this pass reads.</summary>
    public ReadOnlySpan<string> GetReadResources()
    {
        _readsSnapshot ??= _reads.ToArray();
        return _readsSnapshot;
    }

    /// <summary>Gets the resource names this pass writes.</summary>
    public ReadOnlySpan<string> GetWrittenResources()
    {
        _writesSnapshot ??= _writes.ToArray();
        return _writesSnapshot;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderPass"/> class.
    /// </summary>
    /// <param name="name">The pass name. Must not be null or empty.</param>
    /// <param name="kind">The pass kind.</param>
    protected RenderPass(string name, FramePassKind kind)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Kind = kind;
    }

    /// <summary>
    /// Declares that this pass reads the specified resource.
    /// </summary>
    /// <param name="resourceName">The resource name in the pass context.</param>
    protected void Reads(string resourceName)
    {
        _reads.Add(resourceName);
        _readsSnapshot = null;
    }

    /// <summary>
    /// Declares that this pass writes the specified resource.
    /// </summary>
    /// <param name="resourceName">The resource name in the pass context.</param>
    protected void Writes(string resourceName)
    {
        _writes.Add(resourceName);
        _writesSnapshot = null;
    }

    /// <summary>
    /// Declares how this pass's render target attachments must be loaded.
    /// Call from the subclass constructor. Opt-in metadata: it does not change
    /// scheduling or execution, and the default preserves existing behavior.
    /// </summary>
    /// <param name="action">The declared load action.</param>
    protected void DeclaresLoadAction(AttachmentLoadAction action) => LoadAction = action;

    /// <summary>
    /// Declares how this pass's render target attachments must be stored.
    /// Call from the subclass constructor. Opt-in metadata: it does not change
    /// scheduling or execution, and the default preserves existing behavior.
    /// </summary>
    /// <param name="action">The declared store action.</param>
    protected void DeclaresStoreAction(AttachmentStoreAction action) => StoreAction = action;

    internal void DeclareRead(string resourceName) => Reads(resourceName);
    internal void DeclareWrite(string resourceName) => Writes(resourceName);
    internal void DeclareLoadAction(AttachmentLoadAction action) => DeclaresLoadAction(action);
    internal void DeclareStoreAction(AttachmentStoreAction action) => DeclaresStoreAction(action);

    /// <summary>
    /// Legacy OpenGL execution bridge: issues this pass's work directly against a
    /// <see cref="Context.GLContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>The frame graph no longer calls this method. It executes every pass
    /// through <see cref="IRenderPass.Execute(IRenderContext)"/> on the graph-owned
    /// <see cref="GLRenderContext"/>, and the interface implementation below adapts
    /// that call back into this bridge for GL-specific subclasses.</para>
    /// <para>Pass types that are <see cref="IRenderContext"/>-native (such as
    /// <see cref="GenericRenderPass{TPassData}"/>) do not override this bridge; the
    /// base implementation fails closed so a legacy call can never silently execute
    /// a pass on a context whose state cache the pass does not share.</para>
    /// </remarks>
    /// <param name="context">The GL context for issuing draw calls.</param>
    /// <param name="ctx">The pass execution context for resource resolution.</param>
    public virtual void Execute(Context.GLContext context, PassExecutionContext ctx)
    {
        ThrowHelper.Throw(
            RenderErrorCode.InvalidOperation,
            $"{GetType().Name} has no legacy GLContext execution bridge; execute it through IRenderContext.",
            nameof(RenderPass));
    }

    /// <inheritdoc/>
    void IRenderPass.Execute(IRenderContext context)
    {
        if (context is GLRenderContext glCtx)
        {
            if (glCtx.PassContext is null)
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "PassContext is not configured on the GLRenderContext.", nameof(RenderPass));
            }

            Execute(glCtx.GLContext, glCtx.PassContext);
        }
        else
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, $"Execution of {GetType().Name} requires a GLRenderContext backend.", nameof(RenderPass));
        }
    }

    /// <summary>
    /// Validates the pass configuration. Called before execution to catch
    /// misconfigurations early. Override to add pass-specific validation.
    /// </summary>
    public virtual void Validate() { }


    /// <inheritdoc/>
    public override string ToString() => $"RenderPass: \"{Name}\" ({Kind}), Enabled={IsEnabled}";
}
