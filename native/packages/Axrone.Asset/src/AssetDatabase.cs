namespace Axrone.Asset;

/// <summary>
/// Asset database: generational stores over id, key and alias indexes with
/// revision history, dependency graph, source bindings, query catalog and an
/// Axrone.Event-backed fan-out. All mutations serialize on one lock; events fire
/// after the lock drops so listeners never deadlock reentrancy.
/// </summary>
/// <typeparam name="TSchema">Schema tag; selects the kind space.</typeparam>
/// <typeparam name="TCodec">Object codec used for snapshots.</typeparam>
/// <typeparam name="TLifecycle">Lifecycle hooks for stored payloads.</typeparam>
public sealed class AssetDatabase<TSchema, TCodec, TLifecycle> : IDisposable, IAsyncDisposable
    where TCodec : struct, IAssetCodecPolicy<object>
    where TLifecycle : struct, IAssetLifecyclePolicy<object>
{
    private readonly Lock _gate = new();
    private readonly Func<long> _clock;
    private readonly string _defaultLocale;
    private readonly int _maxRevisions;
    private readonly AssetBinaryPersistenceOptions _binaryOptions;
    private readonly IBinaryStore _binaryStore;
    private readonly EventBatchDispatcher _dispatcher = new();

    private readonly Dictionary<AssetId, StoredAsset> _assetsById = new();
    private readonly Dictionary<AssetId, RevisionHistoryRingBuffer> _revisionsById = new();
    private readonly Dictionary<AssetKey, AssetId> _keyIndex = new();
    private readonly Dictionary<AssetKey, AssetId> _aliasIndex = new();
    private readonly Dictionary<AssetId, HashSet<AssetId>> _dependentsById = new();
    private readonly Dictionary<AssetSourceIdentity, AssetSourceBinding> _sourceBindings = new();
    private readonly AssetQueryCatalog _catalog = new();
    private readonly List<IAssetImporter> _importers = new();

    private CachePaddedCounters _counters;
    private int _disposed;

    /// <summary>Creates a database with default tuning.</summary>
    public AssetDatabase()
        : this(null)
    {
    }

    /// <summary>Creates a database with the given options.</summary>
    public AssetDatabase(AssetDatabaseOptions? options)
    {
        AssetDatabaseOptions opt = options ?? new AssetDatabaseOptions();
        _defaultLocale = opt.Locale;
        _maxRevisions = opt.MaxRevisions;
        _clock = opt.Clock ?? UnixMillisecondsNow;
        _binaryOptions = new AssetBinaryPersistenceOptions
        {
            Mode = opt.BinaryMode,
            InlineThresholdBytes = opt.InlineThresholdBytes,
            Store = opt.BinaryStore,
        };
        _binaryStore = _binaryOptions.Store ?? new InMemoryBinaryStore();
        ThrowHelper.MessageResolver = opt.MessageResolver;

        _importers.Add(new RawBytesImporter());
        _importers.Add(new TextImporter());
    }

    /// <summary>Stored asset count.</summary>
    public int Size
    {
        get
        {
            EnsureNotDisposed();
            lock (_gate)
            {
                return _assetsById.Count;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureNotDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            ThrowHelper.ThrowDisposed();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long UnixMillisecondsNow() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Registers an importer; higher priority wins.</summary>
    public void RegisterImporter(IAssetImporter importer)
    {
        ArgumentNullException.ThrowIfNull(importer);
        EnsureNotDisposed();
        lock (_gate)
        {
            _importers.Add(importer);
            _importers.Sort(static (a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>Whether a selector resolves to a live asset.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(in AssetSelector selector)
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            return TryResolveId(in selector, out _);
        }
    }

    /// <summary>Gets an asset or null when the selector misses or the type differs.</summary>
    public AssetRecord<TData>? Get<TData>(in AssetSelector selector)
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            if (!TryResolveId(in selector, out AssetId id))
            {
                return null;
            }

            if (!_assetsById.TryGetValue(id, out StoredAsset? asset))
            {
                return null;
            }

            if (selector.Kind == SelectorKind.VersionedReference &&
                selector.Revision != asset.Revision &&
                _revisionsById.TryGetValue(id, out RevisionHistoryRingBuffer? ring) &&
                ring.TryFind(selector.Revision, out StoredAsset? revision))
            {
                return revision.ToRecord<TData>();
            }

            if (selector.Kind == SelectorKind.VersionedReference && selector.Revision != asset.Revision)
            {
                return null;
            }

            if (asset.Data is not TData)
            {
                return null;
            }

            return asset.ToRecord<TData>();
        }
    }

    /// <summary>Gets an asset or throws <see cref="KeyNotFoundException"/>.</summary>
    public AssetRecord<TData> Require<TData>(in AssetSelector selector)
    {
        AssetRecord<TData>? record = Get<TData>(in selector);
        if (record is null)
        {
            throw new KeyNotFoundException(string.Create(
                CultureInfo.InvariantCulture,
                $"Asset selector could not be resolved (kind {selector.Kind})."));
        }

        return record.Value;
    }

    /// <summary>Inserts or updates one asset.</summary>
    public AssetRecord<TData> Upsert<TData>(in AssetWriteInput<TData> input)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Kind);
        TData inputData = input.Data;

        StoredAsset stored;
        bool isInsert;
        lock (_gate)
        {
            long now = _clock();
            AssetMetadata meta = input.Metadata ?? new AssetMetadata { Locale = new AssetLocale(_defaultLocale) };
            AssetFingerprint fingerprint = DeterministicFingerprinter.Compute<TData, DefaultDataCodecPolicy<TData>>(
                input.Kind, input.Key, in inputData, meta);

            AssetId finalId;
            if (_keyIndex.TryGetValue(input.Key, out AssetId bound))
            {
                if (input.Id.HasValue && input.Id.Value != bound)
                {
                    ThrowHelper.ThrowConflictError(
                        string.Create(CultureInfo.InvariantCulture, $"Asset key '{input.Key}' is already bound to '{bound}'."),
                        "key-bound");
                }

                finalId = bound;
            }
            else
            {
                finalId = input.Id ?? AssetId.CreateNew();
            }

            if (input.DependencyIds is not null)
            {
                foreach (AssetId dep in input.DependencyIds)
                {
                    if (!_assetsById.ContainsKey(dep))
                    {
                        ThrowHelper.ThrowDependencyError(
                            string.Create(CultureInfo.InvariantCulture, $"Dependency '{dep}' does not exist."),
                            dep.ToString());
                    }
                }
            }

            AssetRevision finalRev;
            if (_assetsById.TryGetValue(finalId, out StoredAsset? existing))
            {
                isInsert = false;
                if (!string.Equals(existing.Kind, input.Kind, StringComparison.Ordinal))
                {
                    ThrowHelper.ThrowConflictError(
                        string.Create(CultureInfo.InvariantCulture, $"Asset kind mismatch for '{finalId}'."),
                        "kind-mismatch");
                }

                ref RevisionHistoryRingBuffer? ring = ref CollectionsMarshal.GetValueRefOrAddDefault(_revisionsById, finalId, out _);
                ring ??= new RevisionHistoryRingBuffer(_maxRevisions);
                ring.Push(existing);

                _catalog.UnindexAsset(existing);

                finalRev = existing.Revision.Next();
                stored = existing.CloneWithNewRevision(
                    finalRev,
                    input.Data,
                    fingerprint,
                    now,
                    meta,
                    input.DependencyIds ?? [],
                    input.Aliases,
                    input.Name);
                RemoveDependencyEdges(finalId, existing.DependencyIds);
            }
            else
            {
                isInsert = true;
                finalRev = AssetRevision.Initial;
                stored = new StoredAsset
                {
                    Kind = input.Kind,
                    Id = finalId,
                    Key = input.Key,
                    Aliases = input.Aliases ?? [],
                    Name = input.Name,
                    Data = input.Data,
                    Revision = finalRev,
                    Fingerprint = fingerprint,
                    CreatedAtEpochMs = now,
                    UpdatedAtEpochMs = now,
                    Metadata = meta,
                    DependencyIds = input.DependencyIds ?? [],
                };
            }

            _assetsById[finalId] = stored;
            _keyIndex[stored.Key] = finalId;
            foreach (AssetKey alias in stored.Aliases)
            {
                _aliasIndex[alias] = finalId;
            }

            AddDependencyEdges(finalId, stored.DependencyIds);
            _catalog.IndexAsset(stored);
            Interlocked.Increment(ref _counters.Upserts);
        }

        AssetRecord<TData> record = stored.ToRecord<TData>();
        object boxed = record.Data!;
        if (isInsert)
        {
            TLifecycle.OnCreated(in boxed);
        }
        else
        {
            AssetRevision revision = record.Revision;
            TLifecycle.OnUpdated(in boxed, in revision);
        }

        _dispatcher.Enqueue(new AssetEvent(
            AssetEventType.Upsert,
            stored.Id,
            stored.Revision,
            Fnv1a64.ComputeUtf8Chars(stored.Kind.AsSpan())));
        return record;
    }

    /// <summary>Inserts or updates many assets.</summary>
    public IReadOnlyList<AssetRecord<object>> UpsertMany(IReadOnlyList<AssetWriteInput<object>> inputs)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(inputs);
        var records = new List<AssetRecord<object>>(inputs.Count);
        for (int i = 0; i < inputs.Count; i++)
        {
            AssetWriteInput<object> input = inputs[i];
            records.Add(Upsert(in input));
        }

        return records;
    }

    /// <summary>Deletes an asset, optionally cascading through dependents.</summary>
    public bool Delete(in AssetSelector selector, bool cascade = false)
    {
        EnsureNotDisposed();
        List<AssetId> toDelete = new();
        List<StoredAsset> removed = new();

        lock (_gate)
        {
            if (!TryResolveId(in selector, out AssetId id))
            {
                return false;
            }

            if (cascade)
            {
                AssetDependencyGraph.TopologicallySortForDelete(id, _dependentsById, toDelete);
            }
            else
            {
                if (_dependentsById.TryGetValue(id, out HashSet<AssetId>? dependents) && dependents.Count > 0)
                {
                    ThrowHelper.ThrowConflictError(
                        string.Create(CultureInfo.InvariantCulture, $"Cannot delete asset '{id}': dependents exist."),
                        "has-dependents");
                }

                toDelete.Add(id);
            }

            foreach (AssetId target in toDelete)
            {
                if (!_assetsById.Remove(target, out StoredAsset? existing))
                {
                    continue;
                }

                _keyIndex.Remove(existing.Key);
                foreach (AssetKey alias in existing.Aliases)
                {
                    _aliasIndex.Remove(alias);
                }

                _catalog.UnindexAsset(existing);
                RemoveDependencyEdges(target, existing.DependencyIds);
                _revisionsById.Remove(target);
                removed.Add(existing);
                Interlocked.Increment(ref _counters.Deletes);
            }
        }

        foreach (StoredAsset asset in removed)
        {
            TLifecycle.OnDeleted(asset.Data!);
            _dispatcher.Enqueue(new AssetEvent(
                AssetEventType.Delete,
                asset.Id,
                asset.Revision,
                Fnv1a64.ComputeUtf8Chars(asset.Kind.AsSpan())));
        }

        return removed.Count > 0;
    }

    /// <summary>Runs a zero-copy visitor over every live asset.</summary>
    public void ForEachActive<TState, TVisitor>(ref TState state, TVisitor visitor)
        where TState : allows ref struct
        where TVisitor : struct, IAssetVisitor<TState>
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            foreach (StoredAsset asset in _assetsById.Values)
            {
                visitor.Visit(in asset, ref state);
            }
        }
    }

    /// <summary>Runs a zero-allocation snapshot enumerator over live assets.</summary>
    public AssetSnapshotEnumerator GetEnumerator() => new(this);

    /// <summary>Stack-only enumerator over live assets.</summary>
    public readonly ref struct AssetSnapshotEnumerator
    {
        private readonly Dictionary<AssetId, StoredAsset>.Enumerator _inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AssetSnapshotEnumerator(AssetDatabase<TSchema, TCodec, TLifecycle> database) => _inner = database._assetsById.GetEnumerator();

        /// <summary>Advances to the next asset.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => _inner.MoveNext();

        /// <summary>Current asset.</summary>
        public StoredAsset Current => _inner.Current.Value;

        /// <summary>Returns itself.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AssetSnapshotEnumerator GetEnumerator() => this;
    }

    /// <summary>Finds assets matching every set query field.</summary>
    public IReadOnlyList<AssetRecord<object>> Find(in AssetQuery query)
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            var results = new List<AssetRecord<object>>();
            HashSet<AssetId>? candidates = _catalog.FindCandidates(in query);
            if (candidates is not null)
            {
                foreach (AssetId id in candidates)
                {
                    if (_assetsById.TryGetValue(id, out StoredAsset? asset) && MatchesKind(asset, query.Kind))
                    {
                        results.Add(asset.ToRecord<object>());
                    }
                }

                return results;
            }

            if (query.Kind is null)
            {
                return results;
            }

            foreach (StoredAsset asset in _assetsById.Values)
            {
                if (MatchesKind(asset, query.Kind))
                {
                    results.Add(asset.ToRecord<object>());
                }
            }

            return results;
        }

        static bool MatchesKind(StoredAsset asset, string? kind) =>
            kind is null || string.Equals(asset.Kind, kind, StringComparison.Ordinal);
    }

    /// <summary>Lists every stored asset.</summary>
    public IReadOnlyList<AssetRecord<object>> List()
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            var results = new List<AssetRecord<object>>(_assetsById.Count);
            foreach (StoredAsset asset in _assetsById.Values)
            {
                results.Add(asset.ToRecord<object>());
            }

            return results;
        }
    }

    /// <summary>Imports one source through the matching importer.</summary>
    public async ValueTask<AssetImportReceipt<TData>> ImportAsync<TData>(
        IAssetSource source,
        AssetKey baseKey,
        AssetUri? sourceUri = null,
        AssetSourceIdentity? sourceIdentity = null,
        AssetMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(source);

        AssetImportContext context = new(source, baseKey, sourceUri, sourceIdentity, metadata);
        IAssetImporter? selected = null;
        lock (_gate)
        {
            for (int i = 0; i < _importers.Count; i++)
            {
                if (_importers[i].CanImport(context))
                {
                    selected = _importers[i];
                    break;
                }
            }
        }

        if (selected is null)
        {
            ThrowHelper.ThrowImporterNotFound(string.Create(
                CultureInfo.InvariantCulture,
                $"No eligible importer found for source kind '{source.Kind}'."));
        }

        AssetImportResult result = await selected.ImportAsync(context, cancellationToken).ConfigureAwait(false);

        AssetRecord<TData> primary = Upsert(new AssetWriteInput<TData>(
            result.Primary.Kind,
            result.Primary.Id,
            result.Primary.Key,
            result.Primary.Name,
            (TData)result.Primary.Data!,
            result.Primary.Aliases,
            result.Primary.Metadata,
            result.Primary.DependencyIds));

        List<AssetRecord<object>> imported = new() { primary.ToUntypedRecord() };
        if (result.Additional is not null)
        {
            for (int i = 0; i < result.Additional.Count; i++)
            {
                imported.Add(Upsert(result.Additional[i]));
            }
        }

        if (sourceIdentity.HasValue)
        {
            BindSourceIdentity(sourceIdentity.Value, AssetSelector.FromId(primary.Id));
        }

        return new AssetImportReceipt<TData>(
            selected.Id,
            source.Kind,
            sourceUri,
            baseKey,
            sourceIdentity,
            _clock(),
            result.Diagnostics ?? context.Diagnostics,
            primary,
            imported);
    }

    /// <summary>Binds a source identity to an asset.</summary>
    public void BindSourceIdentity(AssetSourceIdentity identity, in AssetSelector selector)
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            if (!TryResolveId(in selector, out AssetId id))
            {
                ThrowHelper.ThrowReferenceError("Target selector could not be resolved for binding.");
            }

            _sourceBindings[identity] = new AssetSourceBinding(identity, id, _clock());
        }
    }

    /// <summary>Removes a source binding.</summary>
    public bool UnbindSourceIdentity(AssetSourceIdentity identity)
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            return _sourceBindings.Remove(identity);
        }
    }

    /// <summary>Resolves a source binding to its asset.</summary>
    public AssetRecord<object>? ResolveSourceIdentity(AssetSourceIdentity identity)
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            if (_sourceBindings.TryGetValue(identity, out AssetSourceBinding binding) &&
                _assetsById.TryGetValue(binding.TargetId, out StoredAsset? asset))
            {
                return asset.ToRecord<object>();
            }

            return null;
        }
    }

    /// <summary>Lists every source binding.</summary>
    public IReadOnlyList<AssetSourceBinding> ListSourceBindings()
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            return new List<AssetSourceBinding>(_sourceBindings.Values);
        }
    }

    /// <summary>Subscribes to asset events.</summary>
    public IAssetSubscription Subscribe(Action<AssetEvent> listener)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(listener);
        return _dispatcher.Subscribe(listener);
    }

    /// <summary>Captures a version-4 snapshot.</summary>
    public AssetDatabaseSnapshot Snapshot()
    {
        EnsureNotDisposed();
        lock (_gate)
        {
            var assets = new List<AssetSnapshotRecord>(_assetsById.Count);
            foreach (KeyValuePair<AssetId, StoredAsset> kvp in _assetsById)
            {
                StoredAsset asset = kvp.Value;
                List<AssetSnapshotRecord>? history = null;
                if (_revisionsById.TryGetValue(asset.Id, out RevisionHistoryRingBuffer? ring))
                {
                    StoredAsset[] revs = ring.ToArray();
                    history = new List<AssetSnapshotRecord>(revs.Length);
                    foreach (StoredAsset rev in revs)
                    {
                        history.Add(ToSnapshotRecord(rev, SerializeDataForSnapshot(rev.Data)));
                    }
                }

                AssetSnapshotRecord record = ToSnapshotRecord(asset, SerializeDataForSnapshot(asset.Data));
                assets.Add(history is null ? record : record with { History = history });
            }

            List<AssetSnapshotSourceBindingRecord>? bindings = null;
            if (_sourceBindings.Count > 0)
            {
                bindings = new List<AssetSnapshotSourceBindingRecord>(_sourceBindings.Count);
                foreach (AssetSourceBinding binding in _sourceBindings.Values)
                {
                    bindings.Add(new AssetSnapshotSourceBindingRecord(binding.Identity.Value, binding.TargetId.Value, binding.BoundAtEpochMs));
                }
            }

            return new AssetDatabaseSnapshot
            {
                Locale = _defaultLocale,
                CapturedAtEpochMs = _clock(),
                Assets = assets,
                SourceBindings = bindings,
            };
        }
    }

    /// <summary>Restores a snapshot, optionally replacing the database.</summary>
    public void Hydrate(AssetDatabaseSnapshot snapshot, AssetHydrateOptions? options = null)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version != 4)
        {
            ThrowHelper.ThrowSnapshotError(string.Create(CultureInfo.InvariantCulture, $"Unsupported snapshot version {snapshot.Version}."));
        }

        bool replace = options?.Replace ?? false;
        lock (_gate)
        {
            if (replace)
            {
                ClearInternal();
            }

            foreach (AssetSnapshotRecord record in snapshot.Assets)
            {
                AssetId id = new(record.Id);
                if (!replace && _assetsById.ContainsKey(id))
                {
                    continue;
                }

                object? data = DeserializeDataFromSnapshot(record.SerializedData);
                AssetMetadata metadata = record.Metadata;
                AssetKey[] aliases = new AssetKey[record.Aliases.Count];
                for (int ai = 0; ai < aliases.Length; ai++)
                {
                    aliases[ai] = new AssetKey(record.Aliases[ai]);
                }

                AssetId[] dependencies = new AssetId[record.DependencyIds.Count];
                for (int di = 0; di < dependencies.Length; di++)
                {
                    dependencies[di] = new AssetId(record.DependencyIds[di]);
                }

                var stored = new StoredAsset
                {
                    Kind = record.Kind,
                    Id = id,
                    Key = new AssetKey(record.Key),
                    Aliases = aliases,
                    Name = record.Name,
                    Data = data,
                    Revision = new AssetRevision((uint)record.Revision),
                    Fingerprint = AssetFingerprint.FromHex(record.Fingerprint.AsSpan()),
                    CreatedAtEpochMs = record.CreatedAtEpochMs,
                    UpdatedAtEpochMs = record.UpdatedAtEpochMs,
                    Metadata = metadata,
                    DependencyIds = dependencies,
                };

                _assetsById[id] = stored;
                _keyIndex[stored.Key] = id;
                foreach (AssetKey alias in stored.Aliases)
                {
                    _aliasIndex[alias] = id;
                }

                AddDependencyEdges(id, stored.DependencyIds);
                _catalog.IndexAsset(stored);
            }

            if (snapshot.SourceBindings is not null)
            {
                foreach (AssetSnapshotSourceBindingRecord binding in snapshot.SourceBindings)
                {
                    AssetSourceIdentity identity = new(binding.Identity);
                    _sourceBindings[identity] = new AssetSourceBinding(identity, new AssetId(binding.TargetId), binding.BoundAtEpochMs);
                }
            }
        }
    }

    private static AssetSnapshotRecord ToSnapshotRecord(StoredAsset asset, string serializedData)
    {
        string[] aliases = new string[asset.Aliases.Count];
        for (int i = 0; i < aliases.Length; i++)
        {
            aliases[i] = asset.Aliases[i].Value ?? asset.Aliases[i].Hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        ulong[] dependencies = new ulong[asset.DependencyIds.Count];
        for (int i = 0; i < dependencies.Length; i++)
        {
            dependencies[i] = asset.DependencyIds[i].Value;
        }

        return new AssetSnapshotRecord
        {
            Kind = asset.Kind,
            Id = asset.Id.Value,
            Key = asset.Key.Value ?? asset.Key.Hash.ToString("x16", CultureInfo.InvariantCulture),
            Aliases = aliases,
            Name = asset.Name,
            SerializedData = serializedData,
            Revision = asset.Revision.Value,
            Fingerprint = asset.Fingerprint.ToString(),
            CreatedAtEpochMs = asset.CreatedAtEpochMs,
            UpdatedAtEpochMs = asset.UpdatedAtEpochMs,
            Metadata = asset.Metadata,
            DependencyIds = dependencies,
            History = null,
        };
    }

    private string SerializeDataForSnapshot(object? data)
    {
        if (data is null)
        {
            ThrowHelper.ThrowSnapshotError("Cannot serialize a null data block.");
        }

        int maxBytes = TCodec.MaxSerializedBytes(in data);
        byte[]? rented = null;
        Span<byte> buffer = maxBytes <= 512 ? stackalloc byte[maxBytes] : (rented = ArrayPool<byte>.Shared.Rent(maxBytes));

        try
        {
            if (!TCodec.TrySerialize(in data, buffer, out int written))
            {
                ThrowHelper.ThrowSnapshotError("Failed to serialize data block.");
            }

            ReadOnlySpan<byte> slice = buffer[..written];
            bool inline = _binaryOptions.Mode switch
            {
                AssetBinaryPersistenceMode.Inline => true,
                AssetBinaryPersistenceMode.External => false,
                _ => written <= _binaryOptions.InlineThresholdBytes,
            };

            if (inline)
            {
                var inlineValue = new AssetBinaryValue
                {
                    Storage = "inline",
                    Encoding = "base64",
                    Data = Convert.ToBase64String(slice),
                    ByteLength = written,
                };
                return JsonSerializer.Serialize(inlineValue, AssetJsonContext.Default.AssetBinaryValue);
            }

            byte[] copy = slice.ToArray();
            string storageKey = _binaryStore.WriteAsync(copy).AsTask().GetAwaiter().GetResult();
            var externalValue = new AssetBinaryValue
            {
                Storage = "external",
                StorageKey = storageKey,
                ByteLength = written,
            };
            return JsonSerializer.Serialize(externalValue, AssetJsonContext.Default.AssetBinaryValue);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private object DeserializeDataFromSnapshot(string serialized)
    {
        if (serialized.Contains("\"__asset\":\"axrone.binary\"", StringComparison.Ordinal) ||
            serialized.Contains("\"__asset\": \"axrone.binary\"", StringComparison.Ordinal))
        {
            AssetBinaryValue? envelope = JsonSerializer.Deserialize(serialized, AssetJsonContext.Default.AssetBinaryValue);
            if (envelope is not null)
            {
                byte[] raw = envelope.Storage == "inline" && envelope.Data is not null
                    ? Convert.FromBase64String(envelope.Data)
                    : (envelope.StorageKey is not null
                        ? _binaryStore.ReadAsync(envelope.StorageKey).AsTask().GetAwaiter().GetResult()
                        : Array.Empty<byte>());

                if (TCodec.TryDeserialize(raw, out object? decoded))
                {
                    return decoded!;
                }

                return raw;
            }
        }

        if (TCodec.TryDeserialize(Encoding.UTF8.GetBytes(serialized), out object? fallback))
        {
            return fallback!;
        }

        return serialized;
    }

    private bool TryResolveId(in AssetSelector selector, out AssetId id)
    {
        switch (selector.Kind)
        {
            case SelectorKind.Id:
            case SelectorKind.VersionedReference:
                id = selector.Id;
                return _assetsById.ContainsKey(id);

            case SelectorKind.Key:
                if (_keyIndex.TryGetValue(selector.Key, out id))
                {
                    if (selector.KindFilterHash == 0UL)
                    {
                        return true;
                    }

                    if (_assetsById.TryGetValue(id, out StoredAsset? keyed) &&
                        Fnv1a64.ComputeUtf8Chars(keyed.Kind.AsSpan()) == selector.KindFilterHash)
                    {
                        return true;
                    }

                    id = default;
                }

                if (_aliasIndex.TryGetValue(selector.Key, out id))
                {
                    if (selector.KindFilterHash == 0UL)
                    {
                        return true;
                    }

                    if (_assetsById.TryGetValue(id, out StoredAsset? aliased) &&
                        Fnv1a64.ComputeUtf8Chars(aliased.Kind.AsSpan()) == selector.KindFilterHash)
                    {
                        return true;
                    }

                    id = default;
                }

                return false;

            default:
                id = default;
                return false;
        }
    }

    private void AddDependencyEdges(AssetId id, IReadOnlyList<AssetId> dependencies)
    {
        foreach (AssetId dep in dependencies)
        {
            ref HashSet<AssetId>? dependents = ref CollectionsMarshal.GetValueRefOrAddDefault(_dependentsById, dep, out _);
            dependents ??= new HashSet<AssetId>();
            dependents.Add(id);
        }
    }

    private void RemoveDependencyEdges(AssetId id, IReadOnlyList<AssetId> dependencies)
    {
        foreach (AssetId dep in dependencies)
        {
            if (_dependentsById.TryGetValue(dep, out HashSet<AssetId>? dependents))
            {
                dependents.Remove(id);
                if (dependents.Count == 0)
                {
                    _dependentsById.Remove(dep);
                }
            }
        }
    }

    private void ClearInternal()
    {
        foreach (StoredAsset asset in _assetsById.Values)
        {
            TLifecycle.OnDeleted(asset.Data!);
        }

        _assetsById.Clear();
        _revisionsById.Clear();
        _keyIndex.Clear();
        _aliasIndex.Clear();
        _dependentsById.Clear();
        _sourceBindings.Clear();
        _catalog.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_gate)
        {
            ClearInternal();
        }

        _dispatcher.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_gate)
        {
            ClearInternal();
        }

        await _dispatcher.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
