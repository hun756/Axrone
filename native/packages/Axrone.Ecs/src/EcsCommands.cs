namespace Axrone.Ecs;

/// <summary>
/// Structural command discriminant.
/// </summary>
public enum CommandKind : byte
{
    /// <summary>Spawn (no-op: entity already exists).</summary>
    Spawn = 0,

    /// <summary>Destroy.</summary>
    Despawn = 1,

    /// <summary>Set component from a payload.</summary>
    SetComponent = 2,

    /// <summary>Add component from a payload.</summary>
    AddComponent = 3,

    /// <summary>Remove a component.</summary>
    RemoveComponent = 4,
}

/// <summary>
/// Discriminated structural command. Component payloads live in the owning
/// <see cref="EntityCommandBuffer"/> and are addressed by offset and length.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public readonly struct EntityCommand
{
    /// <summary>Discriminant.</summary>
    [FieldOffset(0)]
    public readonly CommandKind Kind;

    /// <summary>Target entity.</summary>
    [FieldOffset(4)]
    public readonly Entity Entity;

    /// <summary>Component for component commands.</summary>
    [FieldOffset(12)]
    public readonly ComponentId ComponentId;

    /// <summary>Payload byte offset in the owning buffer.</summary>
    [FieldOffset(16)]
    public readonly int PayloadOffset;

    /// <summary>Payload byte length; zero for commands without a payload.</summary>
    [FieldOffset(20)]
    public readonly int PayloadLength;

    /// <summary>Creates a spawn command.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityCommand Spawn(in Entity entity) =>
        new(CommandKind.Spawn, in entity, default, 0, 0);

    /// <summary>Creates a despawn command.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityCommand Despawn(in Entity entity) =>
        new(CommandKind.Despawn, in entity, default, 0, 0);

    /// <summary>Creates a payload-less set command; playback reports an invalid payload.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityCommand Set(in Entity entity, ComponentId id) =>
        new(CommandKind.SetComponent, in entity, id, 0, 0);

    internal static EntityCommand Create(
        CommandKind kind,
        in Entity entity,
        ComponentId componentId,
        int payloadOffset,
        int payloadLength) =>
        new(kind, in entity, componentId, payloadOffset, payloadLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EntityCommand(
        CommandKind kind,
        in Entity entity,
        ComponentId componentId,
        int payloadOffset,
        int payloadLength)
    {
        Kind = kind;
        Entity = entity;
        ComponentId = componentId;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
    }
}

/// <summary>
/// Deferred structural command buffer: enqueue during iteration, playback against the
/// world afterwards. Component payloads are copied into a pinned byte arena, so a
/// buffer can be filled without per-command allocations. Playback applies commands in
/// enqueue order and drains the buffer on success.
/// </summary>
public sealed class EntityCommandBuffer
{
    private EntityCommand[] _commands;
    private byte[] _payload;
    private int _count;
    private int _payloadCount;

    /// <summary>Creates a buffer.</summary>
    public EntityCommandBuffer(int initialCapacity = 256)
    {
        if (initialCapacity <= 0)
        {
            EcsThrowHelper.ThrowArgumentOutOfRange(nameof(initialCapacity));
        }

        _commands = GC.AllocateArray<EntityCommand>(initialCapacity, pinned: true);
        _payload = GC.AllocateArray<byte>(initialCapacity * 16, pinned: true);
    }

    /// <summary>Pending command count.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Enqueues a payload-less command.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in EntityCommand command)
    {
        if (_count == _commands.Length)
        {
            EntityCommand[] grown = GC.AllocateArray<EntityCommand>(_commands.Length * 2, pinned: true);
            Array.Copy(_commands, grown, _commands.Length);
            _commands = grown;
        }

        _commands[_count++] = command;
    }

    /// <summary>Enqueues an add-component command and copies the component value.</summary>
    public void EnqueueAdd<T>(scoped in Entity entity, scoped in T component) where T : unmanaged
    {
        int offset = WritePayload(in component);
        Enqueue(EntityCommand.Create(CommandKind.AddComponent, in entity, ComponentType<T>.Id, offset, Unsafe.SizeOf<T>()));
    }

    /// <summary>Enqueues a set-component command and copies the component value.</summary>
    public void EnqueueSet<T>(scoped in Entity entity, scoped in T component) where T : unmanaged
    {
        int offset = WritePayload(in component);
        Enqueue(EntityCommand.Create(CommandKind.SetComponent, in entity, ComponentType<T>.Id, offset, Unsafe.SizeOf<T>()));
    }

    /// <summary>Enqueues a remove-component command.</summary>
    public void EnqueueRemove<T>(scoped in Entity entity) where T : unmanaged =>
        Enqueue(EntityCommand.Create(CommandKind.RemoveComponent, in entity, ComponentType<T>.Id, 0, 0));

    /// <summary>Enqueues a despawn command.</summary>
    public void EnqueueDespawn(scoped in Entity entity) =>
        Enqueue(EntityCommand.Despawn(in entity));

    /// <summary>Replays and drains every pending command in enqueue order.</summary>
    public void Playback(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ReadOnlySpan<EntityCommand> commands = new(_commands, 0, _count);
        for (int index = 0; index < commands.Length; index++)
        {
            ref readonly EntityCommand command = ref commands[index];
            switch (command.Kind)
            {
                case CommandKind.Spawn:
                    break;
                case CommandKind.Despawn:
                    world.DestroyEntity(command.Entity);
                    break;
                case CommandKind.AddComponent:
                    GetBinding(command.ComponentId).Add(world, in command.Entity, PayloadOf(in command));
                    break;
                case CommandKind.SetComponent:
                    GetBinding(command.ComponentId).Set(world, in command.Entity, PayloadOf(in command));
                    break;
                case CommandKind.RemoveComponent:
                    GetBinding(command.ComponentId).Remove(world, in command.Entity);
                    break;
                default:
                    EcsThrowHelper.ThrowExhaustiveMatchFailure();
                    break;
            }
        }

        _count = 0;
        _payloadCount = 0;
    }

    /// <summary>Drops every pending command and payload.</summary>
    public void Clear()
    {
        _count = 0;
        _payloadCount = 0;
    }

    private int WritePayload<T>(scoped in T component) where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        if (_payloadCount + size > _payload.Length)
        {
            int capacity = _payload.Length;
            while (capacity < _payloadCount + size)
            {
                capacity *= 2;
            }

            byte[] grown = GC.AllocateArray<byte>(capacity, pinned: true);
            Array.Copy(_payload, grown, _payloadCount);
            _payload = grown;
        }

        int offset = _payloadCount;
        ref byte start = ref MemoryMarshal.GetArrayDataReference(_payload);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, offset), component);
        _payloadCount += size;
        return offset;
    }

    private ReadOnlySpan<byte> PayloadOf(in EntityCommand command) =>
        new(_payload, command.PayloadOffset, command.PayloadLength);

    private static IComponentCommandBinding GetBinding(ComponentId id)
    {
        if (ComponentRegistry.TryGetBinding(id, out IComponentCommandBinding? binding))
        {
            return binding!;
        }

        EcsThrowHelper.ThrowUnknownComponentType(id);
        return null!;
    }
}

