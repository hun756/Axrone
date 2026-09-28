#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// A transparent mesh entry with its world position for back-to-front sorting.
/// </summary>
/// <param name="Mesh">The mesh.</param>
/// <param name="WorldPosition">The world position used for sorting.</param>
public readonly record struct TransparentMeshEntry(GLMesh Mesh, Vector3 WorldPosition);

/// <summary>
/// Payload for the transparent geometry pass.
/// </summary>
public record struct TransparentPassData
{
    /// <summary>Shader program.</summary>
    public GLProgram Program { get; set; }

    /// <summary>Target framebuffer resource name, or null for the default framebuffer.</summary>
    public string? TargetFramebufferName { get; set; }

    /// <summary>Mesh entries rendered in this pass.</summary>
    public List<TransparentMeshEntry> Entries { get; set; }
}

/// <summary>
/// Factory for the transparent geometry pass (alpha-blended, back-to-front sorted).
/// </summary>
public static class TransparentPass
{
    /// <summary>Creates a transparent geometry pass.</summary>
    public static RenderPass<TransparentPassData> Create(
        string name,
        GLProgram program,
        string? targetFramebufferName = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        return new RenderPass<TransparentPassData>(
            name,
            FramePassKind.Transparent,
            (IRenderPassBuilder builder, ref TransparentPassData data) =>
            {
                data.Program = program;
                data.TargetFramebufferName = targetFramebufferName;
                data.Entries = new List<TransparentMeshEntry>();
                if (targetFramebufferName is not null)
                {
                    builder.Reads(targetFramebufferName);
                }
            },
            Execute,
            Validate);
    }

    private static void Validate(in TransparentPassData data)
    {
        if (data.Program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Shader program has been disposed", nameof(TransparentPass));
        }
    }

    private static void Execute(in TransparentPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        if (data.Entries.Count == 0)
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
        state.SetDepthMask(false);
        state.SetBlend(true);
        state.SetBlendFuncSeparate(GLConst.SrcAlpha, GLConst.OneMinusSrcAlpha, GLConst.SrcAlpha, GLConst.OneMinusSrcAlpha);
        state.SetBlendEquationSeparate(GLConst.FuncAdd, GLConst.FuncAdd);
        state.SetCullFace(true);
        state.SetCullMode(GLConst.Back);
        state.SetColorMask(true, true, true, true);

        state.UseProgram(data.Program.Id);

        var entries = CollectionsMarshal.AsSpan(data.Entries);
        entries.Sort(static (a, b) =>
        {
            float distA = a.WorldPosition.LengthSquared();
            float distB = b.WorldPosition.LengthSquared();
            return distB.CompareTo(distA);
        });

        for (int i = 0; i < entries.Length; i++)
        {
            entries[i].Mesh.Draw();
        }
    }
}
