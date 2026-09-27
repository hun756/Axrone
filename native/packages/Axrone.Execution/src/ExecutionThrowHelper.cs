namespace Axrone.Execution;

/// <summary>
/// Cold-path exception factory. All methods are non-inlined and never return,
/// keeping the hot path free of throw-site code.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>Throws for a non-power-of-two or undersized capacity.</summary>
    /// <param name="value">The rejected capacity.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidCapacity(int value) =>
        throw new ArgumentOutOfRangeException(nameof(value), value, "Capacity must be a power of two greater than or equal to 2.");
}
