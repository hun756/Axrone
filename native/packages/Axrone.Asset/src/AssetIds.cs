namespace Axrone.Asset;

/// <summary>
/// Strongly-typed asset identity: 64-bit value rendered as 16 hex digits.
/// Zero is the empty sentinel.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetId(ulong Value) :
    IComparable<AssetId>,
    IEquatable<AssetId>,
    IComparisonOperators<AssetId, AssetId, bool>,
    ISpanFormattable,
    ISpanParsable<AssetId>
{
    /// <summary>Empty sentinel.</summary>
    public static readonly AssetId Empty = new(0UL);

    /// <summary>Mints a random identity; zero never escapes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetId CreateNew()
    {
        Span<byte> randomBytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(randomBytes);
        ulong value = BinaryPrimitives.ReadUInt64LittleEndian(randomBytes);
        return new AssetId(value == 0 ? 1UL : value);
    }

    /// <summary>Parses a string identity, accepting an optional ast_ prefix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetId FromString(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Parse(value.AsSpan(), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetId other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("x16", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, "x16", provider ?? CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format ?? "x16", formatProvider ?? CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public static AssetId Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out AssetId result))
        {
            ThrowHelper.ThrowReferenceError("Failed to parse AssetId from span.");
        }

        return result;
    }

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out AssetId result)
    {
        if (s.StartsWith("ast_", StringComparison.OrdinalIgnoreCase))
        {
            s = s[4..];
        }

        if (ulong.TryParse(s, NumberStyles.HexNumber, provider ?? CultureInfo.InvariantCulture, out ulong parsed))
        {
            result = new AssetId(parsed);
            return true;
        }

        if (s.Length > 0)
        {
            result = new AssetId(Fnv1a64.ComputeUtf8Chars(s));
            return true;
        }

        result = Empty;
        return false;
    }

    /// <inheritdoc/>
    public static AssetId Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <inheritdoc/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out AssetId result) =>
        TryParse(s.AsSpan(), provider, out result);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetId left, AssetId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetId left, AssetId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetId left, AssetId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetId left, AssetId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ulong(AssetId id) => id.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetId(ulong value) => new(value);
}

/// <summary>
/// Lookup key pairing a 64-bit hash with its optional source string.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetKey :
    IComparable<AssetKey>,
    IEquatable<AssetKey>,
    IComparisonOperators<AssetKey, AssetKey, bool>
{
    /// <summary>Hash of the key text.</summary>
    public ulong Hash { get; }

    /// <summary>Source text, when retained.</summary>
    public string? Value { get; }

    /// <summary>Creates a keyed hash, canonicalizing separators and whitespace.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = Canonicalize(value);
        Hash = Fnv1a64.ComputeUtf8Chars(Value.AsSpan());
    }

    /// <summary>Canonicalizes key text: trims and unifies separators.</summary>
    public static string Canonicalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string trimmed = value.Trim();
        return trimmed.IndexOf('\\') >= 0 ? trimmed.Replace('\\', '/') : trimmed;
    }

    /// <summary>Creates a key from a precomputed hash.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetKey(ulong hash, string? value = null)
    {
        Hash = hash;
        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetKey other) => Hash.CompareTo(other.Hash);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetKey other) => Hash == other.Hash;

    /// <inheritdoc/>
    public override int GetHashCode() => (int)Hash;

    /// <inheritdoc/>
    public override string ToString() => Value ?? Hash.ToString("x16", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetKey left, AssetKey right) => left.Hash < right.Hash;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetKey left, AssetKey right) => left.Hash <= right.Hash;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetKey left, AssetKey right) => left.Hash > right.Hash;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetKey left, AssetKey right) => left.Hash >= right.Hash;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetKey(string key) => new(key);
}

/// <summary>
/// Monotonic asset revision with increment and decrement operators.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetRevision(long Value) :
    IComparable<AssetRevision>,
    IEquatable<AssetRevision>,
    IComparisonOperators<AssetRevision, AssetRevision, bool>,
    IIncrementOperators<AssetRevision>,
    IDecrementOperators<AssetRevision>,
    ISpanFormattable
{
    /// <summary>Zero revision.</summary>
    public static readonly AssetRevision Zero = new(0L);

    /// <summary>First revision.</summary>
    public static readonly AssetRevision Initial = new(1L);

    /// <summary>Next revision.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetRevision Next() => new(Value + 1);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetRevision other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetRevision other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format, provider ?? CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetRevision operator ++(AssetRevision revision) => new(revision.Value + 1);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetRevision operator --(AssetRevision revision) => new(revision.Value - 1);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetRevision left, AssetRevision right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetRevision left, AssetRevision right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetRevision left, AssetRevision right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetRevision left, AssetRevision right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator long(AssetRevision revision) => revision.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetRevision(long value) => new(value);
}

