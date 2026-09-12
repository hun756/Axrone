namespace Axrone.Utility.Alignment;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct ByteSize(nuint Value) : IComparable<ByteSize>, ISpanFormattable
{
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
