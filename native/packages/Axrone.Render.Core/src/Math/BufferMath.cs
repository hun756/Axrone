namespace Axrone.Render.Core;

/// <summary>
/// Buffer alignment and padding utilities.
/// </summary>
public static class BufferMath
{
    /// <summary>
    /// Aligns a value up to the specified alignment.
    /// </summary>
    /// <param name="value">The value to align.</param>
    /// <param name="alignment">The alignment (must be a power of 2).</param>
    /// <returns>The aligned value.</returns>
    /// <example>
    /// <code>
    /// AlignTo(5, 4) = 8
    /// AlignTo(1, 16) = 16
    /// AlignTo(17, 8) = 24
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignTo(int value, int alignment)
    {
        if (alignment <= 0)
            ThrowHelper.ThrowInvalidArgument("Alignment must be positive");

        int rem = value % alignment;
        return rem == 0 ? value : value + (alignment - rem);
    }

    /// <summary>
    /// Aligns a value up to the specified alignment using bit operations (faster for power-of-2 alignments).
    /// </summary>
    /// <param name="value">The value to align.</param>
    /// <param name="alignment">The alignment (must be a power of 2).</param>
    /// <returns>The aligned value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignToPowerOf2(int value, int alignment)
    {
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            ThrowHelper.ThrowInvalidArgument("Alignment must be a power of 2");

        return (value + (alignment - 1)) & ~(alignment - 1);
    }

    /// <summary>
    /// Calculates the padding needed to align an offset.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="alignment">The alignment.</param>
    /// <returns>The padding in bytes.</returns>
    /// <example>
    /// <code>
    /// CalculatePadding(1, 4) = 3
    /// CalculatePadding(5, 8) = 3
    /// CalculatePadding(7, 4) = 1
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculatePadding(int offset, int alignment)
    {
        if (alignment <= 0)
            ThrowHelper.ThrowInvalidArgument("Alignment must be positive");

        int rem = offset % alignment;
        return rem == 0 ? 0 : alignment - rem;
    }

    /// <summary>
    /// Calculates the padding needed to align an offset using bit operations.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="alignment">The alignment (must be a power of 2).</param>
    /// <returns>The padding in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculatePaddingPowerOf2(int offset, int alignment)
    {
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            ThrowHelper.ThrowInvalidArgument("Alignment must be a power of 2");

        int mask = alignment - 1;
        return (alignment - (offset & mask)) & mask;
    }

    /// <summary>
    /// Checks if a value is aligned to the specified alignment.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="alignment">The alignment.</param>
    /// <returns>True if the value is aligned; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(int value, int alignment)
    {
        if (alignment <= 0)
            ThrowHelper.ThrowInvalidArgument("Alignment must be positive");

        return (value % alignment) == 0;
    }

    /// <summary>
    /// Checks if a value is aligned to the specified alignment using bit operations.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="alignment">The alignment (must be a power of 2).</param>
    /// <returns>True if the value is aligned; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAlignedPowerOf2(int value, int alignment)
    {
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            ThrowHelper.ThrowInvalidArgument("Alignment must be a power of 2");

        return (value & (alignment - 1)) == 0;
    }

}
