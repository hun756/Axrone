namespace Axrone.Geometry;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

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
        throw new InvalidOperationException($"Spatial entity with ID {id.RawValue} is unallocated or has been removed.");
    }

    /// <summary>
    /// Rejects an index past the end of a native buffer.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIndexOutOfRange()
    {
        throw new IndexOutOfRangeException("Native memory index was out of allocated range.");
    }

    /// <summary>
    /// Rejects use of a disposed mesh object.
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
    /// Rejects text that does not spell a valid shape under the parser grammar.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidFormatException()
    {
        throw new FormatException("The string was not recognized as a valid Aabb3D.");
    }

    /// <summary>
    /// Rejects a fattening margin that is negative or NaN, which would shrink stored
    /// bounds instead of padding them and let a moving item outrun its own box.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float ThrowInvalidFatMargin()
    {
        throw new ArgumentOutOfRangeException("fatMargin", "Fat margin must not be negative.");
    }

    /// <summary>
    /// Rejects a velocity multiplier below one or NaN, which would budget less slack
    /// than the stationary margin for an item that actually moves.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float ThrowInvalidVelocityMultiplier()
    {
        throw new ArgumentOutOfRangeException("velocityMultiplier", "Velocity multiplier must be at least 1.");
    }

    /// <summary>
    /// Rejects a batch insert whose handle span cannot take one handle per entry.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowHandlesSpanTooShort()
    {
        throw new ArgumentException("Destination handles span is shorter than the entries span.", "handles");
    }

    /// <summary>
    /// Rejects a value outside the permissible domain range of the named parameter.
    /// </summary>
    /// <param name="paramName">The parameter that received the bad value.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentOutOfRange(string paramName)
    {
        throw new ArgumentOutOfRangeException(paramName, "Value is outside the permissible domain range.");
    }

    /// <summary>
    /// Rejects an operation with no valid outcome, folding the throw into an
    /// expression (for example the default arm of a discriminated-union match).
    /// </summary>
    /// <typeparam name="TReturn">The type the caller folds into its own control flow.</typeparam>
    /// <param name="message">The failure to report.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowInvalidOperation<TReturn>(string message)
    {
        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Rejects a quad-tree root that is inverted: every insert would fall
    /// outside it, so the tree would be born useless.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidQuadBounds()
    {
        throw new ArgumentException("Quad-tree root bounds must be valid (min corner must not exceed max corner).", "rootBounds");
    }

    /// <summary>
    /// Rethrows the failure that faulted a spatial component, or reports the
    /// terminal faulted state when no failure was captured.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowComponentFaulted(ExceptionDispatchInfo? info)
    {
        info?.Throw();
        throw new InvalidOperationException("The spatial component is in a terminal faulted state.");
    }

    /// <summary>
    /// Rejects a mutation against a component whose submission stream has settled.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowComponentCompleted()
    {
        throw new InvalidOperationException("The spatial component has settled/completed and rejects further mutations.");
    }

    /// <summary>
    /// Rejects use of a disposed spatial component, folding the throw into an expression.
    /// </summary>
    /// <typeparam name="TReturn">The type the caller folds into its own control flow.</typeparam>
    /// <param name="objectName">The disposed object to report.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowObjectDisposed<TReturn>(string objectName)
    {
        throw new ObjectDisposedException(objectName);
    }

    /// <summary>
    /// Rejects an octree capacity below its floor: eight node slots (one child
    /// block) and sixteen item slots.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidCapacity(string paramName)
    {
        throw new ArgumentOutOfRangeException(paramName, "Capacities must satisfy NodeCapacity >= 8 and ItemCapacity >= 16.");
    }

    /// <summary>
    /// Rejects bounds that are NaN or not strictly ordered: an octree domain
    /// must have positive volume, so touching corners are already invalid.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidBounds(in Aabb3D bounds)
    {
        throw new ArgumentException($"Invalid bounding dimensions: Min [{bounds.Min}] must be strictly less than Max [{bounds.Max}].");
    }

    /// <summary>
    /// Rejects bounds that are NaN or not strictly ordered, folding the throw
    /// into an expression.
    /// </summary>
    /// <typeparam name="TReturn">The type the caller folds into its own control flow.</typeparam>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowInvalidBounds<TReturn>(in Aabb3D bounds)
    {
        throw new ArgumentException($"Invalid bounding dimensions: Min [{bounds.Min}] must be strictly less than Max [{bounds.Max}].");
    }

    /// <summary>
    /// Rejects an insert whose bounds escape the root domain.
    /// </summary>
    /// <typeparam name="TReturn">The type the caller folds into its own control flow.</typeparam>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowOutOfBounds<TReturn>(in Aabb3D itemBounds, in Aabb3D rootBounds)
    {
        throw new ArgumentOutOfRangeException(nameof(itemBounds), $"Bounds [{itemBounds.Min} -> {itemBounds.Max}] exceed domain [{rootBounds.Min} -> {rootBounds.Max}].");
    }

    /// <summary>
    /// Rejects an unrecognized insert outcome, which means the match is missing a case.
    /// </summary>
    /// <typeparam name="TReturn">The type the caller folds into its own control flow.</typeparam>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowInvalidOutcome<TReturn>(InsertOutcome outcome)
    {
        throw new InvalidOperationException($"Unrecognized operation outcome: {outcome}.");
    }

    /// <summary>
    /// Rejects a traversal that outgrew its fixed 128-slot stack: the tree is
    /// deeper than the stack, which means corruption or a degenerate build.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowStackDepthExceeded()
    {
        throw new InvalidOperationException("Octree traversal stack capacity of 128 exceeded.");
    }

    /// <summary>
    /// Reports a broken intrusive link chain: the slot is gone but no list holds it.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCorruptionDetected()
    {
        throw new InvalidOperationException("Internal octree intrusive link-chain corruption detected.");
    }

    /// <summary>
    /// Rejects a null or whitespace argument with a message.
    /// </summary>
    /// <param name="message">The failure to report.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentException(string message)
    {
        throw new ArgumentException(message);
    }

    /// <summary>
    /// Rejects a null argument.
    /// </summary>
    /// <param name="paramName">The parameter that received null.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentNullException(string paramName)
    {
        throw new ArgumentNullException(paramName);
    }

    /// <summary>
    /// Rejects a null argument, folding the throw into an expression.
    /// </summary>
    /// <typeparam name="TReturn">The type the caller folds into its own control flow.</typeparam>
    /// <param name="paramName">The parameter that received null.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static TReturn ThrowArgumentNullException<TReturn>(string paramName)
    {
        throw new ArgumentNullException(paramName);
    }
}