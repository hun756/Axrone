#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the opaque geometry pass.
/// Meshes are direct GPU handles (not graph resources): mutating <see cref="Meshes"/>
/// after creation is safe and never affects the dependency DAG.
/// </summary>
public record struct OpaquePassData
{
    /// <summary>Shader program.</summary>
    public GLProgram Program { get; set; }

    /// <summary>Target framebuffer resource name, or null for the default framebuffer.</summary>
    public string? TargetFramebufferName { get; set; }

    /// <summary>Meshes rendered in this pass.</summary>
    public List<GLMesh> Meshes { get; set; }
}

/// <summary>
/// Factory for the opaque geometry pass (depth-tested, front-to-back early-Z).
/// </summary>
public static class OpaquePass
{
    /// <summary>Creates an opaque geometry pass.</summary>
    public static RenderPass<OpaquePassData> Create(
        string name,
        GLProgram program,
        string? targetFramebufferName = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        return new RenderPass<OpaquePassData>(
            name,
            FramePassKind.Opaque,
            (IRenderPassBuilder builder, ref OpaquePassData data) =>
            {
                data.Program = program;
                data.TargetFramebufferName = targetFramebufferName;
                data.Meshes = new List<GLMesh>();
                if (targetFramebufferName is not null)
                {
                    builder.Writes(targetFramebufferName);
                }
            },
            Execute,
            Validate);
    }

    private static void Validate(in OpaquePassData data)
    {
        if (data.Program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Shader program has been disposed", nameof(OpaquePass));
        }
    }

    private static void Execute(in OpaquePassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        if (data.Meshes.Count == 0)
        {
            return;
        }

        var state = glContext.State;

        uint fboId = 0;
        if (data.TargetFramebufferName is not null && ctx.HasResource(data.TargetFramebufferName))
        {
            fboId = ctx.GetFramebuffer(data.TargetFramebufferName).Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);

        state.SetDepthTest(true);
        state.SetDepthFunc(GLConst.Lequal);
        state.SetDepthMask(true);
        state.SetBlend(false);
        state.SetCullFace(true);
        state.SetCullMode(GLConst.Back);
        state.SetColorMask(true, true, true, true);

        state.UseProgram(data.Program.Id);

        var meshSpan = CollectionsMarshal.AsSpan(data.Meshes);
        for (int i = 0; i < meshSpan.Length; i++)
        {
            ref var mesh = ref meshSpan[i];
            mesh.Draw();
        }
    }
}
