namespace Axrone.Patterns;

/// <summary>
/// Cold-path argument validation and error reporting for node arenas. Every
/// member throws, so callers keep their hot paths free of exception
/// construction and stay inlineable.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>Rejects a non-positive capacity or an out-of-range slot.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentOutOfRange(string paramName)
    {
        throw new ArgumentOutOfRangeException(paramName);
    }

    /// <summary>Rejects a handle or range that names no live slot.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIndexOutOfRange()
    {
        throw new IndexOutOfRangeException();
    }

    /// <summary>Rejects use of a disposed arena.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposed(string objectName)
    {
        throw new ObjectDisposedException(objectName);
    }
}
