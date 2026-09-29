namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// Framebuffer (blit) vocabulary, expressed as static abstracts and shaped exactly
/// like the fullscreen vocabulary in <see cref="IGLFullscreenInvoker{TSelf}"/>:
/// implementations translate the command vocabulary onto their own stack
/// (production: <see cref="GLFramebufferInvoker"/>, which lands every binding in
/// <see cref="GLStateCache"/> first). Static abstracts let the JIT monomorphize
/// each call site, so a command carries no vtable and issues no interface call.
/// </summary>
/// <remarks>
/// <para><typeparamref name="TSelf"/> is the invoker type itself (curiously
/// recurring), which lets the contract live on <c>in</c> context parameters. A blit
/// is a single irreversible copy, so there is deliberately no ambient scope and no
/// restore step: the command carries the complete read/draw binding pair plus the
/// rectangle, mask and filter it needs, and replaying it is deterministic.</para>
/// <para>The framebuffer names stay raw <see cref="uint"/> GL object names, and the
/// rectangle coordinates stay <see cref="int"/> because they are pixel offsets
/// that may be negative or exceed the attachment size — widths would silently
/// change the meaning of a rectangle. An implementation must never bind a
/// framebuffer with a raw <c>glBindFramebuffer</c>: the two bindings must go
/// through the invoker's state cache so its shadow read/draw framebuffer pair
/// stays authoritative for every later pass.</para>
/// </remarks>
public interface IGLFramebufferInvoker<TSelf> where TSelf : struct, IGLFramebufferInvoker<TSelf>
{
    /// <summary>Binds a framebuffer to one target, recording the change in the invoker's state cache.</summary>
    /// <param name="ctx">The invoker context.</param>
    /// <param name="target">The framebuffer target (<see cref="GLConst.ReadFramebuffer"/> or <see cref="GLConst.DrawFramebuffer"/>).</param>
    /// <param name="framebuffer">The GL framebuffer name to bind.</param>
    static abstract void BindFramebuffer(in TSelf ctx, uint target, uint framebuffer);

    /// <summary>
    /// Copies a rectangle between the currently bound read and draw framebuffers.
    /// The read and draw bindings must already be in place; the call is skipped by
    /// the driver when both are framebuffer zero.
    /// </summary>
    /// <param name="ctx">The invoker context.</param>
    /// <param name="srcX0">Source rectangle lower-left x.</param>
    /// <param name="srcY0">Source rectangle lower-left y.</param>
    /// <param name="srcX1">Source rectangle upper-right x.</param>
    /// <param name="srcY1">Source rectangle upper-right y.</param>
    /// <param name="dstX0">Destination rectangle lower-left x.</param>
    /// <param name="dstY0">Destination rectangle lower-left y.</param>
    /// <param name="dstX1">Destination rectangle upper-right x.</param>
    /// <param name="dstY1">Destination rectangle upper-right y.</param>
    /// <param name="mask">The buffer mask selecting which buffers are copied.</param>
    /// <param name="filter">The filter applied when the rectangles differ in size.</param>
    static abstract void BlitFramebuffer(
        in TSelf ctx,
        int srcX0, int srcY0, int srcX1, int srcY1,
        int dstX0, int dstY0, int dstX1, int dstY1,
        uint mask, uint filter);
}

/// <summary>
/// A recorded framebuffer command that can be replayed against any concrete
/// invoker.
/// </summary>
/// <remarks>
/// <para>Deliberately a plain (non-<c>ref struct</c>) contract, matching
/// <see cref="IFullscreenDrawCommand{TInvoker}"/>: commands are passed as
/// <c>ref</c> parameters, so a command never escapes to the heap, while a plain
/// interface still lets the existing class-based pass executors build and issue
/// commands without changing their own signatures.</para>
/// <para>The command holds data plus exactly one replay method. It never inspects
/// the invoker's type, never allocates, and never throws on the replay path.</para>
/// </remarks>
/// <typeparam name="TInvoker">The invoker type this command replays against.</typeparam>
public interface IFramebufferCommand<TInvoker> where TInvoker : struct, IGLFramebufferInvoker<TInvoker>
{
    /// <summary>Replays this command against <paramref name="invoker"/>.</summary>
    /// <param name="invoker">The invoker context. Threaded by reference so the struct stays on the stack.</param>
    void Execute(ref TInvoker invoker);
}

/// <summary>
/// Forwards a framebuffer command to its invoker. One call, monomorphized per
/// closed <c>(TInvoker, TCommand)</c> pair — the same idiom as
/// <see cref="FullscreenDispatcher"/>, so the forwarder inlines and no interface
/// type is ever materialized.
/// </summary>
public static class BlitDispatcher
{
    /// <summary>Replays <paramref name="command"/> against <paramref name="invoker"/>.</summary>
    /// <typeparam name="TInvoker">The invoker struct type.</typeparam>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="invoker">The invoker context, threaded by reference.</param>
    /// <param name="command">The command to replay, threaded by reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Dispatch<TInvoker, TCommand>(ref TInvoker invoker, ref TCommand command)
        where TInvoker : struct, IGLFramebufferInvoker<TInvoker>
        where TCommand : IFramebufferCommand<TInvoker> =>
        command.Execute(ref invoker);
}

