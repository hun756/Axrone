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

    /// <summary>
    /// Checks if an offset is a multiple of an element size.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="elementSize">The element size.</param>
    /// <returns>True if the offset is a multiple; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMultipleOf(int offset, int elementSize)
    {
        if (elementSize <= 0)
            ThrowHelper.ThrowInvalidArgument("Element size must be positive");

        return (offset % elementSize) == 0;
    }

    /// <summary>
    /// Validates that an offset is a multiple of an element size, throwing if not.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="elementSize">The element size.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateAlignment(int offset, int elementSize)
    {
        if (!IsMultipleOf(offset, elementSize))
            ThrowHelper.ThrowAlignmentViolation(offset, elementSize);
    }

    /// <summary>
    /// Calculates the number of elements that fit in a byte range.
    /// </summary>
    /// <param name="byteLength">The byte length.</param>
    /// <param name="elementSize">The element size in bytes.</param>
    /// <returns>The number of elements (floor division).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateElementCount(int byteLength, int elementSize)
    {
        if (elementSize <= 0)
            ThrowHelper.ThrowInvalidArgument("Element size must be positive");

        return byteLength / elementSize;
    }

    /// <summary>
    /// Calculates the byte length for a given number of elements.
    /// </summary>
    /// <param name="elementCount">The number of elements.</param>
    /// <param name="elementSize">The element size in bytes.</param>
    /// <returns>The byte length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateByteLength(int elementCount, int elementSize)
    {
        if (elementSize <= 0)
            ThrowHelper.ThrowInvalidArgument("Element size must be positive");

        return elementCount * elementSize;
    }

    /// <summary>
    /// Validates that a range [offset, offset + length) is within bounds [0, totalSize).
    /// </summary>
    /// <param name="offset">The range start offset.</param>
    /// <param name="length">The range length.</param>
    /// <param name="totalSize">The total size.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateRange(int offset, int length, int totalSize)
    {
        if (offset < 0)
            ThrowHelper.ThrowOffsetNegative();

        if (offset + length > totalSize)
            ThrowHelper.ThrowBufferBoundsExceeded();
    }

    /// <summary>
    /// Calculates the next power of 2 greater than or equal to the value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The next power of 2.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextPowerOf2(int value)
    {
        if (value <= 0)
            return 1;

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    /// <summary>
    /// Checks if a value is a power of 2.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True if the value is a power of 2; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPowerOf2(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }
}