/// <summary>Type-erased playback adapter for one component payload shape.</summary>
internal interface IComponentCommandBinding
{
    /// <summary>Adds or replaces a component from payload bytes.</summary>
    void Add(World world, in Entity entity, ReadOnlySpan<byte> payload);

    /// <summary>Sets an existing component from payload bytes.</summary>
    void Set(World world, in Entity entity, ReadOnlySpan<byte> payload);

    /// <summary>Removes the component.</summary>
    void Remove(World world, in Entity entity);
}

internal sealed class ComponentCommandBinding<T> : IComponentCommandBinding where T : unmanaged
{
    public void Add(World world, in Entity entity, ReadOnlySpan<byte> payload)
    {
        T value = Read(payload);
        world.AddComponent(entity, in value);
    }

    public void Set(World world, in Entity entity, ReadOnlySpan<byte> payload)
    {
        T value = Read(payload);
        world.SetComponent(entity, in value);
    }

    public void Remove(World world, in Entity entity) => world.RemoveComponent<T>(in entity);

    private static T Read(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != Unsafe.SizeOf<T>())
        {
            EcsThrowHelper.ThrowInvalidCommandPayload(payload.Length, Unsafe.SizeOf<T>());
        }

        return MemoryMarshal.Read<T>(payload);
    }
}