/// <summary>
/// Records one framebuffer blit — source and destination framebuffer names, the
/// source and destination rectangles, the buffer mask and the filter — and replays
/// it against any <see cref="IGLFramebufferInvoker{TSelf}"/>. The payload is a
/// positional record struct, so building a command allocates nothing and two
/// commands with identical fields compare and hash identically.
/// </summary>
/// <remarks>
/// The replayed sequence is the <see cref="GLFramebuffer.BlitTo"/> sequence:
/// bind the source name to <see cref="GLConst.ReadFramebuffer"/>, bind the
/// destination name to <see cref="GLConst.DrawFramebuffer"/>, then issue the blit.
/// The order matters — a blit reads the read framebuffer and writes the draw
/// framebuffer, so both bindings must be current before the call — and both
/// bindings go through the state cache so a later pass that reuses either target
/// sees a cache hit instead of a stale shadow value.
/// </remarks>
/// <typeparam name="TInvoker">The invoker type this command replays against.</typeparam>
/// <param name="SourceFramebuffer">The GL framebuffer name to bind as the blit source.</param>
/// <param name="DestinationFramebuffer">The GL framebuffer name to bind as the blit destination.</param>
/// <param name="SourceX0">Source rectangle lower-left x.</param>
/// <param name="SourceY0">Source rectangle lower-left y.</param>
/// <param name="SourceX1">Source rectangle upper-right x.</param>
/// <param name="SourceY1">Source rectangle upper-right y.</param>
/// <param name="DestinationX0">Destination rectangle lower-left x.</param>
/// <param name="DestinationY0">Destination rectangle lower-left y.</param>
/// <param name="DestinationX1">Destination rectangle upper-right x.</param>
/// <param name="DestinationY1">Destination rectangle upper-right y.</param>
/// <param name="Mask">The buffer mask selecting which buffers are copied.</param>
/// <param name="Filter">The filter applied when the rectangles differ in size.</param>
internal readonly record struct BlitFramebufferCommand<TInvoker>(
    uint SourceFramebuffer,
    uint DestinationFramebuffer,
    int SourceX0,
    int SourceY0,
    int SourceX1,
    int SourceY1,
    int DestinationX0,
    int DestinationY0,
    int DestinationX1,
    int DestinationY1,
    uint Mask,
    uint Filter) : IFramebufferCommand<TInvoker>
    where TInvoker : struct, IGLFramebufferInvoker<TInvoker>
{
    /// <summary>Replays the recorded blit against <paramref name="invoker"/>.</summary>
    /// <param name="invoker">The invoker context, threaded by reference.</param>
    public void Execute(ref TInvoker invoker)
    {
        // Read binding first, then the draw binding, then the copy: both targets
        // must be current before glBlitFramebuffer reads one and writes the other.
        TInvoker.BindFramebuffer(in invoker, GLConst.ReadFramebuffer, SourceFramebuffer);
        TInvoker.BindFramebuffer(in invoker, GLConst.DrawFramebuffer, DestinationFramebuffer);

        TInvoker.BlitFramebuffer(
            in invoker,
            SourceX0, SourceY0, SourceX1, SourceY1,
            DestinationX0, DestinationY0, DestinationX1, DestinationY1,
            Mask, Filter);
    }
}

/// <summary>
/// Production framebuffer invoker: replays framebuffer commands onto a
/// <see cref="GLContext"/>, funnelling both framebuffer bindings through
/// <see cref="GLStateCache"/>.
/// </summary>
/// <remarks>
/// The state cache is the reason this type exists. A blit binds the read and draw
/// targets separately, and the cache tracks them as two independent shadow values.
/// Binding either target with a raw <c>glBindFramebuffer</c> would leave the cache
/// believing an older framebuffer was still bound, so a later pass would skip its
/// binding call and read from or draw into the wrong attachment — a silent,
/// frame-wide rendering bug. Routing both binds through the cache keeps shadow
/// state and driver state in lockstep. The blit itself needs no shadow state: it is
/// a copy, not a bound object, so it goes straight to the API.
/// </remarks>
public readonly struct GLFramebufferInvoker : IGLFramebufferInvoker<GLFramebufferInvoker>, IEquatable<GLFramebufferInvoker>
{
    private readonly GLContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLFramebufferInvoker"/> struct.
    /// </summary>
    /// <param name="context">The GL context to issue calls through.</param>
    public GLFramebufferInvoker(GLContext context)
    {
        if (context is null)
        {
            ThrowHelper.ThrowArgumentNull(nameof(context));
        }

        _context = context;
    }

    /// <summary>Compares two invokers by the GL context they target.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if both invokers target the same GL context.</returns>
    public static bool operator ==(GLFramebufferInvoker left, GLFramebufferInvoker right) => left.Equals(right);

    /// <summary>Compares two invokers by the GL context they target.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if the invokers target different GL contexts.</returns>
    public static bool operator !=(GLFramebufferInvoker left, GLFramebufferInvoker right) => !left.Equals(right);

    /// <summary>Compares this invoker with another by the GL context they target.</summary>
    /// <param name="other">The other invoker.</param>
    /// <returns>True if both invokers target the same GL context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GLFramebufferInvoker other) => ReferenceEquals(_context, other._context);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GLFramebufferInvoker other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _context is null ? 0 : RuntimeHelpers.GetHashCode(_context);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void BindFramebuffer(in GLFramebufferInvoker ctx, uint target, uint framebuffer) =>
        ctx._context.State.BindFramebuffer(target, framebuffer);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void BlitFramebuffer(
        in GLFramebufferInvoker ctx,
        int srcX0, int srcY0, int srcX1, int srcY1,
        int dstX0, int dstY0, int dstX1, int dstY1,
        uint mask, uint filter) =>
        ctx._context.GL.BlitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter);
}
