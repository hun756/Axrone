namespace Axrone.Shapes2D;

/// <summary>Registry options.</summary>
public readonly record struct ShapeRegistryOptions(
    CapacityLimit MaxShapes = default,
    CapacityLimit MaxCompiledEntries = default,
    ShapeApproximationOptions Approximation = default)
{
    /// <summary>Maximum shapes.</summary>
    public CapacityLimit MaxShapes { get; init; } = MaxShapes.Value == 0 ? CapacityLimit.DefaultShapes : MaxShapes;

    /// <summary>Maximum compiled entries.</summary>
    public CapacityLimit MaxCompiledEntries { get; init; } = MaxCompiledEntries.Value == 0 ? CapacityLimit.DefaultCompiled : MaxCompiledEntries;

    /// <summary>Default options.</summary>
    public static readonly ShapeRegistryOptions Default = new(CapacityLimit.DefaultShapes, CapacityLimit.DefaultCompiled);
}

/// <summary>Registry statistics.</summary>
public readonly record struct ShapeRegistryStats(
    int ShapeCount,
    int FingerprintCount,
    int CompiledCount,
    bool Disposed);

/// <summary>Interning registry with an LRU compiled cache.</summary>
public sealed class ShapeRegistry : IDisposable
{
    private readonly Lock _sync = new();

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct CachePaddedState
    {
        [FieldOffset(0)]
        public uint NextId;
        [FieldOffset(64)]
        public int DisposedFlag;
    }

    private CachePaddedState _state;
    private readonly int _maxShapes;
    private readonly int _maxCompiledEntries;
    private readonly ShapeApproximationOptions _defaultApproximation;

    private readonly Dictionary<string, Shape2D> _shapesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _fingerprintsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _idsByFingerprint = new(StringComparer.Ordinal);

    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _compiledByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _compiledKeysByFingerprint = new(StringComparer.Ordinal);
    private readonly LinkedList<CacheEntry> _lruList = new();

    private readonly record struct CacheEntry(string Key, string Fingerprint, CompiledShape2D Compiled);

    /// <summary>Creates a registry.</summary>
    public ShapeRegistry(ShapeRegistryOptions options = default)
    {
        if (options.Equals(default(ShapeRegistryOptions)))
        {
            options = ShapeRegistryOptions.Default;
        }

        _maxShapes = options.MaxShapes;
        _maxCompiledEntries = options.MaxCompiledEntries;
        _defaultApproximation = options.Approximation;
        _state.NextId = 1;
        _state.DisposedFlag = 0;
    }

    /// <summary>Statistics snapshot.</summary>
    public ShapeRegistryStats Stats
    {
        get
        {
            lock (_sync)
            {
                return new ShapeRegistryStats(
                    _shapesById.Count,
                    _idsByFingerprint.Count,
                    _compiledByKey.Count,
                    Volatile.Read(ref _state.DisposedFlag) != 0);
            }
        }
    }

    /// <summary>Whether the id is registered.</summary>
    public bool Has(string id)
    {
        lock (_sync)
        {
            return _shapesById.ContainsKey(id);
        }
    }

    /// <summary>Gets a shape or null.</summary>
    public Shape2D? Get(string id)
    {
        lock (_sync)
        {
            EnsureActive();
            return _shapesById.GetValueOrDefault(id);
        }
    }

    /// <summary>Registers a shape, interning by fingerprint.</summary>
    public string Register(Shape2D shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        lock (_sync)
        {
            EnsureActive();
            string fp = shape.Fingerprint;
            if (_idsByFingerprint.TryGetValue(fp, out string? existingId))
            {
                return existingId;
            }

            if (_shapesById.Count >= _maxShapes)
            {
                ThrowHelper.ThrowRegistry(Shapes2DErrorCode.CapacityExceeded, $"Shape registry capacity of {_maxShapes} entries exceeded.");
            }

            ShapeId newId = new(_state.NextId++);
            string id = newId.ToString();
            _shapesById.Add(id, shape);
            _fingerprintsById.Add(id, fp);
            _idsByFingerprint.Add(fp, id);
            return id;
        }
    }

