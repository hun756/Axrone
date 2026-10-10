namespace Axrone.Utility.Alignment;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct ByteSize(nuint Value) : IComparable<ByteSize>, ISpanFormattable
{
    /// <summary>Zero bytes.</summary>
    public static ByteSize Zero => new(0);

    /// <summary>Creates a size from a byte count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteSize From(nuint bytes) => new(bytes);

    /// <summary>Creates a size from a byte count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteSize FromBytes(nuint bytes) => new(bytes);

    /// <summary>Creates a size from kibibytes; overflows throw.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteSize FromKilobytes(nuint kb) => new(checked(kb * 1024));

    /// <summary>Creates a size from mebibytes; overflows throw.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteSize FromMegabytes(nuint mb) => new(checked(mb * 1024 * 1024));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteSize operator +(ByteSize left, ByteSize right) => new(checked(left.Value + right.Value));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteSize operator -(ByteSize left, ByteSize right) => new(checked(left.Value - right.Value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(ByteSize size) => size.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ByteSize(nuint value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ByteSize other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);
}
