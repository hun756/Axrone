namespace Axrone.Memory;

/// <summary>
/// 16-bit tag in the upper address bits (ABA counters, generations); no alignment needed.
/// </summary>
/// <remarks>
/// 64-bit only. Assumes a 48-bit virtual address space (canonical x64/ARM64). On 5-level
/// paging x86-64 or 52-bit VA ARM64 the upper bits carry address data — verify before use.
/// </remarks>
public readonly struct HighBit16TagPolicy : ITagPolicy<ushort>
{
    private const int TagShiftValue = 48;

    /// <inheritdoc/>
    public static nuint TagMask => unchecked((nuint)0xFFFF_0000_0000_0000UL);

    /// <inheritdoc/>
    public static nuint PointerMask => unchecked((nuint)0x0000_FFFF_FFFF_FFFFUL);

    /// <inheritdoc/>
    public static int TagShift => TagShiftValue;

    /// <inheritdoc/>
    public static nuint AlignmentRequirement => 1;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Unpack(nuint raw) => (ushort)(raw >> TagShiftValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint Pack(ushort tag) => ((nuint)tag) << TagShiftValue;
}
