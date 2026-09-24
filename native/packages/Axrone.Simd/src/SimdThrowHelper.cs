namespace Axrone.Simd;

// Named SimdThrowHelper (not ThrowHelper) so it never collides with
// Axrone.Utility.Internal.ThrowHelper, which shares every scope via global using.
internal static class SimdThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidRegisterWidth(ushort bitWidth)
    {
        throw new ArgumentOutOfRangeException(nameof(bitWidth), bitWidth, "Register width must be 0, 64, 128, 256, or 512 bits.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidAlignment(ushort boundary)
    {
        throw new ArgumentException($"Alignment must be a non-zero power of two: {boundary}.", nameof(boundary));
    }
}
