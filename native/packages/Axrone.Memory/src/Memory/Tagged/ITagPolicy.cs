namespace Axrone.Memory;

/// <summary>
/// Bit layout contract for pointer tagging: which bits carry the tag, which carry the address,
/// and what alignment makes the tag bits free.
/// </summary>
/// <typeparam name="TTag">Tag value type.</typeparam>
public interface ITagPolicy<TTag>
    where TTag : unmanaged, IEquatable<TTag>
{
    /// <summary>Bits reserved for the tag.</summary>
    static abstract nuint TagMask { get; }

    /// <summary>Bits carrying the address.</summary>
    static abstract nuint PointerMask { get; }

    /// <summary>Shift between tag value and its packed position (0 for low-bit policies).</summary>
    static abstract int TagShift { get; }

    /// <summary>Allocation alignment that keeps tag bits free; power of two.</summary>
    static abstract nuint AlignmentRequirement { get; }

    /// <summary>Extracts the tag value from a packed word.</summary>
    static abstract TTag Unpack(nuint raw);

    /// <summary>Packs a tag value into position, silently masking overflow bits.</summary>
    static abstract nuint Pack(TTag tag);
}
