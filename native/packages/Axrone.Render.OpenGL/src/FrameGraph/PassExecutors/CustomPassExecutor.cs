namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// User-defined render pass with callback-based execution.
/// Allows arbitrary custom rendering logic to be integrated into the frame graph
/// without creating a dedicated pass executor subclass.
/// </summary>
/// <remarks>
/// <para>The execute callback receives the <see cref="GLContext"/> and
/// <see cref="PassExecutionContext"/>, giving full access to GPU state and
/// frame graph resources. An optional validate callback can enforce
/// pre-conditions before execution.</para>
/// </remarks>
public sealed class CustomPassExecutor : RenderPass
{
    private readonly Action<GLContext, PassExecutionContext> _executeCallback;
    private readonly Action? _validateCallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomPassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="kind">The pass kind for frame graph scheduling.</param>
    /// <param name="executeCallback">The callback invoked during <see cref="RenderPass.Execute"/>.</param>
    public CustomPassExecutor(
        string name,
        FramePassKind kind,
        Action<GLContext, PassExecutionContext> executeCallback)
        : base(name, kind)
    {
        ArgumentNullException.ThrowIfNull(executeCallback);
        _executeCallback = executeCallback;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomPassExecutor"/> class
    /// with an optional validation callback.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="kind">The pass kind for frame graph scheduling.</param>
    /// <param name="executeCallback">The callback invoked during <see cref="RenderPass.Execute"/>.</param>
    /// <param name="validateCallback">Optional callback invoked during <see cref="RenderPass.Validate"/>.</param>
    public CustomPassExecutor(
        string name,
        FramePassKind kind,
        Action<GLContext, PassExecutionContext> executeCallback,
        Action? validateCallback)
        : base(name, kind)
    {
        ArgumentNullException.ThrowIfNull(executeCallback);
        _executeCallback = executeCallback;
        _validateCallback = validateCallback;
    }

    /// <summary>Gets the execute callback.</summary>
    public Action<GLContext, PassExecutionContext> ExecuteCallback
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _executeCallback;
    }

    /// <summary>Gets the validate callback, or <see langword="null"/> if none was provided.</summary>
    public Action? ValidateCallback
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _validateCallback;
    }

    /// <summary>
    /// Declares that this pass reads the specified resource.
    /// </summary>
    /// <param name="resourceName">The resource name in the pass context.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public new CustomPassExecutor Reads(string resourceName)
    {
        base.Reads(resourceName);
        return this;
    }

    /// <summary>
    /// Declares that this pass writes the specified resource.
    /// </summary>
    /// <param name="resourceName">The resource name in the pass context.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public new CustomPassExecutor Writes(string resourceName)
    {
        base.Writes(resourceName);
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        _validateCallback?.Invoke();
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();
        _executeCallback(context, ctx);
    }
}
