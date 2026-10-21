namespace Axrone.Asset;

/// <summary>JSON value kind.</summary>
public enum AssetJsonValueKind : byte
{
    /// <summary>Null.</summary>
    Null = 0,

    /// <summary>Boolean.</summary>
    Boolean = 1,

    /// <summary>Number.</summary>
    Number = 2,

    /// <summary>String.</summary>
    String = 3,

    /// <summary>Array.</summary>
    Array = 4,

    /// <summary>Object.</summary>
    Object = 5,
}

/// <summary>
/// AOT-safe JSON value: primitives plus immutable arrays and objects.
/// Canonical form (sorted object keys, invariant numbers) feeds fingerprinting.
/// </summary>
public readonly struct AssetJsonValue : IEquatable<AssetJsonValue>
{
    /// <summary>Value kind.</summary>
    public AssetJsonValueKind Kind { get; }

    private readonly double _number;
    private readonly string? _string;
    private readonly ImmutableArray<AssetJsonValue> _array;
    private readonly ImmutableDictionary<string, AssetJsonValue> _object;

    private AssetJsonValue(AssetJsonValueKind kind, double number, string? text,
        ImmutableArray<AssetJsonValue> array, ImmutableDictionary<string, AssetJsonValue>? obj)
    {
        Kind = kind;
        _number = number;
        _string = text;
        _array = array;
        _object = obj ?? ImmutableDictionary<string, AssetJsonValue>.Empty;
    }

    /// <summary>Null value.</summary>
    public static AssetJsonValue Null => new(AssetJsonValueKind.Null, 0.0, null, default, null);

    /// <summary>Creates a boolean.</summary>
    public static AssetJsonValue FromBoolean(bool value) =>
        new(AssetJsonValueKind.Boolean, value ? 1.0 : 0.0, null, default, null);

    /// <summary>Creates a number.</summary>
    public static AssetJsonValue FromNumber(double value) =>
        new(AssetJsonValueKind.Number, value, null, default, null);

    /// <summary>Creates a string.</summary>
    public static AssetJsonValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AssetJsonValue(AssetJsonValueKind.String, 0.0, value, default, null);
    }

    /// <summary>Creates an array.</summary>
    public static AssetJsonValue FromArray(ImmutableArray<AssetJsonValue> values) =>
        new(AssetJsonValueKind.Array, 0.0, null, values.IsDefault ? [] : values, null);

    /// <summary>Creates an object.</summary>
    public static AssetJsonValue FromObject(ImmutableDictionary<string, AssetJsonValue> values) =>
        new(AssetJsonValueKind.Object, 0.0, null, default, values);

    /// <inheritdoc/>
    public static implicit operator AssetJsonValue(string value) => FromString(value);

    /// <inheritdoc/>
    public static implicit operator AssetJsonValue(double value) => FromNumber(value);

    /// <inheritdoc/>
    public static implicit operator AssetJsonValue(long value) => FromNumber(value);

    /// <inheritdoc/>
    public static implicit operator AssetJsonValue(bool value) => FromBoolean(value);

    /// <summary>Boolean payload.</summary>
    public bool AsBoolean => Kind == AssetJsonValueKind.Boolean && _number != 0.0;

    /// <summary>Number payload.</summary>
    public double AsNumber => _number;

    /// <summary>String payload.</summary>
    public string? AsString => _string;

    /// <summary>Array payload.</summary>
    public ImmutableArray<AssetJsonValue> AsArray => _array.IsDefault ? [] : _array;

    /// <summary>Object payload.</summary>
    public ImmutableDictionary<string, AssetJsonValue> AsObject => _object;

    /// <inheritdoc/>
    public bool Equals(AssetJsonValue other)
    {
        if (Kind != other.Kind)
        {
            return false;
        }

        return Kind switch
        {
            AssetJsonValueKind.Null => true,
            AssetJsonValueKind.Boolean or AssetJsonValueKind.Number => _number.Equals(other._number),
            AssetJsonValueKind.String => string.Equals(_string, other._string, StringComparison.Ordinal),
            AssetJsonValueKind.Array => AsArray.SequenceEqual(other.AsArray),
            AssetJsonValueKind.Object => AsObject.Count == other.AsObject.Count &&
                AsObject.All(kvp => other.AsObject.TryGetValue(kvp.Key, out AssetJsonValue v) && v.Equals(kvp.Value)),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AssetJsonValue other && Equals(other);

    /// <inheritdoc/>
    public static bool operator ==(AssetJsonValue left, AssetJsonValue right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(AssetJsonValue left, AssetJsonValue right) => !left.Equals(right);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add((byte)Kind);
        switch (Kind)
        {
            case AssetJsonValueKind.Boolean:
            case AssetJsonValueKind.Number:
                hash.Add(_number);
                break;
            case AssetJsonValueKind.String:
                hash.Add(_string, StringComparer.Ordinal);
                break;
            case AssetJsonValueKind.Array:
                foreach (AssetJsonValue item in AsArray)
                {
                    hash.Add(item);
                }

                break;
            case AssetJsonValueKind.Object:
                foreach (KeyValuePair<string, AssetJsonValue> kvp in AsObject)
                {
                    hash.Add(kvp.Key, StringComparer.Ordinal);
                    hash.Add(kvp.Value);
                }

                break;
            default:
                break;
        }

        return hash.ToHashCode();
    }

    /// <summary>Canonical JSON form: sorted object keys, invariant numbers.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder(32);
        AppendCanonical(builder, this);
        return builder.ToString();
    }

    internal static void AppendCanonical(StringBuilder builder, in AssetJsonValue value)
    {
        switch (value.Kind)
        {
            case AssetJsonValueKind.Null:
                builder.Append("null");
                break;
            case AssetJsonValueKind.Boolean:
                builder.Append(value._number != 0.0 ? "true" : "false");
                break;
            case AssetJsonValueKind.Number:
                builder.Append(value._number.ToString(CultureInfo.InvariantCulture));
                break;
            case AssetJsonValueKind.String:
                builder.Append('"');
                StableJson.AppendEscaped(builder, value._string ?? string.Empty);
                builder.Append('"');
                break;
            case AssetJsonValueKind.Array:
                builder.Append('[');
                ImmutableArray<AssetJsonValue> items = value.AsArray;
                for (int i = 0; i < items.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    AppendCanonical(builder, items[i]);
                }

                builder.Append(']');
                break;
            default:
                builder.Append('{');
                string[] keys = new string[value.AsObject.Count];
                int ki = 0;
                foreach (string key in value.AsObject.Keys)
                {
                    keys[ki++] = key;
                }

                Array.Sort(keys, StringComparer.Ordinal);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append('"');
                    StableJson.AppendEscaped(builder, keys[i]);
                    builder.Append("\":");
                    AppendCanonical(builder, value.AsObject[keys[i]]);
                }

                builder.Append('}');
                break;
        }
    }
}
