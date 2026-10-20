namespace Axrone.Asset;

/// <summary>
/// Canonical serialization for fingerprints and snapshots: sorted keys,
/// invariant culture, explicit escaping. No reflection, no allocations
/// beyond the result string.
/// </summary>
public static class StableJson
{
    /// <summary>Canonical form of metadata: sorted properties, ordered tags.</summary>
    public static string StableMetadataString(AssetMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var builder = new System.Text.StringBuilder(128);
        builder.Append("{\"locale\":\"");
        AppendEscaped(builder, metadata.Locale?.Value ?? string.Empty);
        builder.Append("\",\"mimeType\":\"");
        AppendEscaped(builder, metadata.MimeType ?? string.Empty);
        builder.Append("\",\"properties\":{");

        string[] keys = new string[metadata.Properties.Count];
        int ki = 0;
        foreach (string key in metadata.Properties.Keys)
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

            builder.Append('\"');
            AppendEscaped(builder, keys[i]);
            builder.Append("\":\"");
            AppendEscaped(builder, metadata.Properties[keys[i]]);
            builder.Append('\"');
        }

        builder.Append("},\"tags\":[");
        for (int i = 0; i < metadata.Tags.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append('\"');
            AppendEscaped(builder, metadata.Tags[i]);
            builder.Append('\"');
        }

        builder.Append("],\"uri\":\"");
        AppendEscaped(builder, metadata.Uri?.Value ?? string.Empty);
        builder.Append("\"}");
        return builder.ToString();
    }

    /// <summary>Appends a JSON-escaped string body.</summary>
    public static void AppendEscaped(System.Text.StringBuilder builder, string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(value);

        foreach (char c in value)
        {
            switch (c)
            {
                case '\"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }
    }
}
