namespace Axrone.Physics3D;

using System.Diagnostics.CodeAnalysis;

/// <summary>Houses exceptional throwing paths outside hot methods to preserve RyuJIT inline budgets.</summary>
internal static class ThrowHelper
{
    /// <summary>Throws an invalid-operation error with the supplied message.</summary>
    /// <param name="message">The error message.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidOperation(string message) => throw new InvalidOperationException(message);

    /// <summary>Throws when an identity names no live body.</summary>
    /// <param name="id">The dead identity.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowBodyNotFound(BodyId id) =>
        throw new InvalidOperationException($"Body {id.RawValue} is unallocated or has been removed.");

    /// <summary>Throws when an identity names no live shape.</summary>
    /// <param name="id">The dead identity.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowShapeNotFound(ShapeId id) =>
        throw new InvalidOperationException($"Shape {id.RawValue} is unallocated or has been removed.");

    /// <summary>
    /// Rejects a negative distance metric; distances live in non-negative space.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNegativeDistance() =>
        throw new ArgumentOutOfRangeException("distance", "Distance metric cannot be strictly negative.");

    /// <summary>
    /// Rejects a zero GJK iteration limit; the traversal needs at least one step.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidIterationLimit() =>
        throw new ArgumentOutOfRangeException("limit", "GJK iteration limit must be strictly positive.");

    /// <summary>
    /// Rejects a zero GJK iteration limit; the traversal needs at least one step.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowInvalidIterationLimit<T>() =>
        throw new ArgumentOutOfRangeException("limit", "GJK iteration limit must be strictly positive.");

    /// <summary>
    /// Rejects simplex overflow past the tetrahedral capacity of four vertices.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSimplexOverflow() =>
        throw new InvalidOperationException("3D Simplex capacity exceeded (maximum tetrahedral capacity is 4 vertices).");

    /// <summary>
    /// Rejects an unreachable simplex state during reduction.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSimplexInvalidState() =>
        throw new InvalidOperationException("Invalid simplex state encountered during reduction.");

    /// <summary>
    /// Rejects an unreachable discriminated-union discriminant.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowUnreachable<T>() =>
        throw new InvalidOperationException("Unreachable state encountered in exhaustive discriminated union match.");
}
