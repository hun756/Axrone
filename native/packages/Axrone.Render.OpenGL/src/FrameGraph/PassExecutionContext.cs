namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// Execution context passed to render passes during frame graph execution.
/// Provides access to transient resources allocated by the frame graph.
/// </summary>
public sealed class PassExecutionContext
{
    private readonly Dictionary<string, object> _resources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the GL context.
    /// </summary>
    public GLContext Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PassExecutionContext"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    internal PassExecutionContext(GLContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <summary>
    /// Gets the resource with the specified name.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="name">The resource name.</param>
    /// <returns>The resource instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetResource<T>(string name) where T : class
    {
        if (!_resources.TryGetValue(name, out var resource))
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, $"Resource '{name}' not found in pass execution context", nameof(PassExecutionContext));
            return default!; // Unreachable, satisfies compiler
        }

        if (resource is not T typed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, $"Resource '{name}' is not of type '{typeof(T).Name}'", nameof(PassExecutionContext));
            return default!; // Unreachable, satisfies compiler
        }

        return typed;
    }

    /// <summary>
    /// Sets the resource with the specified name.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="resource">The resource instance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetResource(string name, object resource)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(resource);
        _resources[name] = resource;
    }

    /// <summary>
    /// Determines whether a resource with the specified name exists.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <returns><c>true</c> if the resource exists; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasResource(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _resources.ContainsKey(name);
    }

    /// <summary>
    /// Gets a texture resource by name.
    /// </summary>
    /// <param name="name">The texture name.</param>
    /// <returns>The texture instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GLTexture GetTexture(string name) => GetResource<GLTexture>(name);

    /// <summary>
    /// Gets a framebuffer resource by name.
    /// </summary>
    /// <param name="name">The framebuffer name.</param>
    /// <returns>The framebuffer instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GLFramebuffer GetFramebuffer(string name) => GetResource<GLFramebuffer>(name);

    /// <summary>
    /// Gets a buffer resource by name.
    /// </summary>
    /// <param name="name">The buffer name.</param>
    /// <returns>The buffer instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GLBuffer GetBuffer(string name) => GetResource<GLBuffer>(name);

    /// <summary>
    /// Clears all resources from the context.
    /// </summary>
    internal void Clear() => _resources.Clear();
}
