namespace Axrone.Utility.Identifiers;

using System.Text;

/// <summary>
/// Static metadata and structural trait descriptor for strongly-typed identifiers.
/// Declares the nominal discriminator prefix, index-base semantics and domain invariants.
/// </summary>
/// <typeparam name="TSelf">The tag type itself.</typeparam>
public interface IIdentifierTag<TSelf>
    where TSelf : struct, IIdentifierTag<TSelf>
{
    /// <summary>Human-readable domain name of the identifier.</summary>
    static abstract string TagName { get; }

    /// <summary>Short prefix used by span formatting.</summary>
    static abstract string Prefix { get; }

    /// <summary>Whether indices start at one with zero reserved for invalid.</summary>
    static abstract bool IsOneBased { get; }
}

/// <summary>
/// Universal contract for register-promotable nominal identifiers: complete
/// algebraic operations, bounds conversion and zero-allocation span formatting.
/// </summary>
/// <typeparam name="TSelf">The identifier type itself.</typeparam>
/// <typeparam name="TUnderlying">The backing integer type.</typeparam>
public interface IStrongId<TSelf, TUnderlying>
    : IEquatable<TSelf>,
      IComparable<TSelf>,
      IEqualityOperators<TSelf, TSelf, bool>,
      IComparisonOperators<TSelf, TSelf, bool>,
      ISpanFormattable,
      IUtf8SpanFormattable
    where TSelf : struct, IStrongId<TSelf, TUnderlying>
    where TUnderlying : unmanaged, IBinaryInteger<TUnderlying>, IMinMaxValue<TUnderlying>
{
    /// <summary>The backing value.</summary>
    TUnderlying RawValue { get; }

    /// <summary>Whether the value names a live entity.</summary>
    bool IsValid { get; }

    /// <summary>Invalid sentinel.</summary>
    static abstract TSelf Invalid { get; }

    /// <summary>Wraps a raw value without validation.</summary>
    static abstract TSelf FromRaw(TUnderlying value);

    /// <summary>Converts a 0-based index, honoring the tag base.</summary>
    static abstract TSelf FromIndex(int index);

    /// <summary>Converts back to a 0-based index, or -1 while invalid.</summary>
    int ToIndex();
}

/// <summary>
/// Universal zero-overhead phantom-typed nominal identifier. Bitwise identical
/// to <typeparamref name="TUnderlying"/> in memory and registers, eliminating
/// primitive obsession while guaranteeing compile-time type segregation.
/// </summary>
/// <typeparam name="TTag">The domain tag.</typeparam>
/// <typeparam name="TUnderlying">The backing integer type.</typeparam>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct StrongId<TTag, TUnderlying>(TUnderlying RawValue)
    : IStrongId<StrongId<TTag, TUnderlying>, TUnderlying>
    where TTag : struct, IIdentifierTag<TTag>
    where TUnderlying : unmanaged, IBinaryInteger<TUnderlying>, IMinMaxValue<TUnderlying>
{
    /// <inheritdoc/>
    public static StrongId<TTag, TUnderlying> Invalid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(TTag.IsOneBased ? TUnderlying.Zero : TUnderlying.AllBitsSet);
    }

    /// <inheritdoc/>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TTag.IsOneBased ? RawValue != TUnderlying.Zero : RawValue != TUnderlying.AllBitsSet;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StrongId<TTag, TUnderlying> FromRaw(TUnderlying value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StrongId<TTag, TUnderlying> FromIndex(int index)
    {
        if (index < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        TUnderlying offset = TTag.IsOneBased ? TUnderlying.One : TUnderlying.Zero;
        return new(TUnderlying.CreateChecked(index) + offset);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToIndex()
    {
        if (!IsValid)
        {
            return -1;
        }

        TUnderlying offset = TTag.IsOneBased ? TUnderlying.One : TUnderlying.Zero;
        return int.CreateChecked(RawValue - offset);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(StrongId<TTag, TUnderlying> other) => RawValue.CompareTo(other.RawValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(StrongId<TTag, TUnderlying> left, StrongId<TTag, TUnderlying> right) =>
        left.RawValue < right.RawValue;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(StrongId<TTag, TUnderlying> left, StrongId<TTag, TUnderlying> right) =>
        left.RawValue > right.RawValue;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(StrongId<TTag, TUnderlying> left, StrongId<TTag, TUnderlying> right) =>
        left.RawValue <= right.RawValue;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(StrongId<TTag, TUnderlying> left, StrongId<TTag, TUnderlying> right) =>
        left.RawValue >= right.RawValue;

    /// <inheritdoc/>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        ReadOnlySpan<char> prefix = TTag.Prefix;
        int requiredLength = prefix.Length + 1;

        if (destination.Length < requiredLength)
        {
            charsWritten = 0;
            return false;
        }

        prefix.CopyTo(destination);
        destination[prefix.Length] = '_';

        if (RawValue.TryFormat(destination.Slice(requiredLength), out int valueChars, format, provider))
        {
            charsWritten = requiredLength + valueChars;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <inheritdoc/>
    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        ReadOnlySpan<char> prefix = TTag.Prefix;
        int prefixByteCount = Encoding.UTF8.GetByteCount(prefix);
        int requiredLength = prefixByteCount + 1;

        if (utf8Destination.Length < requiredLength)
        {
            bytesWritten = 0;
            return false;
        }

        Encoding.UTF8.GetBytes(prefix, utf8Destination);
        utf8Destination[prefixByteCount] = (byte)'_';

        if (RawValue.TryFormat(utf8Destination.Slice(requiredLength), out int valueBytes, format, provider))
        {
            bytesWritten = requiredLength + valueBytes;
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[32];
        if (TryFormat(buffer, out int charsWritten, format, formatProvider))
        {
            return buffer.Slice(0, charsWritten).ToString();
        }

        return $"{TTag.Prefix}_{RawValue}";
    }

    /// <inheritdoc/>
    public override string ToString() => ToString(null, null);
}
