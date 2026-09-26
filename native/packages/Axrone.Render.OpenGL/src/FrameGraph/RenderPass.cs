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

    /// <summary>User-defined custom pass.</summary>
    Custom,
}

/// <summary>
/// Base class for all render passes in the frame graph.
/// Each pass declares its resource reads/writes and implements execution logic.
/// </summary>
/// <remarks>
/// <para>Subclasses must implement <see cref="Execute"/> and may override
/// <see cref="Validate"/> to enforce pre-conditions. The constructor must call
/// <see cref="Reads"/> and <see cref="Writes"/> to declare resource dependencies
/// for the frame graph scheduler.</para>
/// </remarks>
public abstract class RenderPass
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
    /// Executes the pass using the given GL context and resource context.
    /// </summary>
    /// <param name="context">The GL context for issuing draw calls.</param>
    /// <param name="ctx">The pass execution context for resource resolution.</param>
    public abstract void Execute(Context.GLContext context, PassExecutionContext ctx);

    /// <summary>
    /// Validates the pass configuration. Called before execution to catch
    /// misconfigurations early. Override to add pass-specific validation.
    /// </summary>
    public virtual void Validate() { }

    /// <inheritdoc/>
    public override string ToString() => $"RenderPass: \"{Name}\" ({Kind}), Enabled={IsEnabled}";
}
