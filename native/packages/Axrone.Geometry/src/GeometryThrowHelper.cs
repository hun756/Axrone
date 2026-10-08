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
}