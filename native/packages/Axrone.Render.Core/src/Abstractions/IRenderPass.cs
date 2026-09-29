namespace Axrone.Render.Core.Abstractions;

/// <summary>
/// Builder interface used during the render pass setup phase to declare resource dependencies,
/// attachment layout, and execution configuration.
/// </summary>
public interface IRenderPassBuilder
{
    /// <summary>Declares that the pass reads the specified resource name.</summary>
    void Reads(string resourceName);

    /// <summary>Declares that the pass writes the specified resource name.</summary>
    void Writes(string resourceName);

    /// <summary>Configures the render pass descriptor (targets, viewport, scissor, attachments).</summary>
    void SetDescriptor(in RenderPassDescriptor descriptor);

    /// <summary>Configures the attachment load action for the pass.</summary>
    void SetLoadAction(AttachmentLoadAction action);

    /// <summary>Configures the attachment store action for the pass.</summary>
    void SetStoreAction(AttachmentStoreAction action);
}

/// <summary>
/// Hardware-agnostic render pass contract.
/// </summary>
public interface IRenderPass
{
    /// <summary>Gets the name of the pass.</summary>
    string Name { get; }

    /// <summary>Gets or sets whether this pass is enabled.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Gets the render pass descriptor describing render targets and bounds.</summary>
    RenderPassDescriptor Descriptor { get; }

    /// <summary>Gets the declared input resource names.</summary>
    ReadOnlySpan<string> GetReadResources();

    /// <summary>Gets the declared output resource names.</summary>
    ReadOnlySpan<string> GetWrittenResources();

    /// <summary>Gets the attachment load action.</summary>
    AttachmentLoadAction LoadAction { get; }

    /// <summary>Gets the attachment store action.</summary>
    AttachmentStoreAction StoreAction { get; }

    /// <summary>Executes the pass using the given hardware-agnostic render context.</summary>
    void Execute(IRenderContext context);

    /// <summary>Validates the pass configuration prior to graph execution.</summary>
    void Validate();
}
