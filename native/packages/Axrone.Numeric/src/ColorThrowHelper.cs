namespace Axrone.Numeric;

/// <summary>Microarchitectural cold-path segregation.</summary>
[StackTraceHidden]
internal static class ThrowHelper
{
    /// <summary>Throws for an undersized destination buffer.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentException_BufferTooSmall() =>
        throw new ArgumentException("The destination buffer is too small to receive the output elements.");

    /// <summary>Throws a format error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowFormatException(string message) =>
        throw new FormatException(message);

    /// <summary>Throws an invalid operation error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowInvalidOperationException<T>(string message) =>
        throw new InvalidOperationException(message);

    /// <summary>Throws an out-of-range error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowArgumentOutOfRangeException<T>(string parameterName) =>
        throw new ArgumentOutOfRangeException(parameterName, "Provided value is outside canonical physical limits.");
}
