namespace Axrone.Numeric;

internal static class NumericThrowHelper
{
    [DoesNotReturn]
    [System.Diagnostics.StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArgumentException(string message) =>
        throw new ArgumentException(message);

    [DoesNotReturn]
    [System.Diagnostics.StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArgumentException(string paramName, string message) =>
        throw new ArgumentException(message, paramName);

    [DoesNotReturn]
    [System.Diagnostics.StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArgumentOutOfRangeException(string paramName) =>
        throw new ArgumentOutOfRangeException(paramName);

    [DoesNotReturn]
    [System.Diagnostics.StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowFormatException(string message) =>
        throw new FormatException(message);
}
