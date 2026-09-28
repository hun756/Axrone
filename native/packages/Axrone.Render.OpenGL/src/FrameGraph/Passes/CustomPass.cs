namespace Axrone.Render.OpenGL.FrameGraph.Passes;

/// <summary>
/// Payload for the user-defined escape-hatch pass.
/// </summary>
public record struct CustomPassData
{
    /// <summary>Callback invoked during execution with full GL and graph access.</summary>
    public Action<GLContext, PassExecutionContext> ExecuteCallback { get; set; }

    /// <summary>Optional callback invoked during validation.</summary>
    public Action? ValidateCallback { get; set; }
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
        string[] readSnapshot = reads is null ? Array.Empty<string>() : reads.ToArray();
        string[] writeSnapshot = writes is null ? Array.Empty<string>() : writes.ToArray();
        return new RenderPass<CustomPassData>(
            name,
            kind,
            (IRenderPassBuilder builder, ref CustomPassData data) =>
            {
                data.ExecuteCallback = executeCallback;
                data.ValidateCallback = validateCallback;
                for (int i = 0; i < readSnapshot.Length; i++)
                {
                    builder.Reads(readSnapshot[i]);
                }

                for (int i = 0; i < writeSnapshot.Length; i++)
                {
                    builder.Writes(writeSnapshot[i]);
                }
            },
            Execute,
            Validate);
    }

    private static void Validate(in CustomPassData data) => data.ValidateCallback?.Invoke();

    private static void Execute(in CustomPassData data, IRenderContext context, PassExecutionContext ctx)
    {
        var glContext = ctx.Context;
        glContext.AssertRenderThread();
        data.ExecuteCallback(glContext, ctx);
    }
}
