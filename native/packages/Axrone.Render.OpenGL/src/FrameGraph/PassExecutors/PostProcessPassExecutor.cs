namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Generic post-process pass: fullscreen quad with custom shader reading from input texture.
/// Reads from an input texture and writes to an output texture/framebuffer.
/// </summary>
public sealed class PostProcessPassExecutor : RenderPass
{
    private readonly string _inputTextureName;
    private readonly string? _outputFramebufferName;
    private readonly GLProgram _postProcessProgram;
    private readonly Dictionary<string, Action<GLContext, GLProgram>> _uniformSetters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="PostProcessPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="postProcessProgram">The post-process shader program.</param>
    /// <param name="inputTextureName">The input texture resource name.</param>
    /// <param name="outputFramebufferName">The output framebuffer resource name, or null for the default framebuffer.</param>
    public PostProcessPassExecutor(
        string name,
        GLProgram postProcessProgram,
        string inputTextureName,
        string? outputFramebufferName = null)
        : base(name, FramePassKind.PostProcess)
    {
        ArgumentNullException.ThrowIfNull(postProcessProgram);
        ArgumentNullException.ThrowIfNull(inputTextureName);

        _postProcessProgram = postProcessProgram;
        _inputTextureName = inputTextureName;
        _outputFramebufferName = outputFramebufferName;

        Reads(inputTextureName);

        if (outputFramebufferName != null)
        {
            Writes(outputFramebufferName);
        }
    }

    /// <summary>
    /// Adds a custom uniform setter that will be called before drawing.
    /// </summary>
    /// <param name="uniformName">The uniform name (for identification).</param>
    /// <param name="setter">The setter action that receives the GL context and program.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public PostProcessPassExecutor SetUniform(string uniformName, Action<GLContext, GLProgram> setter)
    {
        ArgumentNullException.ThrowIfNull(uniformName);
        ArgumentNullException.ThrowIfNull(setter);
        _uniformSetters[uniformName] = setter;
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_postProcessProgram.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Post-process shader program has been disposed", nameof(PostProcessPassExecutor));
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        var gl = context.GL;
        var state = context.State;

        // Get input texture
        var inputTexture = ctx.GetTexture(_inputTextureName);

        // Bind output framebuffer
        uint fboId = 0;
        if (_outputFramebufferName != null && ctx.HasResource(_outputFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(_outputFramebufferName);
            fboId = fbo.Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);

        // Configure state for fullscreen quad rendering
        state.SetDepthTest(false);
        state.SetBlend(false);
        state.SetCullFace(false);
        state.SetColorMask(true, true, true, true);

        // Bind post-process shader
        state.UseProgram(_postProcessProgram.Id);

        // Bind input texture to texture unit 0
        state.BindTexture2D(0, inputTexture.Id);

        // Set input texture uniform
        int inputTexLocation = _postProcessProgram.GetUniformLocation("u_inputTexture");
        if (inputTexLocation >= 0)
        {
            gl.Uniform1(inputTexLocation, 0);
        }

        // Set viewport to input texture size
        state.SetViewport(0, 0, inputTexture.Width, inputTexture.Height);

        // Call custom uniform setters
        foreach (var kvp in _uniformSetters)
        {
            kvp.Value(context, _postProcessProgram);
        }

        // Draw fullscreen triangle (3 vertices, generated in vertex shader)
        // Use a temporary VAO-less draw via glDrawArrays
        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
