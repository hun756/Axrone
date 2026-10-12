namespace Axrone.Raycast;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Cold-path error reporting for the raycast engine. Every member throws, so
/// hot traversal code stays free of exception construction and stays
/// inlineable. Mirrors the engine spec's helper surface.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>Rejects use of a disposed pipeline.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposed(string objectName) =>
        throw new ObjectDisposedException(objectName);

    /// <summary>Rejects a value outside the permissible domain range.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentOutOfRange(string paramName) =>
        throw new ArgumentOutOfRangeException(paramName);

    /// <summary>Rejects a value outside the permissible domain range, folding into an expression.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowArgumentOutOfRange<T>(string paramName) =>
        throw new ArgumentOutOfRangeException(paramName);

    /// <summary>Rejects an empty collection where at least one element is required.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentEmpty(string paramName) =>
        throw new ArgumentException("Collection must contain at least one element.", paramName);

    /// <summary>Rejects an operation with no valid outcome.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidOperation(string message) =>
        throw new InvalidOperationException(message);

    /// <summary>Rejects an operation with no valid outcome, folding into an expression.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowInvalidOperation<T>(string message) =>
        throw new InvalidOperationException(message);
}
