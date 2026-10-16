namespace Axrone.Audio;

/// <summary>
/// Bus registration.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioBusDefinition(
    AudioBusId Id,
    AudioBusId ParentId = default,
    float Volume = 1.0f,
    bool Mute = false,
    float Pan = 0.0f,
    object? Metadata = null);

/// <summary>
/// Committed bus state with effective gain.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioBusState(
    AudioBusId Id,
    AudioBusId ParentId,
    float Volume,
    bool Mute,
    float Pan,
    float EffectiveGain,
    int ChildCount,
    object? Metadata);

/// <summary>
/// Partial bus update.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioBusPatch(
    float? Volume = null,
    bool? Mute = null,
    float? Pan = null,
    AudioBusId? ParentId = null);

/// <summary>
/// Directed bus topology: id-indexed nodes with parent gain accumulation.
/// Muting a bus zeroes its subtree; gains clamp volume to [0, 4].
/// </summary>
public sealed class AudioBusRegistry
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct BusNode
    {
        [FieldOffset(0)] public AudioBusDefinition Definition;
        [FieldOffset(32)] public float EffectiveGain;
        [FieldOffset(36)] public bool IsActive;
        [FieldOffset(40)] public int ChildCount;
    }

    private readonly BusNode[] _nodes;
    private readonly int[][] _childrenIndices;
    private readonly AudioObservabilityRuntime _observability;
    private readonly Lock _gate = new();
    private readonly int _maxBuses;

    /// <summary>Creates a registry with the default hierarchy.</summary>
    public AudioBusRegistry(AudioObservabilityRuntime observability, int maxBuses = 128)
    {
        _maxBuses = maxBuses;
        _nodes = new BusNode[maxBuses];
        _childrenIndices = new int[maxBuses][];
        for (int i = 0; i < maxBuses; i++)
        {
            _childrenIndices[i] = new int[maxBuses];
        }

        _observability = observability;

        InitializeDefaultHierarchy();
    }

    private void InitializeDefaultHierarchy()
    {
        ReadOnlySpan<AudioBusDefinition> defaults =
        [
            new(AudioBusId.Master, AudioBusId.Invalid, 1.0f, false, 0.0f),
            new(AudioBusId.Music, AudioBusId.Master, 1.0f, false, 0.0f),
            new(AudioBusId.Sfx, AudioBusId.Master, 1.0f, false, 0.0f),
            new(AudioBusId.Voice, AudioBusId.Master, 1.0f, false, 0.0f),
            new(AudioBusId.Ambient, AudioBusId.Music, 1.0f, false, 0.0f),
            new(AudioBusId.Soundtrack, AudioBusId.Music, 1.0f, false, 0.0f),
            new(AudioBusId.Ui, AudioBusId.Sfx, 1.0f, false, 0.0f),
            new(AudioBusId.World, AudioBusId.Sfx, 1.0f, false, 0.0f),
        ];

        lock (_gate)
        {
            for (int i = 0; i < defaults.Length; i++)
            {
                UpsertInternal(defaults[i]);
            }

            RebuildHierarchyTopology();
        }
    }

    /// <summary>Registers or replaces a bus.</summary>
    public AudioBusState Upsert(in AudioBusDefinition definition)
    {
        lock (_gate)
        {
            AudioBusState state = UpsertInternal(definition);
            RebuildHierarchyTopology();
            _observability.Counters.IncrementBusMutations();
            _observability.Dispatch(AudioEventType.BusUpserted, definition.Id.Value, definition.ParentId.Value, state.EffectiveGain);
            return state;
        }
    }

    /// <summary>Applies a partial update.</summary>
    public AudioBusState UpdateBus(AudioBusId id, in AudioBusPatch patch)
    {
        lock (_gate)
        {
            uint idx = id.Value;
            if (idx >= (uint)_maxBuses || !_nodes[idx].IsActive)
            {
                AudioThrowHelper.ThrowBus(id, "Bus is not registered.");
            }

            ref BusNode node = ref _nodes[idx];
            AudioBusDefinition def = node.Definition;

            node.Definition = def with
            {
                Volume = patch.Volume ?? def.Volume,
                Mute = patch.Mute ?? def.Mute,
                Pan = patch.Pan ?? def.Pan,
                ParentId = patch.ParentId ?? def.ParentId,
            };

            RebuildHierarchyTopology();
            _observability.Counters.IncrementBusMutations();
            _observability.Dispatch(AudioEventType.BusUpserted, id.Value, node.Definition.ParentId.Value, node.EffectiveGain);

            return BuildBusState((int)idx);
        }
    }

    /// <summary>Reads effective gain; zero when unknown.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float GetEffectiveGain(AudioBusId id)
    {
        uint idx = id.Value;
        return idx < (uint)_maxBuses && _nodes[idx].IsActive ? _nodes[idx].EffectiveGain : 0.0f;
    }

    /// <summary>Enumerates active buses.</summary>
    public BusEnumerator GetEnumerator() => new(_nodes);

    /// <summary>Duck-typed zero-allocation enumerator over active buses.</summary>
    public ref struct BusEnumerator
    {
        private readonly BusNode[] _nodes;
        private int _index;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal BusEnumerator(BusNode[] nodes)
        {
            _nodes = nodes;
            _index = -1;
        }

        /// <summary>Advances to the next active bus.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (++_index < _nodes.Length)
            {
                if (_nodes[_index].IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Current bus state.</summary>
        public readonly AudioBusState Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref readonly BusNode n = ref _nodes[_index];
                return new AudioBusState(
                    n.Definition.Id,
                    n.Definition.ParentId,
                    n.Definition.Volume,
                    n.Definition.Mute,
                    n.Definition.Pan,
                    n.EffectiveGain,
                    n.ChildCount,
                    n.Definition.Metadata);
            }
        }
    }

    private AudioBusState UpsertInternal(in AudioBusDefinition definition)
    {
        uint idx = definition.Id.Value;
        if (idx >= (uint)_maxBuses)
        {
            AudioThrowHelper.ThrowConfig("audio.invalid-bus", "Bus id exceeds registry bounds.");
        }

        _nodes[idx].Definition = definition;
        _nodes[idx].IsActive = true;
        return BuildBusState((int)idx);
    }

    private void RebuildHierarchyTopology()
    {
        for (int i = 0; i < _maxBuses; i++)
        {
            _nodes[i].ChildCount = 0;
        }

        for (int i = 0; i < _maxBuses; i++)
        {
            if (!_nodes[i].IsActive)
            {
                continue;
            }

            uint parentIdx = _nodes[i].Definition.ParentId.Value;
            if (parentIdx != 0U && parentIdx < (uint)_maxBuses && _nodes[parentIdx].IsActive && parentIdx != (uint)i)
            {
                int count = _nodes[parentIdx].ChildCount;
                _childrenIndices[parentIdx][count] = i;
                _nodes[parentIdx].ChildCount = count + 1;
            }
        }

        Span<bool> visited = stackalloc bool[_maxBuses];
        for (int i = 0; i < _maxBuses; i++)
        {
            if (_nodes[i].IsActive && !visited[i])
            {
                ComputeGainsRecursive(i, visited);
            }
        }
    }

    private float ComputeGainsRecursive(int idx, Span<bool> visited)
    {
        if (visited[idx])
        {
            return _nodes[idx].EffectiveGain;
        }

        ref BusNode node = ref _nodes[idx];
        if (!node.IsActive)
        {
            return 0.0f;
        }

        if (node.Definition.Mute)
        {
            node.EffectiveGain = 0.0f;
            visited[idx] = true;
            return 0.0f;
        }

        float parentGain = 1.0f;
        uint p = node.Definition.ParentId.Value;
        if (p != 0U && p < (uint)_maxBuses && p != (uint)idx)
        {
            parentGain = ComputeGainsRecursive((int)p, visited);
        }

        node.EffectiveGain = Math.Clamp(node.Definition.Volume, 0.0f, 4.0f) * parentGain;
        visited[idx] = true;
        return node.EffectiveGain;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AudioBusState BuildBusState(int idx)
    {
        ref BusNode n = ref _nodes[idx];
        return new AudioBusState(
            n.Definition.Id,
            n.Definition.ParentId,
            n.Definition.Volume,
            n.Definition.Mute,
            n.Definition.Pan,
            n.EffectiveGain,
            n.ChildCount,
            n.Definition.Metadata);
    }
}
