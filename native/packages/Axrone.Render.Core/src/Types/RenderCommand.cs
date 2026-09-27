namespace Axrone.Render.Core;

/// <summary>
/// Unmanaged render command packet for pump dispatch. Resources travel as
/// generational registry handles (never raw GL names): a recycled slot fails
/// closed at resolve time instead of aliasing a stranger object.
/// </summary>
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

    /// <summary>Source framebuffer registration.</summary>
    [FieldOffset(16)]
    public readonly DescriptorHandle<GLResourceNode> Source;

    /// <summary>Destination framebuffer registration.</summary>
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