    /// <summary>Unregisters a shape and its compiled entries.</summary>
    public bool Unregister(string id)
    {
        lock (_sync)
        {
            EnsureActive();
            if (!_fingerprintsById.Remove(id, out string? fp))
            {
                return false;
            }

            _idsByFingerprint.Remove(fp);
            _shapesById.Remove(id);

            if (_compiledKeysByFingerprint.Remove(fp, out HashSet<string>? keys))
            {
                foreach (string k in keys)
                {
                    if (_compiledByKey.Remove(k, out LinkedListNode<CacheEntry>? node))
                    {
                        _lruList.Remove(node);
                    }
                }
            }

            return true;
        }
    }

    /// <summary>Compiles a registered shape.</summary>
    public CompiledShape2D Compile(string id, ShapeCompileOptions options = default)
    {
        lock (_sync)
        {
            EnsureActive();
            if (!_shapesById.TryGetValue(id, out Shape2D? shape))
            {
                ThrowHelper.ThrowRegistry(Shapes2DErrorCode.ShapeNotFound, $"Shape '{id}' is not registered.");
            }

            return CompileInternal(shape, options);
        }
    }

    /// <summary>Compiles a shape without registering it.</summary>
    public CompiledShape2D Compile(Shape2D shape, ShapeCompileOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(shape);

        lock (_sync)
        {
            EnsureActive();
            return CompileInternal(shape, options);
        }
    }

    private CompiledShape2D CompileInternal(Shape2D shape, ShapeCompileOptions options)
    {
        string fp = shape.Fingerprint;
        ShapeApproximationOptions approx = options.Approximation.CurveTolerance > 0f ? options.Approximation : _defaultApproximation;
        ShapeCompileOptions resolvedOptions = options with { Approximation = approx };

        string cacheKey = FormattableString.Invariant($"{fp}|{approx.CurveTolerance}|{approx.MinCurveSegments}|{approx.MaxCurveSegments}|{(resolvedOptions.IncludeFillMesh ? 1 : 0)}|{(resolvedOptions.IncludeStrokeMesh ? 1 : 0)}");

        if (_compiledByKey.TryGetValue(cacheKey, out LinkedListNode<CacheEntry>? existingNode))
        {
            _lruList.Remove(existingNode);
            _lruList.AddLast(existingNode);
            return existingNode.Value.Compiled;
        }

        CompiledShape2D compiled = ShapeQueries.Compile(shape, resolvedOptions);

        while (_compiledByKey.Count >= _maxCompiledEntries && _lruList.First is not null)
        {
            LinkedListNode<CacheEntry> oldest = _lruList.First;
            _lruList.RemoveFirst();
            _compiledByKey.Remove(oldest.Value.Key);
            if (_compiledKeysByFingerprint.TryGetValue(oldest.Value.Fingerprint, out HashSet<string>? set))
            {
                set.Remove(oldest.Value.Key);
                if (set.Count == 0)
                {
                    _compiledKeysByFingerprint.Remove(oldest.Value.Fingerprint);
                }
            }
        }

        var entry = new CacheEntry(cacheKey, fp, compiled);
        LinkedListNode<CacheEntry> newNode = _lruList.AddLast(entry);
        _compiledByKey[cacheKey] = newNode;

        if (!_compiledKeysByFingerprint.TryGetValue(fp, out HashSet<string>? keys))
        {
            keys = new HashSet<string>(StringComparer.Ordinal);
            _compiledKeysByFingerprint[fp] = keys;
        }

        keys.Add(cacheKey);

        return compiled;
    }

    /// <summary>Serializes a registered shape.</summary>
    public string Serialize(string id)
    {
        lock (_sync)
        {
            EnsureActive();
            if (!_shapesById.TryGetValue(id, out Shape2D? shape))
            {
                ThrowHelper.ThrowRegistry(Shapes2DErrorCode.ShapeNotFound, $"Shape '{id}' is not registered.");
            }

            return ShapeSerializer.StringifyShape(shape);
        }
    }

    /// <summary>Clears everything.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _shapesById.Clear();
            _fingerprintsById.Clear();
            _idsByFingerprint.Clear();
            _compiledByKey.Clear();
            _compiledKeysByFingerprint.Clear();
            _lruList.Clear();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_sync)
        {
            if (Volatile.Read(ref _state.DisposedFlag) != 0)
            {
                return;
            }

            Clear();
            Volatile.Write(ref _state.DisposedFlag, 1);
        }

        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureActive()
    {
        if (Volatile.Read(ref _state.DisposedFlag) != 0)
        {
            ThrowHelper.ThrowRegistry(Shapes2DErrorCode.RegistryDisposed, "Shape registry has been disposed.");
        }
    }
}
