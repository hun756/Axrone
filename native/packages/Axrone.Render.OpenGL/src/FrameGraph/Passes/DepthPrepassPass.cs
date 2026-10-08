#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.
#pragma warning disable CA1062 // Phase parameters are supplied by the frame graph, which null-checks before dispatch.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the depth-only pre-pass, owning the pass's setup, validate and execute phases.
/// </summary>
public record struct DepthPrepassPassData
    : IPassSetup<DepthPrepassPassData>, IPassValidate<DepthPrepassPassData>, IPassExecute<DepthPrepassPassData>
{
    /// <summary>Depth-only shader program.</summary>
    public GLProgram DepthProgram { get; set; }

    /// <summary>View-projection matrix uniform name.</summary>
    public string ViewProjectionUniform { get; set; }

    /// <summary>View-projection matrix uploaded on each execution. Update per frame.</summary>
    public Mat4 ViewProjection { get; set; }

    /// <summary>Depth target framebuffer resource name.</summary>
    public string DepthTargetName { get; set; }

    /// <summary>Meshes rendered into the depth target.</summary>
    public List<GLMesh> Meshes { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref DepthPrepassPassData data)
    {
        builder.Writes(data.DepthTargetName);
    }

    /// <inheritdoc/>
    public static void Validate(in DepthPrepassPassData data)
    {
        if (data.DepthProgram.IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Depth shader program has been disposed", nameof(DepthPrepassPass));
        }

        if (string.IsNullOrWhiteSpace(data.ViewProjectionUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "View-projection uniform name must not be empty", nameof(DepthPrepassPass));
        }

        if (string.IsNullOrWhiteSpace(data.DepthTargetName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Depth target name must not be empty", nameof(DepthPrepassPass));
        }
    }

    /// <inheritdoc/>
    public static void Execute(in DepthPrepassPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        if (data.Meshes.Count == 0)
        {
            return;
        }

        var gl = glContext.GL;
        var state = glContext.State;

        uint fboId = 0;
        GLFramebuffer? target = null;
        if (ctx.HasResource(data.DepthTargetName))
        {
            target = ctx.GetFramebuffer(data.DepthTargetName);
            fboId = target.Id;
        }

        state.BindFramebuffer(GLConst.Framebuffer, fboId);
        if (target is not null)
        {
            state.SetViewport(0, 0, target.Width, target.Height);
        }

        state.SetDepthTest(true);
        state.SetDepthFunc(GLConst.Less);
        state.SetDepthMask(true);
        state.SetBlend(false);
        state.SetCullFace(true);
        state.SetCullMode(GLConst.Back);
        state.SetColorMask(false, false, false, false);

        state.UseProgram(data.DepthProgram.Id);

        int location = data.DepthProgram.GetUniformLocation(data.ViewProjectionUniform);
        if (location != -1)
        {
            unsafe
            {
                Mat4 matrix = data.ViewProjection;
                gl.UniformMatrix4(location, 1, false, &matrix.M11);
            }
        }

        var meshSpan = CollectionsMarshal.AsSpan(data.Meshes);
        for (int i = 0; i < meshSpan.Length; i++)
        {
            ref var mesh = ref meshSpan[i];
            mesh.Draw();
        }

        state.SetColorMask(true, true, true, true);
    }
}

/// <summary>
/// Factory for the depth-only pre-pass (early-Z priming).
/// </summary>
public static class DepthPrepassPass
{
    /// <summary>Creates a depth pre-pass.</summary>
    public static RenderPass<DepthPrepassPassData> Create(
        string name,
        GLProgram depthProgram,
        string viewProjectionUniform,
        IReadOnlyList<GLMesh> meshes,
        string depthTargetName)
    {
        ArgumentNullException.ThrowIfNull(depthProgram);
        ArgumentNullException.ThrowIfNull(viewProjectionUniform);
        ArgumentNullException.ThrowIfNull(meshes);
        ArgumentNullException.ThrowIfNull(depthTargetName);

        if (string.IsNullOrWhiteSpace(viewProjectionUniform))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "View-projection uniform name must not be empty", nameof(DepthPrepassPass));
        }

        if (string.IsNullOrWhiteSpace(depthTargetName))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, "Depth target name must not be empty", nameof(DepthPrepassPass));
        }

        List<GLMesh> snapshot = new(meshes.Count);
        for (int i = 0; i < meshes.Count; i++)
        {
            GLMesh mesh = meshes[i];
            ArgumentNullException.ThrowIfNull(mesh);
            snapshot.Add(mesh);
        }

        return new RenderPass<DepthPrepassPassData>(
            name,
            FramePassKind.DepthPrepass,
            new DepthPrepassPassData
            {
                DepthProgram = depthProgram,
                ViewProjectionUniform = viewProjectionUniform,
                ViewProjection = Mat4.Identity,
                DepthTargetName = depthTargetName,
                Meshes = snapshot
            });
    }
}
