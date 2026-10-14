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
}
