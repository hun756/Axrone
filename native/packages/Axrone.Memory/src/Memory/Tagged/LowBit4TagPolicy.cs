namespace Axrone.Memory;

/// <summary>4-bit tag in the lowest address bits; requires 16-byte aligned allocations.</summary>
public readonly struct LowBit4TagPolicy : ITagPolicy<byte>
{
    private const nuint TagMaskValue = 0xF;

    /// <inheritdoc/>
    public static nuint TagMask => TagMaskValue;

    /// <inheritdoc/>
    public static nuint PointerMask => ~TagMaskValue;

    /// <inheritdoc/>
    public static int TagShift => 0;

    /// <inheritdoc/>
    public static nuint AlignmentRequirement => 16;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Unpack(nuint raw) => (byte)(raw & TagMaskValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint Pack(byte tag) => tag & TagMaskValue;
}
