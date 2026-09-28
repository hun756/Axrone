using Axrone.Execution;

namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// Static render command processor: the execution kernel render pumps derive
/// from. Struct for JIT devirtualization; handles resolve through the context
/// registry so recycled slots fail closed instead of aliasing live objects.
/// </summary>
public readonly struct RenderCommandProcessor : ICommandProcessor<RenderCommand, RenderPumpContext>, IEquatable<RenderCommandProcessor>
{
    /// <summary>
    /// The only framebuffer a present can name as its destination. Framebuffer 0
    /// is owned by GL, never by the resource registry, so the processor binds it
    /// by constant instead of resolving a handle that cannot exist.
    /// </summary>
    private const uint DefaultFramebufferId = 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Process(ref RenderCommand command, ref RenderPumpContext context)
    {
        switch (command.Type)
        {
            case RenderCommandType.Blit:
                ExecuteBlit(in command, ref context);
                break;
            case RenderCommandType.Clear:
                ExecuteClear(in command, ref context);
                break;
            case RenderCommandType.Present:
                ExecutePresent(in command, ref context);
                break;
            default:
                ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration, $"Unknown render command type: {command.Type}", nameof(RenderCommandProcessor));
                break;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RenderCommandProcessor other) => true;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is RenderCommandProcessor;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => 0;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(in RenderCommandProcessor left, in RenderCommandProcessor right) => true;

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(in RenderCommandProcessor left, in RenderCommandProcessor right) => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteBlit(in RenderCommand command, ref RenderPumpContext context)
    {
        DescriptorHandle<GLResourceNode> sourceHandle = command.Source;
        DescriptorHandle<GLResourceNode> destinationHandle = command.Destination;
        Context.GLResourceRegistry registry = context.Context.Registry;

        if (!registry.TryResolve(in sourceHandle, out var source) || source is not Resources.GLFramebuffer sourceFbo)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Blit source handle is stale or not a framebuffer", nameof(RenderCommandProcessor));
            return; // Unreachable, satisfies compiler
        }

        if (!registry.TryResolve(in destinationHandle, out var destination) || destination is not Resources.GLFramebuffer destinationFbo)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Blit destination handle is stale or not a framebuffer", nameof(RenderCommandProcessor));
            return; // Unreachable, satisfies compiler
        }

        sourceFbo.BlitTo(
            destinationFbo,
            command.SourceX0, command.SourceY0, command.SourceX1, command.SourceY1,
            command.DestinationX0, command.DestinationY0, command.DestinationX1, command.DestinationY1,
            command.Mask, command.Filter);
    }

    /// <summary>
    /// Mirrors <see cref="PassExecutors.ClearPassExecutor.Execute"/>: bind the target,
    /// push only the clear values the mask asks for, then issue a single
    /// <c>glClear</c>. Clear values stay on the state cache, so a repeat of the
    /// same clear costs one <c>glClear</c> and nothing else.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteClear(in RenderCommand command, ref RenderPumpContext context)
    {
        DescriptorHandle<GLResourceNode> targetHandle = command.Target;
        Context.GLResourceRegistry registry = context.Context.Registry;

        if (!registry.TryResolve(in targetHandle, out var target) || target is not Resources.GLFramebuffer targetFbo)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Clear target handle is stale or not a framebuffer", nameof(RenderCommandProcessor));
            return; // Unreachable, satisfies compiler
        }

        var state = context.Context.State;
        var gl = context.Context.GL;

        state.BindFramebuffer(GLConst.Framebuffer, targetFbo.Id);

        uint mask = command.ClearMask;

        if ((mask & GLConst.ColorBufferBit) != 0)
        {
            state.SetClearColor(command.ColorR, command.ColorG, command.ColorB, command.ColorA);
        }

        if ((mask & GLConst.DepthBufferBit) != 0)
        {
            state.SetClearDepth(command.Depth);
        }

        if ((mask & GLConst.StencilBufferBit) != 0)
        {
            state.SetClearStencil(command.Stencil);
        }

        if (mask != 0)
        {
            gl.Clear(mask);
        }
    }

    /// <summary>
    /// Mirrors <see cref="PassExecutors.PresentPassExecutor.Execute"/> up to and
    /// including the blit: bind the source for reads and framebuffer 0 for
    /// draws, then issue one <c>glBlitFramebuffer</c> carrying the packet's
    /// rectangles, mask, and filter.
    /// </summary>
    /// <remarks>
    /// Only the source is resolved. The destination is the default framebuffer,
    /// which the registry does not manage, so there is no destination handle to
    /// resolve: framebuffer 0 is bound by construction, and a stale source is
    /// the only handle failure a present command can produce.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ExecutePresent(in RenderCommand command, ref RenderPumpContext context)
    {
        DescriptorHandle<GLResourceNode> sourceHandle = command.Source;
        Context.GLResourceRegistry registry = context.Context.Registry;

        if (!registry.TryResolve(in sourceHandle, out var source) || source is not Resources.GLFramebuffer sourceFbo)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Present source handle is stale or not a framebuffer", nameof(RenderCommandProcessor));
            return; // Unreachable, satisfies compiler
        }

        var state = context.Context.State;
        var gl = context.Context.GL;

        // Read and draw are bound separately so the destination side of the blit
        // can be framebuffer 0 while the source side is the resolved FBO.
        state.BindFramebuffer(GLConst.ReadFramebuffer, sourceFbo.Id);
        state.BindFramebuffer(GLConst.DrawFramebuffer, DefaultFramebufferId);

        gl.BlitFramebuffer(
            command.SourceX0, command.SourceY0, command.SourceX1, command.SourceY1,
            command.DestinationX0, command.DestinationY0, command.DestinationX1, command.DestinationY1,
            command.Mask, command.Filter);
    }
}
