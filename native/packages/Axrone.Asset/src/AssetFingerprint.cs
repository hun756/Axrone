namespace Axrone.Asset;

/// <summary>
/// Deterministic content fingerprint: kind, key, codec-serialized payload
/// and canonical metadata folded through FNV-1a.
/// </summary>
public static class DeterministicFingerprinter
{
    /// <summary>Computes a fingerprint from stable parts.</summary>
    public static AssetFingerprint Compute<TData, TCodec>(string kind, AssetKey key, in TData data, AssetMetadata metadata)
        where TCodec : struct, IAssetCodecPolicy<TData>
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(metadata);
        ulong hash = Fnv1a64.ComputeUtf8Chars(kind.AsSpan());
        hash = Fnv1a64.ComputeUtf8Chars("|".AsSpan(), hash);
        hash = Fnv1a64.ComputeUtf8Chars(key.Value ?? key.Hash.ToString("x16", CultureInfo.InvariantCulture), hash);
        hash = Fnv1a64.ComputeUtf8Chars("|".AsSpan(), hash);

        int maxBytes = TCodec.MaxSerializedBytes(in data);
        byte[]? rented = null;
        Span<byte> buffer = maxBytes <= 512 ? stackalloc byte[maxBytes] : (rented = ArrayPool<byte>.Shared.Rent(maxBytes));

        try
        {
            if (TCodec.TrySerialize(in data, buffer, out int written))
            {
                hash = Fnv1a64.Compute(buffer[..written], hash);
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        hash = Fnv1a64.ComputeUtf8Chars("|".AsSpan(), hash);
        if (metadata.Uri.HasValue)
        {
            hash = Fnv1a64.ComputeUtf8Chars(metadata.Uri.Value.Value.AsSpan(), hash);
        }

        if (metadata.MimeType is not null)
        {
            hash = Fnv1a64.ComputeUtf8Chars(metadata.MimeType.AsSpan(), hash);
        }

        if (metadata.Locale.HasValue)
        {
            hash = Fnv1a64.ComputeUtf8Chars(metadata.Locale.Value.Value.AsSpan(), hash);
        }

        for (int i = 0; i < metadata.Tags.Count; i++)
        {
            hash = Fnv1a64.ComputeUtf8Chars(metadata.Tags[i].AsSpan(), hash);
        }

        foreach (KeyValuePair<string, string> entry in metadata.Properties)
        {
            hash = Fnv1a64.ComputeUtf8Chars(entry.Key.AsSpan(), hash);
            hash = Fnv1a64.ComputeUtf8Chars("=".AsSpan(), hash);
            hash = Fnv1a64.ComputeUtf8Chars(entry.Value.AsSpan(), hash);
        }

        return new AssetFingerprint(hash);
    }
}
