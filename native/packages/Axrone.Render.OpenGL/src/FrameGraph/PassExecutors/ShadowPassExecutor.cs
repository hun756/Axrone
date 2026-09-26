namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Renders shadow map from light's perspective.
/// Writes the shadow map texture for use by subsequent passes.
/// </summary>
public sealed class ShadowPassExecutor : RenderPass
{
    private readonly string _shadowMapTextureName;
    private readonly string _shadowFramebufferName;
    private readonly GLProgram _depthProgram;
    private readonly List<GLMesh> _meshes = new();
    private readonly Matrix4x4 _lightViewProjection;
    private readonly int _shadowMapWidth;
    private readonly int _shadowMapHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShadowPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="depthProgram">The depth-only shader program.</param>
    /// <param name="shadowMapTextureName">The shadow map texture resource name.</param>
    /// <param name="shadowFramebufferName">The shadow framebuffer resource name.</param>
    /// <param name="lightViewProjection">The light view-projection matrix.</param>
    /// <param name="shadowMapWidth">The shadow map width.</param>
    /// <param name="shadowMapHeight">The shadow map height.</param>
    public ShadowPassExecutor(
        string name,
        GLProgram depthProgram,
        string shadowMapTextureName,
        string shadowFramebufferName,
        Matrix4x4 lightViewProjection,
        int shadowMapWidth = 2048,
        int shadowMapHeight = 2048)
        : base(name, FramePassKind.Shadow)
    {
        ArgumentNullException.ThrowIfNull(depthProgram);
        ArgumentNullException.ThrowIfNull(shadowMapTextureName);
        ArgumentNullException.ThrowIfNull(shadowFramebufferName);

        _depthProgram = depthProgram;
        _shadowMapTextureName = shadowMapTextureName;
        _shadowFramebufferName = shadowFramebufferName;
        _lightViewProjection = lightViewProjection;
        _shadowMapWidth = shadowMapWidth;
        _shadowMapHeight = shadowMapHeight;

        Writes(shadowMapTextureName);
        Writes(shadowFramebufferName);
    }

    /// <summary>
    /// Adds a mesh to be rendered in the shadow pass.
    /// </summary>
    /// <param name="mesh">The mesh to add.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public ShadowPassExecutor AddMesh(GLMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _meshes.Add(mesh);
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_depthProgram.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Depth shader program has been disposed", nameof(ShadowPassExecutor));
        }

        if (_shadowMapWidth <= 0 || _shadowMapHeight <= 0)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Shadow map dimensions must be positive", nameof(ShadowPassExecutor));
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        var gl = context.GL;
        var state = context.State;

        // Bind shadow framebuffer
        uint fboId = 0;
        if (ctx.HasResource(_shadowFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(_shadowFramebufferName);
            fboId = fbo.Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);

        // Set viewport to shadow map size
        state.SetViewport(0, 0, _shadowMapWidth, _shadowMapHeight);

        // Configure state for depth-only rendering
        state.SetDepthTest(true);
        state.SetDepthFunc(GLConst.Lequal);
        state.SetDepthMask(true);
        state.SetBlend(false);
        state.SetCullFace(true);
        state.SetCullMode(GLConst.Front); // Front-face culling for shadow maps to reduce acne
        state.SetColorMask(false, false, false, false); // Disable color writes

        // Bind depth shader program
        state.UseProgram(_depthProgram.Id);

        // Set light view-projection matrix uniform if location exists
        int lightMatrixLocation = _depthProgram.GetUniformLocation("u_lightViewProjection");
        if (lightMatrixLocation >= 0)
        {
            unsafe
            {
                Matrix4x4 matrix = _lightViewProjection;
                gl.UniformMatrix4(lightMatrixLocation, 1, false, &matrix.M11);
            }
        }

        // Draw all meshes
        var meshSpan = CollectionsMarshal.AsSpan(_meshes);
        for (int i = 0; i < meshSpan.Length; i++)
        {
            ref var mesh = ref meshSpan[i];
            mesh.Draw();
        }

        // Restore color mask
        state.SetColorMask(true, true, true, true);
    }
}
