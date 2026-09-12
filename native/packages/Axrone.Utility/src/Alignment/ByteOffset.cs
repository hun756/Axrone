namespace Axrone.Utility.Alignment;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct ByteOffset(nint Value) : IComparable<ByteOffset>, ISpanFormattable
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nint(ByteOffset offset) => offset.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ByteOffset(nint value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ByteOffset other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);
}
