#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.
#pragma warning disable CA1062 // Phase parameters are supplied by the frame graph, which null-checks before dispatch.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// A transparent mesh entry with its world position for back-to-front sorting.
/// </summary>
/// <param name="Mesh">The mesh.</param>
/// <param name="WorldPosition">The world position used for sorting.</param>
public readonly record struct TransparentMeshEntry(GLMesh Mesh, Vec3 WorldPosition);

/// <summary>
/// Payload for the transparent geometry pass, owning the pass's setup, validate and
/// execute phases.
/// </summary>
public record struct TransparentPassData
    : IPassSetup<TransparentPassData>, IPassValidate<TransparentPassData>, IPassExecute<TransparentPassData>
{
    /// <summary>Shader program.</summary>
    public GLProgram Program { get; set; }

    /// <summary>Target framebuffer resource name, or null for the default framebuffer.</summary>
    public string? TargetFramebufferName { get; set; }

    /// <summary>Mesh entries rendered in this pass.</summary>
    public List<TransparentMeshEntry> Entries { get; set; }

    /// <summary>Camera world position used as the origin for back-to-front sorting.</summary>
    public Vec3 CameraPosition { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref TransparentPassData data)
    {
        if (data.TargetFramebufferName is not null)
        {
            builder.Reads(data.TargetFramebufferName);
        }
    }

    /// <inheritdoc/>
    public static void Validate(in TransparentPassData data)
    {
        if (data.Program.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Shader program has been disposed", nameof(TransparentPass));
        }
    }

    /// <inheritdoc/>
    public static void Execute(in TransparentPassData data, IRenderContext context, PassExecutionContext ctx)
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

        // Sort back-to-front relative to the CAMERA, not the world origin: alpha blending is only
        // correct when fragments are drawn farthest-from-viewer first. Update CameraPosition each
        // frame before execution so the order tracks the viewer.
        Vec3 camera = data.CameraPosition;
        var entries = CollectionsMarshal.AsSpan(data.Entries);
        entries.Sort((a, b) =>
        {
            float distA = (a.WorldPosition - camera).LengthSquared();
            float distB = (b.WorldPosition - camera).LengthSquared();
            return distB.CompareTo(distA);
        });

        for (int i = 0; i < entries.Length; i++)
        {
            entries[i].Mesh.Draw();
        }
    }
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
        string? targetFramebufferName = null,
        Vec3 cameraPosition = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        return new RenderPass<TransparentPassData>(
            name,
            FramePassKind.Transparent,
            new TransparentPassData
            {
                Program = program,
                TargetFramebufferName = targetFramebufferName,
                CameraPosition = cameraPosition,
                Entries = new List<TransparentMeshEntry>()
            });
    }
}
