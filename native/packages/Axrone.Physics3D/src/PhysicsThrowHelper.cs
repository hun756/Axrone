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
}
