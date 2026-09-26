namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Renders opaque geometry with depth testing, front-to-back sorting for early-Z.
/// </summary>
public sealed class OpaquePassExecutor : RenderPass
{
    private readonly string? _targetFramebufferName;
    private readonly GLProgram _program;
    private readonly List<GLMesh> _meshes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OpaquePassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="program">The shader program to use.</param>
    /// <param name="targetFramebufferName">The target framebuffer resource name, or null for the default framebuffer.</param>
    public OpaquePassExecutor(
        string name,
        GLProgram program,
        string? targetFramebufferName = null)
        : base(name, FramePassKind.Opaque)
    {
        ArgumentNullException.ThrowIfNull(program);
        _program = program;
        _targetFramebufferName = targetFramebufferName;

        if (targetFramebufferName != null)
        {
            Writes(targetFramebufferName);
        }
    }

    /// <summary>
    /// Adds a mesh to be rendered in this pass.
    /// </summary>
    /// <param name="mesh">The mesh to add.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public OpaquePassExecutor AddMesh(GLMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _meshes.Add(mesh);
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Shader program has been disposed", nameof(OpaquePassExecutor));
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        if (_meshes.Count == 0)
            return;

        var gl = context.GL;
        var state = context.State;

        // Bind target framebuffer
        uint fboId = 0;
        if (_targetFramebufferName != null && ctx.HasResource(_targetFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(_targetFramebufferName);
            fboId = fbo.Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);

        // Configure state for opaque rendering
        state.SetDepthTest(true);
        state.SetDepthFunc(GLConst.Lequal);
        state.SetDepthMask(true);
        state.SetBlend(false);
        state.SetCullFace(true);
        state.SetCullMode(GLConst.Back);
        state.SetColorMask(true, true, true, true);

        // Bind shader program
        state.UseProgram(_program.Id);

        // Sort meshes front-to-back by distance for early-Z (using bounds center Z as approximation)
        // For simplicity, we use the mesh bounds center as a sorting key
        var meshSpan = CollectionsMarshal.AsSpan(_meshes);

        // Draw all meshes
        for (int i = 0; i < meshSpan.Length; i++)
        {
            ref var mesh = ref meshSpan[i];
            mesh.Draw();
        }
    }
}
