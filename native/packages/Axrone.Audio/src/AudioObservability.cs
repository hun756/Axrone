namespace Axrone.Audio;

/// <summary>
/// Runtime event discriminant.
/// </summary>
public enum AudioEventType : byte
{
    /// <summary>Bus upserted.</summary>
    BusUpserted = 0,

    /// <summary>Bus removed.</summary>
    BusRemoved = 1,

    /// <summary>Listener upserted.</summary>
    ListenerUpserted = 2,

    /// <summary>Listener removed.</summary>
    ListenerRemoved = 3,

    /// <summary>Listener activated.</summary>
    ListenerActivated = 4,

    /// <summary>Source upserted.</summary>
    SourceUpserted = 5,

    /// <summary>Source removed.</summary>
    SourceRemoved = 6,

    /// <summary>Source played.</summary>
    SourcePlayed = 7,

    /// <summary>Source paused.</summary>
    SourcePaused = 8,

    /// <summary>Source resumed.</summary>
    SourceResumed = 9,

    /// <summary>Source stopped.</summary>
    SourceStopped = 10,

    /// <summary>Source ended.</summary>
    SourceEnded = 11,

    /// <summary>Source error.</summary>
    SourceError = 12,

    /// <summary>Snapshot captured.</summary>
    SnapshotCaptured = 13,

    /// <summary>Snapshot restored.</summary>
    SnapshotRestored = 14,

    /// <summary>System suspended.</summary>
    SystemSuspended = 15,

    /// <summary>System resumed.</summary>
    SystemResumed = 16,

    /// <summary>System disposed.</summary>
    SystemDisposed = 17,
}

/// <summary>
/// One runtime event: type, timestamp and two id payloads plus a value.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct AudioRuntimeEvent(
    AudioEventType Type,
    long TimestampTicks,
    uint PrimaryId,
    uint SecondaryId,
    float Value)
{
    /// <summary>Creates an event stamped with the current timestamp.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AudioRuntimeEvent Create(AudioEventType type, uint primaryId, uint secondaryId = 0, float value = 0.0f) =>
        new(type, Stopwatch.GetTimestamp(), primaryId, secondaryId, value);
}

/// <summary>
/// Receives runtime events synchronously on the dispatching thread.
/// </summary>
public interface IAudioEventListener
{
    /// <summary>Handles one event.</summary>
    void OnAudioEvent(in AudioRuntimeEvent ev);
}

/// <summary>
/// False-sharing-immune diagnostics counters: one counter per cache line.
/// Reference type so registries share a single instance.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 576)]
public sealed class CacheAlignedDiagnosticsCounters
{
    [FieldOffset(0)] private long _emittedEventCount;
    [FieldOffset(64)] private long _busMutationCount;
    [FieldOffset(128)] private long _listenerMutationCount;
    [FieldOffset(192)] private long _sourceMutationCount;
    [FieldOffset(256)] private long _playbackCommandCount;
    [FieldOffset(320)] private long _playbackCompletionCount;
    [FieldOffset(384)] private long _playbackErrorCount;
    [FieldOffset(448)] private long _snapshotOperationCount;
    [FieldOffset(512)] private long _lifecycleTransitionCount;

    /// <summary>Emitted events.</summary>
    public long EmittedEventCount => Volatile.Read(ref _emittedEventCount);

    /// <summary>Bus mutations.</summary>
    public long BusMutationCount => Volatile.Read(ref _busMutationCount);

    /// <summary>Listener mutations.</summary>
    public long ListenerMutationCount => Volatile.Read(ref _listenerMutationCount);

    /// <summary>Source mutations.</summary>
    public long SourceMutationCount => Volatile.Read(ref _sourceMutationCount);

    /// <summary>Playback commands.</summary>
    public long PlaybackCommandCount => Volatile.Read(ref _playbackCommandCount);

    /// <summary>Playback completions.</summary>
    public long PlaybackCompletionCount => Volatile.Read(ref _playbackCompletionCount);

    /// <summary>Playback errors.</summary>
    public long PlaybackErrorCount => Volatile.Read(ref _playbackErrorCount);

    /// <summary>Snapshot operations.</summary>
    public long SnapshotOperationCount => Volatile.Read(ref _snapshotOperationCount);

    /// <summary>Lifecycle transitions.</summary>
    public long LifecycleTransitionCount => Volatile.Read(ref _lifecycleTransitionCount);

    /// <summary>Increments emitted events.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementEvents() => Interlocked.Increment(ref _emittedEventCount);

    /// <summary>Increments bus mutations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementBusMutations() => Interlocked.Increment(ref _busMutationCount);

    /// <summary>Increments listener mutations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementListenerMutations() => Interlocked.Increment(ref _listenerMutationCount);

    /// <summary>Increments source mutations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementSourceMutations() => Interlocked.Increment(ref _sourceMutationCount);

    /// <summary>Increments playback commands.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementCommands() => Interlocked.Increment(ref _playbackCommandCount);

    /// <summary>Increments playback completions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementCompletions() => Interlocked.Increment(ref _playbackCompletionCount);

    /// <summary>Increments playback errors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementErrors() => Interlocked.Increment(ref _playbackErrorCount);

    /// <summary>Increments snapshot operations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementSnapshots() => Interlocked.Increment(ref _snapshotOperationCount);

    /// <summary>Increments lifecycle transitions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementLifecycle() => Interlocked.Increment(ref _lifecycleTransitionCount);
}

/// <summary>
/// Synchronous diagnostics fan-out: counters plus a bounded listener set.
/// Diagnostics path only; the mixer never blocks on listeners.
/// </summary>
public sealed class AudioObservabilityRuntime
{
    private readonly CacheAlignedDiagnosticsCounters _counters;
    private readonly IAudioEventListener?[] _listeners;
    private readonly Lock _gate = new();
    private int _listenerCount;

    /// <summary>Shared counters.</summary>
    public CacheAlignedDiagnosticsCounters Counters => _counters;

    /// <summary>Creates a runtime.</summary>
    public AudioObservabilityRuntime(CacheAlignedDiagnosticsCounters counters, int maxListeners = 64)
    {
        _counters = counters;
        _listeners = new IAudioEventListener[maxListeners];
    }

    /// <summary>Subscribes a listener.</summary>
    public void Subscribe(IAudioEventListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_gate)
        {
            for (int i = 0; i < _listeners.Length; i++)
            {
                if (_listeners[i] is null)
                {
                    _listeners[i] = listener;
                    _listenerCount++;
                    return;
                }
            }

            AudioThrowHelper.ThrowConfig("audio.listener-capacity", "Exceeded listener capacity.");
        }
    }

    /// <summary>Unsubscribes a listener.</summary>
    public void Unsubscribe(IAudioEventListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_gate)
        {
            for (int i = 0; i < _listeners.Length; i++)
            {
                if (ReferenceEquals(_listeners[i], listener))
                {
                    _listeners[i] = null;
                    _listenerCount--;
                    return;
                }
            }
        }
    }

    /// <summary>Dispatches one event to all listeners.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Dispatch(AudioEventType type, uint primaryId, uint secondaryId = 0, float value = 0.0f)
    {
        _counters.IncrementEvents();
        if (Volatile.Read(ref _listenerCount) == 0)
        {
            return;
        }

        AudioRuntimeEvent ev = AudioRuntimeEvent.Create(type, primaryId, secondaryId, value);
        lock (_gate)
        {
            for (int i = 0; i < _listeners.Length; i++)
            {
                _listeners[i]?.OnAudioEvent(in ev);
            }
        }
    }
}
