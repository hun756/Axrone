#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the generic post-process pass (fullscreen triangle over one input).
/// Uniform setters do not declare graph dependencies and may be added after creation.
/// </summary>
public record struct PostProcessPassData
{
    /// <summary>Post-process shader program.</summary>
    public GLProgram Program { get; set; }

    /// <summary>Input texture resource name.</summary>
    public string InputTextureName { get; set; }

    /// <summary>Output framebuffer resource name, or null for the default framebuffer.</summary>
    public string? OutputFramebufferName { get; set; }

    /// <summary>Custom uniform setters invoked before drawing.</summary>
    public Dictionary<string, Action<GLContext, GLProgram>> UniformSetters { get; set; }

    /// <summary>Post-process phase (display-referred effects run after tone mapping).</summary>
    public PostProcessPhase Phase { get; set; }
}

/// <summary>
/// Factory for the generic post-process pass.
/// </summary>
public static class PostProcessPass
{
    /// <summary>Creates a generic post-process pass.</summary>
    public static RenderPass<PostProcessPassData> Create(
        string name,
        GLProgram program,
        string inputTextureName,
        string? outputFramebufferName = null,
        PostProcessPhase phase = PostProcessPhase.AfterTonemap)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(inputTextureName);
        return new RenderPass<PostProcessPassData>(
            name,
            FramePassKind.PostProcess,
            (IRenderPassBuilder builder, ref PostProcessPassData data) =>
            {
                data.Program = program;
                data.InputTextureName = inputTextureName;
                data.OutputFramebufferName = outputFramebufferName;
                data.Phase = phase;
                data.UniformSetters = new Dictionary<string, Action<GLContext, GLProgram>>(StringComparer.OrdinalIgnoreCase);
                builder.Reads(inputTextureName);
                if (outputFramebufferName is not null)
                {
                    builder.Writes(outputFramebufferName);
                }
            },
            Execute,
            Validate);
    }

    private static void Validate(in PostProcessPassData data)
    {
        if (data.Program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Post-process shader program has been disposed", nameof(PostProcessPass));
        }
    }

    private static void Execute(in PostProcessPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var gl = glContext.GL;
        var state = glContext.State;

        var inputTexture = ctx.GetTexture(data.InputTextureName);

        uint fboId = 0;
        if (data.OutputFramebufferName is not null && ctx.HasResource(data.OutputFramebufferName))
        {
            fboId = ctx.GetFramebuffer(data.OutputFramebufferName).Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);
        state.SetDepthTest(false);
        state.SetBlend(false);
        state.SetCullFace(false);
        state.SetColorMask(true, true, true, true);

        state.UseProgram(data.Program.Id);
        state.BindTexture2D(0, inputTexture.Id);

        int inputTexLocation = data.Program.GetUniformLocation("u_inputTexture");
        if (inputTexLocation >= 0)
        {
            gl.Uniform1(inputTexLocation, 0);
        }

        state.SetViewport(0, 0, inputTexture.Width, inputTexture.Height);

        foreach (var kvp in data.UniformSetters)
        {
            kvp.Value(glContext, data.Program);
        }

        state.BindVertexArray(0);
        gl.DrawArrays(GLConst.Triangles, 0, 3);
    }
}
