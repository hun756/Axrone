#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.
#pragma warning disable CA1062 // Phase parameters are supplied by the frame graph, which null-checks before dispatch.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Specifies the type of a compute resource binding.
/// </summary>
public enum ComputeBindingType
{
    /// <summary>Shader Storage Buffer Object (SSBO) bound via <c>glBindBufferBase</c>.</summary>
    ShaderStorageBuffer = 0,

    /// <summary>Texture image bound via <c>glBindImageTexture</c>.</summary>
    Image = 1,

    /// <summary>Uniform Buffer Object (UBO) bound via <c>glBindBufferBase</c>.</summary>
    UniformBuffer = 2,
}

/// <summary>
/// Declares how a compute pass accesses a bound resource, for frame-graph hazard tracking.
/// </summary>
public enum ComputeBindingAccess
{
    /// <summary>The pass only reads the resource.</summary>
    Read = 0,

    /// <summary>The pass only writes the resource.</summary>
    Write = 1,

    /// <summary>The pass reads and writes the resource.</summary>
    ReadWrite = 2,
}

/// <summary>
/// Describes a single resource binding for a compute pass.
/// </summary>
/// <param name="BindingPoint">The shader binding point (layout qualifier value).</param>
/// <param name="ResourceName">The resource name in the <see cref="PassExecutionContext"/>.</param>
/// <param name="Type">The binding type determining how the resource is bound.</param>
/// <param name="Access">Whether the pass reads, writes, or both; drives frame-graph write edges.</param>
public readonly record struct ComputeResourceBinding(
    uint BindingPoint,
    string ResourceName,
    ComputeBindingType Type,
    ComputeBindingAccess Access = ComputeBindingAccess.Read);

/// <summary>
/// Payload for the GPU compute dispatch pass, owning the pass's setup, validate and
/// execute phases.
/// Bindings are creation-time: they declare graph dependencies, so they cannot be
/// added after creation without invalidating the compiled DAG.
/// </summary>
public record struct ComputePassData
    : IPassSetup<ComputePassData>, IPassValidate<ComputePassData>, IPassExecute<ComputePassData>
{
    /// <summary>Compute shader program.</summary>
    public GLProgram ComputeShader { get; set; }

    /// <summary>Dispatch work group counts.</summary>
    public uint GroupCountX { get; set; }

    /// <summary>Dispatch work group counts.</summary>
    public uint GroupCountY { get; set; }

    /// <summary>Dispatch work group counts.</summary>
    public uint GroupCountZ { get; set; }

    /// <summary>Resource bindings resolved from the pass context at execution time.</summary>
    public List<ComputeResourceBinding> Bindings { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref ComputePassData data)
    {
        for (int i = 0; i < data.Bindings.Count; i++)
        {
            ComputeResourceBinding binding = data.Bindings[i];

            if (binding.Access is ComputeBindingAccess.Write or ComputeBindingAccess.ReadWrite)
            {
                builder.Writes(binding.ResourceName);
            }

            if (binding.Access is ComputeBindingAccess.Read or ComputeBindingAccess.ReadWrite)
            {
                builder.Reads(binding.ResourceName);
            }
        }
    }

    /// <inheritdoc/>
    public static void Validate(in ComputePassData data)
    {
        if (data.GroupCountX < 1 || data.GroupCountY < 1 || data.GroupCountZ < 1)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Dispatch group counts must be at least 1, got ({data.GroupCountX}, {data.GroupCountY}, {data.GroupCountZ})",
                nameof(ComputePass));
        }

        for (int i = 0; i < data.Bindings.Count; i++)
        {
            if (string.IsNullOrEmpty(data.Bindings[i].ResourceName))
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                    $"Compute resource binding at index {i} has an empty resource name",
                    nameof(ComputePass));
            }
        }
    }

    /// <inheritdoc/>
    public static void Execute(in ComputePassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();

        var gl = glContext.GL;
        glContext.State.UseProgram(data.ComputeShader.Id);

        for (int i = 0; i < data.Bindings.Count; i++)
        {
            var binding = data.Bindings[i];
            switch (binding.Type)
            {
                case ComputeBindingType.ShaderStorageBuffer:
                    {
                        var buffer = ctx.GetBuffer(binding.ResourceName);
                        gl.BindBufferBase(GLConst.ShaderStorageBuffer, binding.BindingPoint, buffer.Id);
                        break;
                    }

                case ComputeBindingType.UniformBuffer:
                    {
                        var buffer = ctx.GetBuffer(binding.ResourceName);
                        gl.BindBufferBase(GLConst.UniformBuffer, binding.BindingPoint, buffer.Id);
                        break;
                    }

                case ComputeBindingType.Image:
                    {
                        var texture = ctx.GetTexture(binding.ResourceName);
                        gl.BindImageTexture(binding.BindingPoint, texture.Id, 0, false, 0, 0x88B8, texture.FormatInfo.InternalFormat);
                        break;
                    }
            }
        }

        gl.DispatchCompute(data.GroupCountX, data.GroupCountY, data.GroupCountZ);
        gl.MemoryBarrier(GLConst.AllShaderStorageBits);
    }
}

/// <summary>
/// Factory for the GPU compute shader dispatch pass.
/// </summary>
public static class ComputePass
{
    /// <summary>Creates a compute dispatch pass.</summary>
    public static RenderPass<ComputePassData> Create(
        string name,
        GLProgram computeShader,
        uint groupCountX = 1,
        uint groupCountY = 1,
        uint groupCountZ = 1,
        IReadOnlyList<ComputeResourceBinding>? bindings = null)
    {
        ArgumentNullException.ThrowIfNull(computeShader);
        List<ComputeResourceBinding> snapshot = bindings is null
            ? new List<ComputeResourceBinding>()
            : new List<ComputeResourceBinding>(bindings);
        return new RenderPass<ComputePassData>(
            name,
            FramePassKind.Compute,
            new ComputePassData
            {
                ComputeShader = computeShader,
                GroupCountX = groupCountX,
                GroupCountY = groupCountY,
                GroupCountZ = groupCountZ,
                Bindings = snapshot
            });
    }
}