/// <summary>
/// Content fingerprint: 64-bit digest rendered as <c>fp:</c> plus hex.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetFingerprint(ulong Value) :
    IComparable<AssetFingerprint>,
    IEquatable<AssetFingerprint>,
    IComparisonOperators<AssetFingerprint, AssetFingerprint, bool>,
    ISpanFormattable
{
    /// <summary>Empty fingerprint.</summary>
    public static readonly AssetFingerprint Empty = new(0UL);

    /// <summary>Parses an optional fp:-prefixed hex digest.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetFingerprint FromHex(ReadOnlySpan<char> hexSpan)
    {
        if (hexSpan.StartsWith("fp:", StringComparison.OrdinalIgnoreCase))
        {
            hexSpan = hexSpan[3..];
        }

        if (!ulong.TryParse(hexSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hash))
        {
            ThrowHelper.ThrowConfigurationError("Invalid fingerprint hex format.");
        }

        return new AssetFingerprint(hash);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetFingerprint other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetFingerprint other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    public override string ToString()
    {
        Span<char> dest = stackalloc char[19];
        dest[0] = 'f';
        dest[1] = 'p';
        dest[2] = ':';
        _ = Value.TryFormat(dest[3..], out int written, "x16", CultureInfo.InvariantCulture);
        return new string(dest[..(3 + written)]);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (destination.Length < 19)
        {
            charsWritten = 0;
            return false;
        }

        destination[0] = 'f';
        destination[1] = 'p';
        destination[2] = ':';
        bool ok = Value.TryFormat(destination[3..], out int written, "x16", provider ?? CultureInfo.InvariantCulture);
        charsWritten = ok ? 3 + written : 0;
        return ok;
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetFingerprint left, AssetFingerprint right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetFingerprint left, AssetFingerprint right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetFingerprint left, AssetFingerprint right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetFingerprint left, AssetFingerprint right) => left.Value >= right.Value;
}

/// <summary>
/// Nominal source URI pairing a hash with its text.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetUri : IEquatable<AssetUri>, IComparable<AssetUri>
{
    /// <summary>Hash of the URI text.</summary>
    public ulong Hash { get; }

    /// <summary>URI text.</summary>
    public string Value { get; }

    /// <summary>Creates a URI nominal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetUri(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        Hash = Fnv1a64.ComputeUtf8Chars(value.AsSpan());
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetUri other) => Hash == other.Hash && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetUri other) => string.CompareOrdinal(Value, other.Value);

    /// <inheritdoc/>
    public override int GetHashCode() => (int)Hash;

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Determines whether the left URI sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetUri left, AssetUri right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left URI sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetUri left, AssetUri right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left URI sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetUri left, AssetUri right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left URI sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetUri left, AssetUri right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetUri(string uri) => new(uri);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(AssetUri uri) => uri.Value;
}

/// <summary>
/// Nominal locale tag; blanks fall back to invariant.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetLocale : IEquatable<AssetLocale>
{
    /// <summary>Invariant locale.</summary>
    public static readonly AssetLocale Invariant = new("invariant");

    /// <summary>Hash of the locale text.</summary>
    public ulong Hash { get; }

    /// <summary>Locale text.</summary>
    public string Value { get; }

    /// <summary>Creates a locale nominal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetLocale(string? value)
    {
        Value = string.IsNullOrWhiteSpace(value) ? "invariant" : value;
        Hash = Fnv1a64.ComputeUtf8Chars(Value.AsSpan());
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetLocale other) => Hash == other.Hash && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override int GetHashCode() => (int)Hash;

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetLocale(string? locale) => new(locale);
}

/// <summary>
/// Nominal importer identity pairing a hash with its text.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetImporterId : IEquatable<AssetImporterId>
{
    /// <summary>Hash of the importer text.</summary>
    public ulong Hash { get; }

    /// <summary>Importer text.</summary>
    public string Value { get; }

    /// <summary>Creates an importer nominal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetImporterId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        Hash = Fnv1a64.ComputeUtf8Chars(value.AsSpan());
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetImporterId other) => Hash == other.Hash && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => (int)Hash;

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Nominal source identity pairing a hash with its text.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetSourceIdentity : IEquatable<AssetSourceIdentity>
{
    /// <summary>Hash of the identity text.</summary>
    public ulong Hash { get; }

    /// <summary>Identity text.</summary>
    public string Value { get; }

    /// <summary>Creates a source identity nominal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetSourceIdentity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        Hash = Fnv1a64.ComputeUtf8Chars(value.AsSpan());
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssetSourceIdentity other) => Hash == other.Hash && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => (int)Hash;

    /// <inheritdoc/>
    public override string ToString() => Value;
}
