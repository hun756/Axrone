namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// Delegate for the setup phase of a generic render pass.
/// </summary>
public delegate void RenderPassSetupDelegate<TPassData>(IRenderPassBuilder builder, ref TPassData data) where TPassData : struct;

/// <summary>
/// Delegate for the execution phase of a generic render pass.
/// </summary>
public delegate void RenderPassExecuteDelegate<TPassData>(in TPassData data, IRenderContext context, PassExecutionContext ctx) where TPassData : struct;

/// <summary>
/// Implementation of <see cref="IRenderPassBuilder"/> that configures a pass.
/// </summary>
internal sealed class RenderPassBuilder : IRenderPassBuilder
{
    private readonly RenderPass _pass;

    public RenderPassDescriptor Descriptor { get; private set; }
    public AttachmentLoadAction LoadAction { get; private set; } = AttachmentLoadAction.Load;
    public AttachmentStoreAction StoreAction { get; private set; } = AttachmentStoreAction.Store;

    public RenderPassBuilder(RenderPass pass)
    {
        _pass = pass;
    }

    public void Reads(string resourceName) => _pass.DeclareRead(resourceName);
    public void Writes(string resourceName) => _pass.DeclareWrite(resourceName);
    public void SetDescriptor(in RenderPassDescriptor descriptor) => Descriptor = descriptor;
    public void SetLoadAction(AttachmentLoadAction action) => LoadAction = action;
    public void SetStoreAction(AttachmentStoreAction action) => StoreAction = action;
}

/// <summary>
/// Strongly-typed generic render pass. Decouples pass payload data, resource setup,
/// and hardware-agnostic execution logic without requiring concrete pass subclassing.
/// </summary>
/// <remarks>
/// <para>This pass type is <see cref="IRenderContext"/>-native: the execute delegate
/// receives the render context supplied by the caller — inside a
/// <see cref="FrameGraph"/> that is the single graph-owned
/// <see cref="GLRenderContext"/>, so the context's state cache stays warm across
/// passes and execution allocates nothing per pass.</para>
/// <para>The legacy <c>Execute(GLContext, PassExecutionContext)</c> bridge is
/// deliberately left unimplemented; the base class fails closed instead of wrapping
/// the raw context in a throwaway render context with a cold state cache.</para>
/// </remarks>
/// <typeparam name="TPassData">The unmanaged or struct payload type storing pass inputs and parameters.</typeparam>
public sealed class GenericRenderPass<TPassData> : RenderPass, IRenderPass where TPassData : struct
{
    private TPassData _data;
    private readonly RenderPassExecuteDelegate<TPassData> _execute;

    /// <summary>Gets a reference to the pass data.</summary>
    public ref readonly TPassData Data => ref _data;

    /// <summary>Initializes a generic render pass with setup and execution delegates.</summary>
    public GenericRenderPass(
        string name,
        FramePassKind kind,
        RenderPassSetupDelegate<TPassData> setup,
        RenderPassExecuteDelegate<TPassData> execute)
        : base(name, kind)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;

        var builder = new RenderPassBuilder(this);
        setup(builder, ref _data);

        Descriptor = builder.Descriptor;
        DeclareLoadAction(builder.LoadAction);
        DeclareStoreAction(builder.StoreAction);
    }

    /// <inheritdoc/>
    void IRenderPass.Execute(IRenderContext context)
    {
        if (context is GLRenderContext glCtx)
        {
            if (glCtx.PassContext is null)
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "PassContext is not configured on the GLRenderContext.", nameof(GenericRenderPass<TPassData>));
            }

            _execute(in _data, glCtx, glCtx.PassContext);
        }
        else
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, $"Execution of {GetType().Name} requires a GLRenderContext backend.", nameof(GenericRenderPass<TPassData>));
        }
    }
}
