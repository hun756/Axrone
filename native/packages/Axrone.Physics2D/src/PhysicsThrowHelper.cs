namespace Axrone.Physics2D;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Cold-path argument validation and error reporting. Every member here is a leaf that
/// throws, so the caller keeps its hot path free of exception construction and stays
/// inlineable.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>
    /// Rejects an identity that names no live item: evicted, never issued, or
    /// past the end of the slot table.
    /// </summary>
    /// <param name="id">The identity to report.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowItemNotFound(SpatialItemId id)
    {
        throw new InvalidOperationException($"Spatial entity with ID {id.Value} is unallocated or has been removed.");
    }

    /// <summary>
    /// Rejects use of a disposed physics object.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposed(string objectName)
    {
        throw new ObjectDisposedException(objectName);
    }

    /// <summary>
    /// Rejects a pipeline operation in the wrong stage.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidOperation(string message)
    {
        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Rejects a fattening margin that is negative, which would shrink stored bounds
    /// instead of padding them and let a moving item outrun its own box.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float ThrowNegativeMargin()
    {
        throw new ArgumentOutOfRangeException("FatteningMargin", "Margin value must be non-negative.");
    }

    /// <summary>
    /// Rejects a negative shape radius; a shape with negative extent is never valid input.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNegativeRadius()
    {
        throw new ArgumentOutOfRangeException("radius", "Shape radius must not be negative.");
    }

    /// <summary>
    /// Rejects a polygon with fewer than three vertices.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowTooFewVertices()
    {
        throw new ArgumentException("A convex polygon needs at least three vertices.", "vertices");
    }

    /// <summary>
    /// Rejects vertices that are not a strictly convex counter-clockwise loop.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNonConvexPolygon()
    {
        throw new ArgumentException("Polygon vertices must form a strictly convex counter-clockwise loop.", "vertices");
    }

    /// <summary>
    /// Rejects vertices containing NaN components.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidVertices()
    {
        throw new ArgumentException("Polygon vertices must be finite.", "vertices");
    }
}
