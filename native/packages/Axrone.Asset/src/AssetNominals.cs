namespace Axrone.Asset;

/// <summary>
/// Asset identity: non-empty brand with no surrounding whitespace.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetId
{
    /// <summary>Brand value.</summary>
    public string Value { get; }

    /// <summary>Creates an id; validates the brand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetId(string value) => Value = AssetGuard.Branded(value, "asset.invalid-id");

    /// <summary>Creates an id from a span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetId FromSpan(ReadOnlySpan<char> value) => new(value.ToString());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetId left, AssetId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetId left, AssetId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetId left, AssetId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetId left, AssetId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Asset key: stable human lookup name, non-empty brand.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetKey : IComparable<AssetKey>
{
    /// <summary>Brand value.</summary>
    public string Value { get; }

    /// <summary>Creates a key; validates the brand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetKey(string value) => Value = AssetGuard.Branded(value, "asset.invalid-key");

    /// <summary>Creates a key from a span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetKey FromSpan(ReadOnlySpan<char> value) => new(value.ToString());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetKey other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left key sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetKey left, AssetKey right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left key sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetKey left, AssetKey right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left key sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetKey left, AssetKey right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left key sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetKey left, AssetKey right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Asset kind discriminator: non-empty brand naming the schema entry.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetKind : IComparable<AssetKind>
{
    /// <summary>Brand value.</summary>
    public string Value { get; }

    /// <summary>Creates a kind; validates the brand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetKind(string value) => Value = AssetGuard.Branded(value, "asset.invalid-kind");

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetKind other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left kind sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetKind left, AssetKind right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left kind sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetKind left, AssetKind right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left kind sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetKind left, AssetKind right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left kind sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetKind left, AssetKind right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Asset URI: non-empty brand naming the source location.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetUri : IComparable<AssetUri>
{
    /// <summary>Brand value.</summary>
    public string Value { get; }

    /// <summary>Creates a URI; validates the brand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetUri(string value) => Value = AssetGuard.Branded(value, "asset.invalid-source", allowInnerWhitespace: true);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetUri other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

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
    public override string ToString() => Value;
}

/// <summary>
/// Content fingerprint: non-empty brand, typically a hash digest.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetFingerprint : IComparable<AssetFingerprint>
{
    /// <summary>Brand value.</summary>
    public string Value { get; }

    /// <summary>Creates a fingerprint; validates the brand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetFingerprint(string value) => Value = AssetGuard.Branded(value, "asset.invalid-source");

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetFingerprint other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left fingerprint sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetFingerprint left, AssetFingerprint right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left fingerprint sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetFingerprint left, AssetFingerprint right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left fingerprint sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetFingerprint left, AssetFingerprint right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left fingerprint sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetFingerprint left, AssetFingerprint right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Source identity: non-empty brand binding an external source to an asset.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetSourceIdentity : IComparable<AssetSourceIdentity>
{
    /// <summary>Brand value.</summary>
    public string Value { get; }

    /// <summary>Creates an identity; validates the brand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetSourceIdentity(string value) => Value = AssetGuard.Branded(value, "asset.invalid-source-identity");

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetSourceIdentity other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetSourceIdentity left, AssetSourceIdentity right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetSourceIdentity left, AssetSourceIdentity right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetSourceIdentity left, AssetSourceIdentity right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetSourceIdentity left, AssetSourceIdentity right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Importer identity: non-empty brand naming the importer.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetImporterId : IComparable<AssetImporterId>
{
    /// <summary>Brand value.</summary>
    public string Value { get; }

    /// <summary>Creates an importer id; validates the brand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetImporterId(string value) => Value = AssetGuard.Branded(value, "asset.invalid-importer");

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetImporterId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left id sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetImporterId left, AssetImporterId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left id sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetImporterId left, AssetImporterId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left id sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetImporterId left, AssetImporterId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left id sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetImporterId left, AssetImporterId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Locale tag: non-empty brand.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AssetLocale : IComparable<AssetLocale>
{
    /// <summary>Brand value.</summary>
    public string Value { get; }

    /// <summary>Invariant locale.</summary>
    public static AssetLocale Invariant => new("iv");

    /// <summary>Creates a locale; validates the brand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetLocale(string value) => Value = AssetGuard.Branded(value, "asset.invalid-source");

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetLocale other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left locale sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetLocale left, AssetLocale right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left locale sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetLocale left, AssetLocale right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left locale sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetLocale left, AssetLocale right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left locale sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetLocale left, AssetLocale right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Monotonic asset revision; starts at one.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AssetRevision : IComparable<AssetRevision>
{
    /// <summary>Revision value.</summary>
    public uint Value { get; }

    /// <summary>First revision.</summary>
    public static AssetRevision Initial => new(1U);

    /// <summary>Creates a revision; rejects zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetRevision(uint value)
    {
        if (value == 0U)
        {
            AssetThrowHelper.ThrowInvalid("asset.invalid-revision", "0");
        }

        Value = value;
    }

    /// <summary>Next revision.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetRevision Next() => new(Value + 1U);

    /// <summary>Parses a revision; rejects zero and non-numeric input.</summary>
    public static AssetRevision FromSpan(ReadOnlySpan<char> value)
    {
        if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint parsed) || parsed == 0U)
        {
            AssetThrowHelper.ThrowInvalid("asset.invalid-revision", value.ToString());
        }

        return new AssetRevision(parsed);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AssetRevision other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left revision is smaller.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AssetRevision left, AssetRevision right) => left.Value < right.Value;

    /// <summary>Determines whether the left revision is smaller or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AssetRevision left, AssetRevision right) => left.Value <= right.Value;

    /// <summary>Determines whether the left revision is larger.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AssetRevision left, AssetRevision right) => left.Value > right.Value;

    /// <summary>Determines whether the left revision is larger or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AssetRevision left, AssetRevision right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Brand validation shared by all asset nominals.</summary>
internal static class AssetGuard
{
    /// <summary>Validates a branded value; throws with the coded error when blank.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Branded(string? value, string code, bool allowInnerWhitespace = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            AssetThrowHelper.ThrowInvalid(code, value);
        }

        string trimmed = value!.Trim();
        if (trimmed.Length == 0 || (!allowInnerWhitespace && trimmed.Length != value.Length))
        {
            AssetThrowHelper.ThrowInvalid(code, value);
        }

        return value;
    }
}
