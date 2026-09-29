namespace Axrone.Render.Core;

/// <summary>
/// Unmanaged render command packet for pump dispatch. Resources travel as
/// generational registry handles (never raw GL names): a recycled slot fails
/// closed at resolve time instead of aliasing a stranger object.
/// </summary>
/// <remarks>
/// The 64 bytes are a tagged union: the leading <see cref="Type"/> byte selects
/// the blit, clear, or present view of the overlapping payload.
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 64)]
public readonly struct RenderCommand : IEquatable<RenderCommand>
{
    /// <summary>Dispatch discriminator.</summary>
    [FieldOffset(0)]
    public readonly RenderCommandType Type;

    /// <summary>Blit buffer mask (color/depth/stencil bits).</summary>
    [FieldOffset(4)]
    public readonly uint Mask;

    /// <summary>Blit filter mode.</summary>
    [FieldOffset(8)]
    public readonly uint Filter;

    /// <summary>Source framebuffer registration; also the present view's only handle.</summary>
    [FieldOffset(16)]
    public readonly DescriptorHandle<GLResourceNode> Source;

    /// <summary>
    /// Destination framebuffer registration. Outside the present view: present has
    /// no destination handle and leaves these bytes zeroed.
    /// </summary>
    [FieldOffset(24)]
    public readonly DescriptorHandle<GLResourceNode> Destination;

    /// <summary>Source rectangle x0.</summary>
    [FieldOffset(32)]
    public readonly int SourceX0;

    /// <summary>Source rectangle y0.</summary>
    [FieldOffset(36)]
    public readonly int SourceY0;

    /// <summary>Source rectangle x1.</summary>
    [FieldOffset(40)]
    public readonly int SourceX1;

    /// <summary>Source rectangle y1.</summary>
    [FieldOffset(44)]
    public readonly int SourceY1;

    /// <summary>Destination rectangle x0.</summary>
    [FieldOffset(48)]
    public readonly int DestinationX0;

    /// <summary>Destination rectangle y0.</summary>
    [FieldOffset(52)]
    public readonly int DestinationY0;

    /// <summary>Destination rectangle x1.</summary>
    [FieldOffset(56)]
    public readonly int DestinationX1;

    /// <summary>Destination rectangle y1.</summary>
    [FieldOffset(60)]
    public readonly int DestinationY1;

    // --------------------------------------------------------------------
    // Clear payload: the explicit layout is a byte union, so these overlap
    // the blit bytes they do not need. Both payloads stay inside the 64-byte
    // packet; the discriminator at offset 0 says which view is live.
    // --------------------------------------------------------------------

    /// <summary>Clear buffer mask (color/depth/stencil bits).</summary>
    [FieldOffset(4)]
    public readonly uint ClearMask;

    /// <summary>Target framebuffer registration.</summary>
    [FieldOffset(16)]
    public readonly DescriptorHandle<GLResourceNode> Target;

    /// <summary>Clear color red.</summary>
    [FieldOffset(32)]
    public readonly float ColorR;

    /// <summary>Clear color green.</summary>
    [FieldOffset(36)]
    public readonly float ColorG;

    /// <summary>Clear color blue.</summary>
    [FieldOffset(40)]
    public readonly float ColorB;

    /// <summary>Clear color alpha.</summary>
    [FieldOffset(44)]
    public readonly float ColorA;

    /// <summary>Clear depth value.</summary>
    [FieldOffset(48)]
    public readonly float Depth;

    /// <summary>Clear stencil value.</summary>
    [FieldOffset(52)]
    public readonly int Stencil;

    // --------------------------------------------------------------------
    // Present payload: the third view of the same bytes. A present is a blit
    // whose destination is the default framebuffer, so the rectangles, mask,
    // and filter are read at exactly the offsets the blit view uses — same
    // semantics, same bytes, no new fields. The one difference is the handle
    // count: present carries `Source` at offset 16 and nothing else.
    //
    // The absent destination handle is structural, not a shortcut. The default
    // framebuffer is owned by GL and the windowing system and is never
    // registered in the resource registry, so no generational
    // DescriptorHandle<GLResourceNode> exists for it — and a fabricated or
    // zeroed handle would resolve to whatever resource later occupies that
    // slot, which is exactly the aliasing the handle scheme exists to
    // prevent. Carrying no destination handle therefore pins the destination
    // to framebuffer 0 by construction instead of by trust. `Destination` at
    // offset 24 is not part of this view and stays zeroed by the constructor's
    // initobj, so two presents with equal arguments are bit-identical under
    // the whole-struct equality below.
    // --------------------------------------------------------------------

    /// <summary>
    /// Creates a present command: a blit of <paramref name="source"/> onto the
    /// default framebuffer.
    /// </summary>
    /// <remarks>
    /// There is no destination parameter because there is no destination handle
    /// to pass — the default framebuffer is not registry-managed. The
    /// destination rectangle describes the region of the default framebuffer to
    /// write, and the processor binds framebuffer 0 for it unconditionally.
    /// </remarks>
    /// <param name="source">The source framebuffer registration.</param>
    /// <param name="sourceX0">Source rectangle x0.</param>
    /// <param name="sourceY0">Source rectangle y0.</param>
    /// <param name="sourceX1">Source rectangle x1.</param>
    /// <param name="sourceY1">Source rectangle y1.</param>
    /// <param name="destinationX0">Destination rectangle x0 on the default framebuffer.</param>
    /// <param name="destinationY0">Destination rectangle y0 on the default framebuffer.</param>
    /// <param name="destinationX1">Destination rectangle x1 on the default framebuffer.</param>
    /// <param name="destinationY1">Destination rectangle y1 on the default framebuffer.</param>
    /// <param name="mask">The blit buffer mask (color/depth/stencil bits).</param>
    /// <param name="filter">The blit filter mode.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static RenderCommand CreatePresent(
        in DescriptorHandle<GLResourceNode> source,
        int sourceX0, int sourceY0, int sourceX1, int sourceY1,
        int destinationX0, int destinationY0, int destinationX1, int destinationY1,
        uint mask, uint filter) => new(
            source,
            sourceX0, sourceY0, sourceX1, sourceY1,
            destinationX0, destinationY0, destinationX1, destinationY1,
            mask, filter);

    /// <summary>
    /// Creates a framebuffer blit command.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static RenderCommand CreateBlit(
        in DescriptorHandle<GLResourceNode> source,
        in DescriptorHandle<GLResourceNode> destination,
        int sourceX0, int sourceY0, int sourceX1, int sourceY1,
        int destinationX0, int destinationY0, int destinationX1, int destinationY1,
        uint mask, uint filter) => new(
            source, destination,
            sourceX0, sourceY0, sourceX1, sourceY1,
            destinationX0, destinationY0, destinationX1, destinationY1,
            mask, filter);

    /// <summary>
    /// Creates a color/depth/stencil clear command.
    /// </summary>
    /// <remarks>
    /// Only the clear fields are written; the remaining bytes stay zeroed by the
    /// constructor's <c>initobj</c>, so two clears with equal arguments are
    /// bit-identical and compare equal under the whole-struct equality below.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static RenderCommand CreateClear(
        in DescriptorHandle<GLResourceNode> target,
        float r, float g, float b, float a,
        float depth, int stencil, uint clearMask) => new(
            target, r, g, b, a, depth, stencil, clearMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private RenderCommand(
        in DescriptorHandle<GLResourceNode> source,
        in DescriptorHandle<GLResourceNode> destination,
        int sourceX0, int sourceY0, int sourceX1, int sourceY1,
        int destinationX0, int destinationY0, int destinationX1, int destinationY1,
        uint mask, uint filter)
    {
        Type = RenderCommandType.Blit;
        Mask = mask;
        Filter = filter;
        Source = source;
        Destination = destination;
        SourceX0 = sourceX0;
        SourceY0 = sourceY0;
        SourceX1 = sourceX1;
        SourceY1 = sourceY1;
        DestinationX0 = destinationX0;
        DestinationY0 = destinationY0;
        DestinationX1 = destinationX1;
        DestinationY1 = destinationY1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private RenderCommand(
        in DescriptorHandle<GLResourceNode> target,
        float r, float g, float b, float a,
        float depth, int stencil, uint clearMask)
    {
        Type = RenderCommandType.Clear;
        ClearMask = clearMask;
        Target = target;
        ColorR = r;
        ColorG = g;
        ColorB = b;
        ColorA = a;
        Depth = depth;
        Stencil = stencil;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private RenderCommand(
        in DescriptorHandle<GLResourceNode> source,
        int sourceX0, int sourceY0, int sourceX1, int sourceY1,
        int destinationX0, int destinationY0, int destinationX1, int destinationY1,
        uint mask, uint filter)
    {
        Type = RenderCommandType.Present;
        Mask = mask;
        Filter = filter;
        Source = source;
        SourceX0 = sourceX0;
        SourceY0 = sourceY0;
        SourceX1 = sourceX1;
        SourceY1 = sourceY1;
        DestinationX0 = destinationX0;
        DestinationY0 = destinationY0;
        DestinationX1 = destinationX1;
        DestinationY1 = destinationY1;
        // Destination is deliberately not written: present has no destination
        // handle, and the initobj default keeps the bytes deterministic.
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RenderCommand other) =>
        Type == other.Type &&
        Mask == other.Mask &&
        Filter == other.Filter &&
        Source == other.Source &&
        Destination == other.Destination &&
        SourceX0 == other.SourceX0 &&
        SourceY0 == other.SourceY0 &&
        SourceX1 == other.SourceX1 &&
        SourceY1 == other.SourceY1 &&
        DestinationX0 == other.DestinationX0 &&
        DestinationY0 == other.DestinationY0 &&
        DestinationX1 == other.DestinationX1 &&
        DestinationY1 == other.DestinationY1;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is RenderCommand other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(
        (byte)Type, Mask, Filter,
        SourceX0, SourceY0, SourceX1, SourceY1,
        DestinationX0);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(in RenderCommand left, in RenderCommand right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(in RenderCommand left, in RenderCommand right) => !left.Equals(right);
}
