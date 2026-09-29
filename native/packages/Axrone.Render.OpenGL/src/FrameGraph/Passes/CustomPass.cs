#pragma warning disable CA1002 // Payloads expose concrete collections for allocation-free access.
#pragma warning disable CA2227 // The setup protocol assigns payload collections once at creation.
#pragma warning disable CA1062 // Phase parameters are supplied by the frame graph, which null-checks before dispatch.

namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the user-defined escape-hatch pass, owning the pass's setup, validate
/// and execute phases. The phases delegate to the caller's callbacks, which is what
/// makes this the documented callback-based escape hatch.
/// </summary>
public record struct CustomPassData
    : IPassSetup<CustomPassData>, IPassValidate<CustomPassData>, IPassExecute<CustomPassData>
{
    /// <summary>Callback invoked during execution with full GL and graph access.</summary>
    public Action<GLContext, PassExecutionContext> ExecuteCallback { get; set; }

    /// <summary>Optional callback invoked during validation.</summary>
    public Action? ValidateCallback { get; set; }

    /// <summary>Resource names this pass reads, declared as graph dependencies.</summary>
    public List<string> Reads { get; set; }

    /// <summary>Resource names this pass writes, declared as graph dependencies.</summary>
    public List<string> Writes { get; set; }

    /// <inheritdoc/>
    public static void Declare(IRenderPassBuilder builder, ref CustomPassData data)
    {
        for (int i = 0; i < data.Reads.Count; i++)
        {
            builder.Reads(data.Reads[i]);
        }

        for (int i = 0; i < data.Writes.Count; i++)
        {
            builder.Writes(data.Writes[i]);
        }
    }

    /// <inheritdoc/>
    public static void Validate(in CustomPassData data) => data.ValidateCallback?.Invoke();

    /// <inheritdoc/>
    public static void Execute(in CustomPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();
        data.ExecuteCallback(glContext, ctx);
    }
}

/// <summary>
/// Factory for user-defined passes with callback-based execution.
/// </summary>
public static class CustomPass
{
    /// <summary>Creates a custom pass.</summary>
    public static RenderPass<CustomPassData> Create(
        string name,
        FramePassKind kind,
        Action<GLContext, PassExecutionContext> executeCallback,
        Action? validateCallback = null,
        IReadOnlyList<string>? reads = null,
        IReadOnlyList<string>? writes = null)
    {
        ArgumentNullException.ThrowIfNull(executeCallback);
        List<string> readSnapshot = reads is null ? new List<string>() : new List<string>(reads);
        List<string> writeSnapshot = writes is null ? new List<string>() : new List<string>(writes);
        return new RenderPass<CustomPassData>(
            name,
            kind,
            new CustomPassData
            {
                ExecuteCallback = executeCallback,
                ValidateCallback = validateCallback,
                Reads = readSnapshot,
                Writes = writeSnapshot
            });
    }
}
