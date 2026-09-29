namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// Consumer context for render command pumps: the owning GL context, threaded
/// by ref so pumps allocate nothing and processors resolve generational
/// handles through the context registry.
/// </summary>
public readonly ref struct RenderPumpContext
{
    /// <summary>The GL context owning the registry and state cache.</summary>
    public Context.GLContext Context { get; }

    /// <summary>
    /// Creates a pump context.
    /// </summary>
    /// <param name="context">The owning GL context.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public RenderPumpContext(Context.GLContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }
}
