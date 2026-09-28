#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the fullscreen triangle pass.
/// Texture bindings are creation-time (they declare graph dependencies).
/// </summary>
public record struct FullscreenQuadPassData
{
    /// <summary>Fullscreen shader program.</summary>
    public GLProgram Shader { get; set; }

    /// <summary>Output framebuffer name, or null for the default framebuffer.</summary>
    public string? OutputFramebufferName { get; set; }

    /// <summary>Texture bindings as (unit, resource name) pairs.</summary>
    public List<(int Unit, string TextureName)> TextureBindings { get; set; }

    /// <summary>Optional custom uniform callback invoked after binding.</summary>
    public Action<GLContext, GLProgram>? UniformCallback { get; set; }
}

/// <summary>
/// Factory for the screen-filling triangle pass (overdrawn triangle, no VAO).
/// </summary>
public static class FullscreenQuadPass
{
    /// <summary>Creates a fullscreen triangle pass.</summary>
    public static RenderPass<FullscreenQuadPassData> Create(
        string name,
        GLProgram shader,
        string? outputFramebufferName = null,
        IReadOnlyList<(int Unit, string TextureName)>? textureBindings = null,
        Action<GLContext, GLProgram>? uniformCallback = null)
    {
        ArgumentNullException.ThrowIfNull(shader);
        List<(int Unit, string TextureName)> snapshot = textureBindings is null
            ? new List<(int Unit, string TextureName)>()
            : new List<(int Unit, string TextureName)>(textureBindings);
        return new RenderPass<FullscreenQuadPassData>(
            name,
            FramePassKind.FullscreenQuad,
            (IRenderPassBuilder builder, ref FullscreenQuadPassData data) =>
            {
                data.Shader = shader;
                data.OutputFramebufferName = outputFramebufferName;
                data.TextureBindings = snapshot;
                data.UniformCallback = uniformCallback;
                if (outputFramebufferName is not null)
                {
                    builder.Writes(outputFramebufferName);
                }

                for (int i = 0; i < snapshot.Count; i++)
                {
                    builder.Reads(snapshot[i].TextureName);
                }
            },
            Execute,
            Validate);
    }

    private static void Validate(in FullscreenQuadPassData data)
    {
        for (int i = 0; i < data.TextureBindings.Count; i++)
        {
            var (unit, textureName) = data.TextureBindings[i];
            if (unit < 0 || unit >= 32)
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                    $"Texture unit must be in [0, 31], got {unit}", nameof(FullscreenQuadPass));
            }

            if (string.IsNullOrEmpty(textureName))
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                    $"Texture binding at unit {unit} has an empty resource name", nameof(FullscreenQuadPass));
            }
        }
    }

    private static void Execute(in FullscreenQuadPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        if (data.UniformCallback is null && data.TextureBindings.Count <= FullscreenCommandLimits.MaxTextureBinds)
        {
            ExecuteMonomorphic(in data, glContext, ctx);
            return;
        }

        if (data.OutputFramebufferName is not null && ctx.HasResource(data.OutputFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(data.OutputFramebufferName);
            glContext.State.BindFramebuffer(GLConst.Framebuffer, fbo.Id);
            glContext.State.SetViewport(0, 0, fbo.Width, fbo.Height);
        }

        glContext.State.SetDepthTest(false);
        glContext.State.SetCullFace(false);
        glContext.State.UseProgram(data.Shader.Id);

        for (int i = 0; i < data.TextureBindings.Count; i++)
        {
            var (unit, textureName) = data.TextureBindings[i];
            var texture = ctx.GetTexture(textureName);
            glContext.State.BindTexture2D((uint)unit, texture.Id);
        }

        data.UniformCallback?.Invoke(glContext, data.Shader);
        glContext.GL.DrawArrays(GLConst.Triangles, 0, 3);
    }

    private static void ExecuteMonomorphic(in FullscreenQuadPassData data, GLContext glContext, PassExecutionContext ctx)
    {
        if (data.OutputFramebufferName is not null && ctx.HasResource(data.OutputFramebufferName))
        {
            var fbo = ctx.GetFramebuffer(data.OutputFramebufferName);
            glContext.State.BindFramebuffer(GLConst.Framebuffer, fbo.Id);
            glContext.State.SetViewport(0, 0, fbo.Width, fbo.Height);
        }

        glContext.State.SetDepthTest(false);
        glContext.State.SetCullFace(false);

        var command = new FullscreenTriangleCommand<GLContextInvoker>(data.Shader.Id);
        for (int i = 0; i < data.TextureBindings.Count; i++)
        {
            var (unit, textureName) = data.TextureBindings[i];
            var texture = ctx.GetTexture(textureName);
            command.AddTextureBind((uint)unit, texture.Id);
        }

        var invoker = new GLContextInvoker(glContext);
        FullscreenDispatcher.Dispatch(ref invoker, ref command);
    }
}
