namespace Axrone.Asset;

/// <summary>Asset query filters; every set field must match.</summary>
public readonly record struct AssetQuery
{
    /// <summary>Kind filter.</summary>
    public string? Kind { get; init; }

    /// <summary>Fingerprint filter.</summary>
    public AssetFingerprint? Fingerprint { get; init; }

    /// <summary>URI filter.</summary>
    public AssetUri? Uri { get; init; }

    /// <summary>MIME type filter.</summary>
    public string? MimeType { get; init; }

    /// <summary>Locale filter.</summary>
    public AssetLocale? Locale { get; init; }

    /// <summary>Tag filter; all listed tags must match.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Property filter; every entry must match exactly.</summary>
    public IReadOnlyDictionary<string, AssetJsonValue>? Properties { get; init; }
}

/// <summary>Zero-copy stack visitor over stored assets.</summary>
/// <typeparam name="TState">Visitor state, possibly a ref struct.</typeparam>
public interface IAssetVisitor<TState> where TState : allows ref struct
{
    /// <summary>Visits one stored asset.</summary>
    void Visit(in StoredAsset asset, ref TState state);
}

/// <summary>Multi-index asset catalog: fingerprint, uri, mime, locale and tags.</summary>
internal sealed class AssetQueryCatalog
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ulong, HashSet<AssetId>> _fingerprintIndex = new();
    private readonly Dictionary<ulong, HashSet<AssetId>> _uriIndex = new();
    private readonly Dictionary<ulong, HashSet<AssetId>> _mimeTypeIndex = new();
    private readonly Dictionary<ulong, HashSet<AssetId>> _localeIndex = new();
    private readonly Dictionary<ulong, HashSet<AssetId>> _tagIndex = new();
    private readonly Dictionary<ulong, HashSet<AssetId>> _propertyIndex = new();

    private static ulong PropertySlot(string name, AssetJsonValue value)
    {
        ulong nameHash = Fnv1a64.ComputeUtf8Chars(name.AsSpan());
        ulong valueHash = Fnv1a64.ComputeUtf8Chars(value.ToString().AsSpan());
        return nameHash ^ (valueHash * 1099511628211UL);
    }

    /// <summary>Indexes a stored asset.</summary>
    public void IndexAsset(in StoredAsset asset)
    {
        lock (_gate)
        {
            AssetId id = asset.Id;
            AssetMetadata meta = asset.Metadata;

            ref HashSet<AssetId>? fpSlot = ref CollectionsMarshal.GetValueRefOrAddDefault(_fingerprintIndex, asset.Fingerprint.Value, out _);
            fpSlot ??= new HashSet<AssetId>();
            fpSlot.Add(id);

            if (meta.Uri.HasValue)
            {
                ref HashSet<AssetId>? uriSlot = ref CollectionsMarshal.GetValueRefOrAddDefault(_uriIndex, meta.Uri.Value.Hash, out _);
                uriSlot ??= new HashSet<AssetId>();
                uriSlot.Add(id);
            }

            if (meta.MimeType is not null)
            {
                ulong hash = Fnv1a64.ComputeUtf8Chars(meta.MimeType.AsSpan());
                ref HashSet<AssetId>? mimeSlot = ref CollectionsMarshal.GetValueRefOrAddDefault(_mimeTypeIndex, hash, out _);
                mimeSlot ??= new HashSet<AssetId>();
                mimeSlot.Add(id);
            }

            if (meta.Locale.HasValue)
            {
                ref HashSet<AssetId>? locSlot = ref CollectionsMarshal.GetValueRefOrAddDefault(_localeIndex, meta.Locale.Value.Hash, out _);
                locSlot ??= new HashSet<AssetId>();
                locSlot.Add(id);
            }

            for (int i = 0; i < meta.Tags.Count; i++)
            {
                ulong tagHash = Fnv1a64.ComputeUtf8Chars(meta.Tags[i].AsSpan());
                ref HashSet<AssetId>? tagSlot = ref CollectionsMarshal.GetValueRefOrAddDefault(_tagIndex, tagHash, out _);
                tagSlot ??= new HashSet<AssetId>();
                tagSlot.Add(id);
            }

            foreach (KeyValuePair<string, AssetJsonValue> property in meta.Properties)
            {
                ulong slot = PropertySlot(property.Key, property.Value);
                ref HashSet<AssetId>? propSlot = ref CollectionsMarshal.GetValueRefOrAddDefault(_propertyIndex, slot, out _);
                propSlot ??= new HashSet<AssetId>();
                propSlot.Add(id);
            }
        }
    }

    /// <summary>Removes a stored asset from every index.</summary>
    public void UnindexAsset(in StoredAsset asset)
    {
        lock (_gate)
        {
            AssetId id = asset.Id;
            AssetMetadata meta = asset.Metadata;

            if (_fingerprintIndex.TryGetValue(asset.Fingerprint.Value, out HashSet<AssetId>? fpSet))
            {
                fpSet.Remove(id);
                if (fpSet.Count == 0)
                {
                    _fingerprintIndex.Remove(asset.Fingerprint.Value);
                }
            }

            if (meta.Uri.HasValue && _uriIndex.TryGetValue(meta.Uri.Value.Hash, out HashSet<AssetId>? uriSet))
            {
                uriSet.Remove(id);
                if (uriSet.Count == 0)
                {
                    _uriIndex.Remove(meta.Uri.Value.Hash);
                }
            }

            if (meta.MimeType is not null)
            {
                ulong hash = Fnv1a64.ComputeUtf8Chars(meta.MimeType.AsSpan());
                if (_mimeTypeIndex.TryGetValue(hash, out HashSet<AssetId>? mimeSet))
                {
                    mimeSet.Remove(id);
                    if (mimeSet.Count == 0)
                    {
                        _mimeTypeIndex.Remove(hash);
                    }
                }
            }

            if (meta.Locale.HasValue && _localeIndex.TryGetValue(meta.Locale.Value.Hash, out HashSet<AssetId>? locSet))
            {
                locSet.Remove(id);
                if (locSet.Count == 0)
                {
                    _localeIndex.Remove(meta.Locale.Value.Hash);
                }
            }

            for (int i = 0; i < meta.Tags.Count; i++)
            {
                ulong tagHash = Fnv1a64.ComputeUtf8Chars(meta.Tags[i].AsSpan());
                if (_tagIndex.TryGetValue(tagHash, out HashSet<AssetId>? tagSet))
                {
                    tagSet.Remove(id);
                    if (tagSet.Count == 0)
                    {
                        _tagIndex.Remove(tagHash);
                    }
                }
            }

            foreach (KeyValuePair<string, AssetJsonValue> property in meta.Properties)
            {
                ulong slot = PropertySlot(property.Key, property.Value);
                if (_propertyIndex.TryGetValue(slot, out HashSet<AssetId>? propSet))
                {
                    propSet.Remove(id);
                    if (propSet.Count == 0)
                    {
                        _propertyIndex.Remove(slot);
                    }
                }
            }
        }
    }

    /// <summary>Finds candidate ids matching every set query field.</summary>
    public HashSet<AssetId>? FindCandidates(in AssetQuery query)
    {
        lock (_gate)
        {
            List<HashSet<AssetId>> matches = new(4);

            if (query.Fingerprint.HasValue)
            {
                if (!_fingerprintIndex.TryGetValue(query.Fingerprint.Value.Value, out HashSet<AssetId>? set))
                {
                    return null;
                }

                matches.Add(set);
            }

            if (query.Uri.HasValue)
            {
                if (!_uriIndex.TryGetValue(query.Uri.Value.Hash, out HashSet<AssetId>? set))
                {
                    return null;
                }

                matches.Add(set);
            }

            if (query.MimeType is not null)
            {
                ulong hash = Fnv1a64.ComputeUtf8Chars(query.MimeType.AsSpan());
                if (!_mimeTypeIndex.TryGetValue(hash, out HashSet<AssetId>? set))
                {
                    return null;
                }

                matches.Add(set);
            }

            if (query.Locale.HasValue)
            {
                if (!_localeIndex.TryGetValue(query.Locale.Value.Hash, out HashSet<AssetId>? set))
                {
                    return null;
                }

                matches.Add(set);
            }

            if (query.Tags is not null)
            {
                for (int i = 0; i < query.Tags.Count; i++)
                {
                    ulong tagHash = Fnv1a64.ComputeUtf8Chars(query.Tags[i].AsSpan());
                    if (!_tagIndex.TryGetValue(tagHash, out HashSet<AssetId>? set))
                    {
                        return null;
                    }

                    matches.Add(set);
                }
            }

            if (query.Properties is not null)
            {
                foreach (KeyValuePair<string, AssetJsonValue> property in query.Properties)
                {
                    if (!_propertyIndex.TryGetValue(PropertySlot(property.Key, property.Value), out HashSet<AssetId>? set))
                    {
                        return null;
                    }

                    matches.Add(set);
                }
            }

            if (matches.Count == 0)
            {
                return null;
            }

            matches.Sort(static (a, b) => a.Count.CompareTo(b.Count));
            HashSet<AssetId> intersection = new(matches[0]);
            for (int i = 1; i < matches.Count; i++)
            {
                intersection.IntersectWith(matches[i]);
                if (intersection.Count == 0)
                {
                    break;
                }
            }

            return intersection;
        }
    }

    /// <summary>Drops every index.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _fingerprintIndex.Clear();
            _uriIndex.Clear();
            _mimeTypeIndex.Clear();
            _localeIndex.Clear();
            _tagIndex.Clear();
            _propertyIndex.Clear();
        }
    }
}
