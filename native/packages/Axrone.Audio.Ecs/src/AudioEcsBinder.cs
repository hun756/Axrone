namespace Axrone.Audio.Ecs;

using Axrone.Audio;
using Axrone.Ecs;

/// <summary>
/// Frame binder between the ECS world and the audio system: per-entity
/// command queues drain into source playback, listener links ensure
/// registry entries. Listener transform sync arrives with the Scene round.
/// </summary>
public sealed class AudioEcsBinder : IDisposable
{
    private const int QueueCapacity = 32;

    private readonly struct CommandQueue
    {
        public readonly AudioSourceCommand[] Ring;
        public readonly int Head;
        public readonly int Tail;
        public readonly int Count;

        public CommandQueue(AudioSourceCommand[] ring, int head, int tail, int count)
        {
            Ring = ring;
            Head = head;
            Tail = tail;
            Count = count;
        }
    }

    private readonly AudioSystem _system;
    private readonly World _world;
    private readonly Dictionary<Entity, CommandQueue> _queues = new();
    private readonly HashSet<Entity> _listeners = new();
    private readonly Lock _gate = new();
    private int _disposed;

    /// <summary>Creates a binder.</summary>
    public AudioEcsBinder(AudioSystem system, World world)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(world);
        _system = system;
        _world = world;
    }

    /// <summary>Queues a play command for an entity.</summary>
    public void Play(Entity entity, in AudioSourcePlayRequest request = default) =>
        Enqueue(entity, new AudioSourceCommand(AudioSourceCommandType.Play, request));

    /// <summary>Queues a pause command for an entity.</summary>
    public void Pause(Entity entity) =>
        Enqueue(entity, new AudioSourceCommand(AudioSourceCommandType.Pause, default));

    /// <summary>Queues a resume command for an entity.</summary>
    public void Resume(Entity entity) =>
        Enqueue(entity, new AudioSourceCommand(AudioSourceCommandType.Resume, default));

    /// <summary>Queues a stop command for an entity.</summary>
    public void Stop(Entity entity) =>
        Enqueue(entity, new AudioSourceCommand(AudioSourceCommandType.Stop, default));

    /// <summary>Tracks a listener entity, ensuring its registry entry.</summary>
    public void AttachListener(Entity entity)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (_listeners.Add(entity) && _world.HasComponent<AudioListenerRef>(entity))
            {
                AudioListenerId id = _world.GetComponent<AudioListenerRef>(entity).ListenerId;
                _system.Listeners.Upsert(id, AudioListenerDescriptor.Default);
            }
        }
    }

    /// <summary>Untracks a listener entity.</summary>
    public void DetachListener(Entity entity)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            _listeners.Remove(entity);
        }
    }

    /// <summary>Drains queues into playback and refreshes listener links.</summary>
    public void Update()
    {
        ThrowIfDisposed();
        Span<AudioSourceCommand> drain = stackalloc AudioSourceCommand[QueueCapacity];

        lock (_gate)
        {
            List<Entity>? dead = null;
            foreach ((Entity entity, CommandQueue queue) in _queues)
            {
                if (!_world.HasComponent<AudioSourceRef>(entity))
                {
                    dead ??= new List<Entity>();
                    dead.Add(entity);
                    continue;
                }

                AudioSourceId sourceId = _world.GetComponent<AudioSourceRef>(entity).SourceId;
                AudioSourceCommand[] ring = queue.Ring;
                int head = queue.Head;
                int remaining = queue.Count;
                int written = 0;
                while (remaining > 0 && written < drain.Length)
                {
                    drain[written++] = ring[head];
                    head = (head + 1) % ring.Length;
                    remaining--;
                }

                for (int i = 0; i < written; i++)
                {
                    Execute(sourceId, drain[i]);
                }

                _queues[entity] = new CommandQueue(ring, head, queue.Tail, remaining);
            }

            if (dead is not null)
            {
                foreach (Entity entity in dead)
                {
                    _queues.Remove(entity);
                }
            }

            List<Entity>? gone = null;
            foreach (Entity entity in _listeners)
            {
                if (!_world.HasComponent<AudioListenerRef>(entity))
                {
                    gone ??= new List<Entity>();
                    gone.Add(entity);
                }
            }

            if (gone is not null)
            {
                foreach (Entity entity in gone)
                {
                    _listeners.Remove(entity);
                }
            }
        }
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
            _queues.Clear();
            _listeners.Clear();
        }

        GC.SuppressFinalize(this);
    }

    private void Enqueue(Entity entity, in AudioSourceCommand command)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (!_queues.TryGetValue(entity, out CommandQueue queue))
            {
                queue = new CommandQueue(new AudioSourceCommand[QueueCapacity], 0, 0, 0);
            }

            AudioSourceCommand[] ring = queue.Ring;
            int head = queue.Head;
            int tail = queue.Tail;
            int count = queue.Count;

            if (count >= ring.Length)
            {
                head = (head + 1) % ring.Length;
                count--;
            }

            ring[tail] = command;
            tail = (tail + 1) % ring.Length;
            count++;

            _queues[entity] = new CommandQueue(ring, head, tail, count);
        }
    }

    private void Execute(AudioSourceId sourceId, in AudioSourceCommand command)
    {
        switch (command.Type)
        {
            case AudioSourceCommandType.Play:
                _system.Sources.Play(sourceId, command.Request);
                break;
            case AudioSourceCommandType.Pause:
                _system.Sources.Pause(sourceId);
                break;
            case AudioSourceCommandType.Resume:
                _system.Sources.Resume(sourceId);
                break;
            case AudioSourceCommandType.Stop:
                _system.Sources.Stop(sourceId);
                break;
            default:
                break;
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new AudioDisposedException(nameof(AudioEcsBinder));
        }
    }
}
