namespace Axrone.Lighting;

/// <summary>Rig builder build transition.</summary>
public static partial class RigBuilderExtensions
{
    /// <summary>Builds a rig from a sealed builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LightingRig Build(this RigBuilder<StageSealed> builder)
    {
        RigConfigurationParameters parameters = builder.Parameters;
        return new LightingRig(parameters.RigId, parameters.Environment);
    }
}

/// <summary>Light collection container with versioned reads.</summary>
public sealed class LightingRig : ILightSourceReader, ILightSourceWriter, IDisposable, IAsyncDisposable
{
    private static ulong _globalRigSequence;

    [StructLayout(LayoutKind.Explicit, Size = CacheTopology.DoubleCacheLineSize)]
    private struct ControlTopology
    {
        [FieldOffset(0)]
        public ulong MutationVersion;

        [FieldOffset(CacheTopology.CacheLineSize)]
        public int ActiveReaders;

        [FieldOffset(CacheTopology.CacheLineSize + 4)]
        public int DrainState;
    }

    private readonly Lock _mutationLock = new();
    private readonly LightStorageTable _storage = new(64);
    private readonly TaskCompletionSource _drainCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ControlTopology _control;
    private ulong _lightSequence;

    /// <summary>Rig identity.</summary>
    public RigId Id { get; }

    /// <summary>Environment.</summary>
    public LightingEnvironment Environment { get; private set; }

    /// <summary>Monotonic version.</summary>
    public RigVersion Version
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Volatile.Read(ref _control.MutationVersion));
    }

    /// <summary>Light count.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            lock (_mutationLock)
            {
                return _storage.Count;
            }
        }
    }

    /// <summary>Whether the rig is disposed or draining.</summary>
    public bool IsDisposed => Volatile.Read(ref _control.DrainState) != 0;

    /// <summary>Creates a rig.</summary>
    public LightingRig(RigId? id = null, in LightingEnvironment? environment = null)
    {
        Id = id ?? new RigId(Interlocked.Increment(ref _globalRigSequence));
        Environment = environment ?? LightingEnvironment.Default;
        _control.MutationVersion = 1;
        _control.ActiveReaders = 0;
        _control.DrainState = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureMutable()
    {
        if (Volatile.Read(ref _control.DrainState) != 0)
        {
            ThrowHelper.ThrowDisposed(Id);
        }
    }

    /// <summary>Mints a light identity.</summary>
    public LightId NextLightId() => new(Interlocked.Increment(ref _lightSequence));

    /// <inheritdoc/>
    public void Add(in LightData light)
    {
        lock (_mutationLock)
        {
            EnsureMutable();
            _storage.Add(light);
            Interlocked.Increment(ref _control.MutationVersion);
        }
    }

    /// <inheritdoc/>
    public bool Remove(LightId id)
    {
        lock (_mutationLock)
        {
            EnsureMutable();
            if (_storage.Remove(id))
            {
                Interlocked.Increment(ref _control.MutationVersion);
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public void Update(in LightData light)
    {
        lock (_mutationLock)
        {
            EnsureMutable();
            _storage.Update(light);
            Interlocked.Increment(ref _control.MutationVersion);
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_mutationLock)
        {
            EnsureMutable();
            if (_storage.Count > 0)
            {
                _storage.Clear();
                Interlocked.Increment(ref _control.MutationVersion);
            }
        }
    }

    /// <inheritdoc/>
    public void SetEnvironment(in LightingEnvironment environment)
    {
        lock (_mutationLock)
        {
            EnsureMutable();
            LightingGuards.ValidateEnvironment(environment);
            Environment = environment;
            Interlocked.Increment(ref _control.MutationVersion);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(LightId id)
    {
        lock (_mutationLock)
        {
            return _storage.Contains(id);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(LightId id, out LightData light)
    {
        lock (_mutationLock)
        {
            return _storage.TryGet(id, out light);
        }
    }

    /// <summary>Zero-alloc light enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LightSpanEnumerator GetEnumerator()
    {
        lock (_mutationLock)
        {
            return _storage.GetEnumerator();
        }
    }

    /// <summary>Runs a visitor over all lights.</summary>
    public void ForEach<TVisitor, TState>(scoped ref TState state)
        where TVisitor : struct, ILightVisitor<TState>
        where TState : allows ref struct
    {
        lock (_mutationLock)
        {
            ReadOnlySpan<LightData> span = _storage.AsReadOnlySpan();
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly LightData light = ref span[i];
                TVisitor.Visit(in light, ref state);
            }
        }
    }

    /// <summary>Acquires a versioned read lock.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryAcquireReader(out RigVersion currentVersion)
    {
        int state = Volatile.Read(ref _control.DrainState);
        if (state != 0)
        {
            currentVersion = default;
            return false;
        }

        Interlocked.Increment(ref _control.ActiveReaders);

        if (Volatile.Read(ref _control.DrainState) != 0)
        {
            ReleaseReader();
            currentVersion = default;
            return false;
        }

        currentVersion = new RigVersion(Volatile.Read(ref _control.MutationVersion));
        return true;
    }

    /// <summary>Releases a read lock.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleaseReader()
    {
        if (Interlocked.Decrement(ref _control.ActiveReaders) == 0)
        {
            if (Volatile.Read(ref _control.DrainState) == 1)
            {
                _drainCompletion.TrySetResult();
            }
        }
    }

    /// <summary>Snapshots dense storage into a target.</summary>
    internal void SnapshotStorage(LightData[] target, ref int count)
    {
        lock (_mutationLock)
        {
            _storage.CopyTo(target, ref count);
        }
    }

    /// <summary>Starts the drain.</summary>
    public void Complete(Exception? error = null)
    {
        lock (_mutationLock)
        {
            if (Volatile.Read(ref _control.DrainState) == 0)
            {
                Volatile.Write(ref _control.DrainState, 1);
                if (Volatile.Read(ref _control.ActiveReaders) == 0)
                {
                    if (error != null)
                    {
                        _drainCompletion.TrySetException(error);
                    }
                    else
                    {
                        _drainCompletion.TrySetResult();
                    }
                }
            }
        }
    }

    /// <summary>Drains and clears the rig.</summary>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        Complete();
        await _drainCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        lock (_mutationLock)
        {
            Volatile.Write(ref _control.DrainState, 2);
            _storage.Clear();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_mutationLock)
        {
            if (Volatile.Read(ref _control.DrainState) != 2)
            {
                Volatile.Write(ref _control.DrainState, 2);
                _drainCompletion.TrySetResult();
                _storage.Clear();
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
