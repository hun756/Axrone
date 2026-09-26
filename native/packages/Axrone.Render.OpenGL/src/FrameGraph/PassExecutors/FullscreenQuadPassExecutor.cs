namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Renders a screen-filling triangle with a specified shader program.
/// Used as the base primitive for post-processing effects and general
/// fullscreen rendering within the frame graph.
/// </summary>
/// <remarks>
/// <para>Uses the modern "overdrawn triangle" technique: 3 vertices with positions
/// generated in the vertex shader (no vertex buffer or VAO required). The triangle
/// is sized to cover the entire viewport with one vertex clipped.</para>
/// <para>Texture bindings and custom uniform setters can be configured fluently
/// to support arbitrary post-process shaders.</para>
/// </remarks>
public sealed class FullscreenQuadPassExecutor : RenderPass
{
    private readonly GLProgram _shader;
    private readonly List<(int Unit, string TextureName)> _textureBindings = new();
    private Action<GLContext, GLProgram>? _uniformCallback;

    private string? _outputFramebufferName;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullscreenQuadPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="shader">The shader program to use for fullscreen rendering.</param>
    public FullscreenQuadPassExecutor(string name, GLProgram shader)
        : base(name, FramePassKind.FullscreenQuad)
    {
        ArgumentNullException.ThrowIfNull(shader);
        _shader = shader;
    }

    /// <summary>Gets the output framebuffer name, or <see langword="null"/> for the default framebuffer.</summary>
    public string? OutputFramebufferName
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outputFramebufferName;
    }

    /// <summary>Gets the texture bindings as a read-only list.</summary>
    public IReadOnlyList<(int Unit, string TextureName)> TextureBindings
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _textureBindings;
    }

    /// <summary>
    /// Sets the output framebuffer name. If <see langword="null"/> or not set,
    /// rendering targets the default framebuffer.
    /// </summary>
    /// <param name="framebufferName">The framebuffer resource name in the pass context.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FullscreenQuadPassExecutor WithOutput(string framebufferName)
    {
        _outputFramebufferName = framebufferName;
        Writes(framebufferName);
        return this;
    }

    /// <summary>
    /// Adds a texture binding at the specified texture unit.
    /// </summary>
    /// <param name="unit">The texture unit index (0–31).</param>
    /// <param name="textureName">The texture resource name in the pass context.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FullscreenQuadPassExecutor BindTexture(int unit, string textureName)
    {
        _textureBindings.Add((unit, textureName));
        Reads(textureName);
        return this;
    }

    /// <summary>
    /// Sets a callback invoked after the shader is bound and textures are bound,
    /// allowing custom uniform values to be set before drawing.
    /// </summary>
    /// <param name="callback">The uniform callback receiving the GL context and shader program.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FullscreenQuadPassExecutor WithUniforms(Action<GLContext, GLProgram> callback)
    {
        _uniformCallback = callback;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        for (int i = 0; i < _textureBindings.Count; i++)
        {
            var (unit, name) = _textureBindings[i];

            if (unit < 0 || unit >= 32)
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                    $"Texture unit must be in [0, 31], got {unit}", nameof(FullscreenQuadPassExecutor));
            }

            if (string.IsNullOrEmpty(name))
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                    $"Texture binding at unit {unit} has an empty resource name", nameof(FullscreenQuadPassExecutor));
            }
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        // Bind output framebuffer (or default).
        if (_outputFramebufferName is not null && ctx.HasResource(_outputFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(_outputFramebufferName);
            context.State.BindFramebuffer(GLConst.Framebuffer, fbo.Id);
            context.State.SetViewport(0, 0, fbo.Width, fbo.Height);
        }

        context.State.SetDepthTest(false);
        context.State.SetCullFace(false);

        // Bind shader.
        context.State.UseProgram(_shader.Id);

        // Bind input textures.
        for (int i = 0; i < _textureBindings.Count; i++)
        {
            var (unit, textureName) = _textureBindings[i];
            var texture = ctx.GetTexture(textureName);
            context.State.BindTexture2D((uint)unit, texture.Id);
        }

        // Invoke custom uniform callback.
        _uniformCallback?.Invoke(context, _shader);

        // Draw fullscreen triangle (3 vertices, no VAO needed).
        context.GL.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
