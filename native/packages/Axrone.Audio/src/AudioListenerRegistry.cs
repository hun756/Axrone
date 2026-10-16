namespace Axrone.Audio;

/// <summary>
/// Listener transform descriptor.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioListenerDescriptor(
    Vector3 Position = default,
    Vector3 Forward = default,
    Vector3 Up = default,
    float GlobalVolume = 1.0f)
{
    /// <summary>Origin-facing listener at unity gain.</summary>
    public static AudioListenerDescriptor Default => new(
        Vector3.Zero,
        -Vector3.UnitZ,
        Vector3.UnitY,
        1.0f);
}

/// <summary>
/// Slot-indexed listener store with one active listener.
/// </summary>
public sealed class AudioListenerRegistry
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ListenerNode
    {
        public AudioListenerDescriptor Descriptor;
        public bool IsRegistered;
    }

    private readonly ListenerNode[] _nodes;
    private readonly AudioObservabilityRuntime _observability;
    private readonly Lock _gate = new();
    private readonly int _maxListeners;
    private uint _activeRaw = AudioListenerId.Primary.Value;

    /// <summary>Active listener.</summary>
    public AudioListenerId ActiveListenerId => new(Volatile.Read(ref _activeRaw));

    /// <summary>Creates a registry with the primary listener.</summary>
    public AudioListenerRegistry(AudioObservabilityRuntime observability, int maxListeners = 16)
    {
        _maxListeners = maxListeners;
        _nodes = new ListenerNode[maxListeners];
        _observability = observability;

        Upsert(AudioListenerId.Primary, AudioListenerDescriptor.Default);
        SetActive(AudioListenerId.Primary);
    }

    /// <summary>Registers or replaces a listener.</summary>
    public void Upsert(AudioListenerId id, in AudioListenerDescriptor descriptor)
    {
        lock (_gate)
        {
            uint idx = id.Value;
            if (idx >= (uint)_maxListeners)
            {
                AudioThrowHelper.ThrowConfig("audio.invalid-listener", "Listener id exceeds registry bounds.");
            }

            _nodes[idx].Descriptor = descriptor;
            _nodes[idx].IsRegistered = true;

            _observability.Counters.IncrementListenerMutations();
            _observability.Dispatch(AudioEventType.ListenerUpserted, id.Value);
        }
    }

    /// <summary>Selects the active listener.</summary>
    public void SetActive(AudioListenerId id)
    {
        lock (_gate)
        {
            uint idx = id.Value;
            if (idx >= (uint)_maxListeners || !_nodes[idx].IsRegistered)
            {
                AudioThrowHelper.ThrowListener(id, "Listener is not registered.");
            }

            Volatile.Write(ref _activeRaw, id.Value);
            _observability.Dispatch(AudioEventType.ListenerActivated, id.Value);
        }
    }

    /// <summary>Reads the active descriptor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public AudioListenerDescriptor GetActiveDescriptor()
    {
        uint idx = Volatile.Read(ref _activeRaw);
        return _nodes[idx].Descriptor;
    }
}
