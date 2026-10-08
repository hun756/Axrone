namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Cold-path argument validation and error reporting. Every member here is a leaf that
/// throws, so the caller keeps its hot path free of exception construction and stays
/// inlineable.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>
    /// Rejects a spatial-tree capacity below <see cref="TreeCapacity.MinimumCapacity"/>.
    /// </summary>
    [DoesNotReturn]
    [System.Diagnostics.StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidInitialCapacity() =>
        throw new ArgumentOutOfRangeException(
            "InitialCapacity",
            "Capacity must be at least 16.");

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
    /// Rejects a velocity multiplier that is negative, which would shrink the margin of
    /// a fast-moving item instead of growing it.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float ThrowNegativeMultiplier()
    {
        throw new ArgumentOutOfRangeException("VelocityMultiplier", "Velocity multiplier must be non-negative.");
    }

    /// <summary>
    /// Rejects a coordinate axis that is outside the 2D/3D projection surface.
    /// </summary>
    /// <typeparam name="T">The <see cref="ArgumentOutOfRangeException"/> type the caller folds into its own control flow.</typeparam>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowInvalidAxis<T>()
    {
        throw new ArgumentOutOfRangeException("axis", "Invalid coordinate axis for 2D/3D projection.");
    }

    /// <summary>
    /// Rejects a ray direction that cannot be normalized, which is one that is zero or
    /// so short that normalizing it would amplify rounding into an arbitrary axis.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidRayDirection()
    {
        throw new ArgumentException("Ray direction vector must be non-zero and finite.");
    }

    /// <summary>
    /// Rejects a destination span that cannot hold the full corner set a query must write.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDestinationTooSmall()
    {
        throw new ArgumentException("Destination span must contain at least 8 elements.", "destination");
    }

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
    /// Rejects text that does not spell a valid shape under the parser grammar.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidFormatException()
    {
        throw new FormatException("The string was not recognized as a valid Aabb3D.");
    }
}