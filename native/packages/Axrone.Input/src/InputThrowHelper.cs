namespace Axrone.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Houses exceptional throwing paths outside hot methods to preserve RyuJIT inline budgets.</summary>
internal static class ThrowHelper
{
    /// <summary>Throws when a queue capacity is not a positive power of two.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMustBePowerOfTwo()
    {
        throw new ArgumentOutOfRangeException("capacity", "Queue capacity must be a positive power of two (2, 4, 8, ...).");
    }

    /// <summary>Throws when an input object is used after disposal.</summary>
    /// <param name="objectName">The disposed object name.</param>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposed(string objectName)
    {
        throw new ObjectDisposedException(objectName);
    }
}
